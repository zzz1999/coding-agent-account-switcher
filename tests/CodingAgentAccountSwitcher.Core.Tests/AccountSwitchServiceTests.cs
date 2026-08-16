namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class AccountSwitchServiceTests
{
    [Fact]
    public async Task CaptureRejectsAStaleRequestThatWouldCreateADuplicateDisplayName()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        File.WriteAllBytes(authenticationPath, [4, 5, 6]);
        var vault = new AuthenticationProfileVault(Path.Combine(temporary.Path, "vault"));
        var existing = vault.CreateProfile(AgentProvider.Codex, "Personal", [1, 2, 3]);
        var service = CreateService(vault, ProcessInspectionResult.Clear);

        var result = await service.CaptureCurrentLoginAsync(adapter, "personal");

        Assert.Equal(AccountOperationStatus.DisplayNameConflict, result.Status);
        Assert.Equal(existing.ProfileId, result.Profile?.ProfileId);
        Assert.Single(vault.ListProfiles(AgentProvider.Codex));
        Assert.Null(vault.GetActiveProfile(AgentProvider.Codex));
    }

    [Fact]
    public async Task CaptureAndSwitchPreserveRefreshedSourceAndUnrelatedConfiguration()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out var configPath);
        byte[] original = [0x00, 0xFF, 1, 2];
        byte[] refreshed = [0x00, 0xFE, 3, 4];
        byte[] target = [9, 8, 0xC3, 0x28];
        byte[] configuration = [7, 7, 7];
        File.WriteAllBytes(authenticationPath, original);
        File.WriteAllBytes(configPath, configuration);

        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        Assert.Equal(AccountOperationStatus.Success, sourceCapture.Status);

        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        File.WriteAllBytes(authenticationPath, refreshed);

        var confirmation = await service.SwitchAsync(adapter, targetProfile.ProfileId);
        Assert.Equal(AccountOperationStatus.ActiveProfileUpdateConfirmationRequired, confirmation.Status);

        var result = await service.SwitchAsync(
            adapter,
            targetProfile.ProfileId,
            confirmation.ConfirmationFingerprint);

        Assert.Equal(AccountOperationStatus.Success, result.Status);
        Assert.Equal(target, File.ReadAllBytes(authenticationPath));
        Assert.Equal(refreshed, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile!.ProfileId));
        Assert.Equal(configuration, File.ReadAllBytes(configPath));
        Assert.Equal(targetProfile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CaptureDoesNotInspectProviderProcessState()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] original = [1, 2, 3];
        File.WriteAllBytes(authenticationPath, original);
        var vaultPath = System.IO.Path.Combine(temporary.Path, "vault");
        var vault = new AuthenticationProfileVault(vaultPath);
        var service = CreateService(vault, new FailOnUseProcessInspector());

        var result = await service.CaptureCurrentLoginAsync(adapter, "Personal");

        Assert.Equal(AccountOperationStatus.Success, result.Status);
        Assert.Equal(original, File.ReadAllBytes(authenticationPath));
        Assert.Equal(ProcessInspectionResult.Clear, result.ProcessInspection);
        Assert.Equal(original, vault.LoadCredential(AgentProvider.Codex, result.Profile!.ProfileId));
    }

    [Theory]
    [InlineData(ProcessInspectionStatus.Running, AccountOperationStatus.BlockedByRunningProcesses)]
    [InlineData(ProcessInspectionStatus.Unknown, AccountOperationStatus.ProcessInspectionUnknown)]
    public async Task ProcessGuardBlocksSwitchWithoutChangingAuthenticationOrState(
        ProcessInspectionStatus inspectionStatus,
        AccountOperationStatus expectedStatus)
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [1, 3, 5, 7];
        byte[] target = [2, 4, 6, 8];
        File.WriteAllBytes(authenticationPath, source);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var clearService = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await clearService.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        var inspection = new ProcessInspectionResult
        {
            Status = inspectionStatus,
            Processes = inspectionStatus == ProcessInspectionStatus.Running
                ? [new DetectedProcess(42, "ChatGPT")]
                : [],
            Issues = inspectionStatus == ProcessInspectionStatus.Unknown
                ? [new ProcessInspectionIssue("*", "Access denied")]
                : []
        };
        var blockedService = CreateService(vault, inspection);

        var result = await blockedService.SwitchAsync(adapter, targetProfile.ProfileId);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(source, File.ReadAllBytes(authenticationPath));
        Assert.Equal(sourceCapture.Profile!.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Equal(source, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile.ProfileId));
        Assert.Equal(target, vault.LoadCredential(AgentProvider.Codex, targetProfile.ProfileId));
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task ChangedLiveAuthenticationRequiresConfirmationBeforeUpdatingActiveProfile()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSource = [1, 1, 2, 3];
        byte[] changedLiveAuthentication = [8, 5, 3, 2];
        byte[] target = [9, 9, 9, 9];
        File.WriteAllBytes(authenticationPath, savedSource);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        File.WriteAllBytes(authenticationPath, changedLiveAuthentication);

        var result = await service.SwitchAsync(adapter, targetProfile.ProfileId);

        Assert.Equal(AccountOperationStatus.ActiveProfileUpdateConfirmationRequired, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ConfirmationFingerprint));
        Assert.Equal(changedLiveAuthentication, File.ReadAllBytes(authenticationPath));
        Assert.Equal(savedSource, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile!.ProfileId));
        Assert.Equal(sourceCapture.Profile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task ConfirmationFingerprintCannotAuthorizeDifferentLiveAuthentication()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSource = [1, 2, 3, 4];
        byte[] firstChangedLogin = [4, 3, 2, 1];
        byte[] secondChangedLogin = [7, 7, 7, 7];
        byte[] target = [9, 8, 7, 6];
        File.WriteAllBytes(authenticationPath, savedSource);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        File.WriteAllBytes(authenticationPath, firstChangedLogin);

        var firstConfirmation = await service.SwitchAsync(adapter, targetProfile.ProfileId);
        File.WriteAllBytes(authenticationPath, secondChangedLogin);
        var staleConfirmation = await service.SwitchAsync(
            adapter,
            targetProfile.ProfileId,
            firstConfirmation.ConfirmationFingerprint);

        Assert.Equal(AccountOperationStatus.ActiveProfileUpdateConfirmationRequired, firstConfirmation.Status);
        Assert.Equal(AccountOperationStatus.ActiveProfileUpdateConfirmationRequired, staleConfirmation.Status);
        Assert.NotEqual(firstConfirmation.ConfirmationFingerprint, staleConfirmation.ConfirmationFingerprint);
        Assert.Equal(secondChangedLogin, File.ReadAllBytes(authenticationPath));
        Assert.Equal(savedSource, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile!.ProfileId));
        Assert.Equal(sourceCapture.Profile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task LastSelectedProfileCanRestoreItsSavedSnapshotAfterBoundConfirmation()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSnapshot = [1, 3, 3, 7];
        byte[] changedLiveLogin = [4, 2, 4, 2];
        File.WriteAllBytes(authenticationPath, savedSnapshot);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var capture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        File.WriteAllBytes(authenticationPath, changedLiveLogin);

        var confirmation = await service.SwitchAsync(adapter, capture.Profile!.ProfileId);
        var result = await service.SwitchAsync(
            adapter,
            capture.Profile.ProfileId,
            confirmation.ConfirmationFingerprint);

        Assert.Equal(AccountOperationStatus.SavedSnapshotRestoreConfirmationRequired, confirmation.Status);
        Assert.False(string.IsNullOrWhiteSpace(confirmation.ConfirmationFingerprint));
        Assert.Equal(AccountOperationStatus.Success, result.Status);
        Assert.Equal(savedSnapshot, File.ReadAllBytes(authenticationPath));
        Assert.Equal(savedSnapshot, vault.LoadCredential(AgentProvider.Codex, capture.Profile.ProfileId));
        Assert.Equal(capture.Profile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConfirmedCaptureReplacesExistingProfileWithoutCreatingDuplicate()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] original = [1, 2, 3, 4];
        byte[] signedInAgain = [5, 6, 7, 8];
        File.WriteAllBytes(authenticationPath, original);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var initialCapture = await service.CaptureCurrentLoginAsync(adapter, "Work");
        File.WriteAllBytes(authenticationPath, signedInAgain);

        var result = await service.CaptureCurrentLoginAsync(
            adapter,
            "Work",
            initialCapture.Profile!.ProfileId);

        Assert.Equal(AccountOperationStatus.Success, result.Status);
        Assert.Equal(initialCapture.Profile.ProfileId, result.Profile!.ProfileId);
        Assert.Single(vault.ListProfiles(AgentProvider.Codex));
        Assert.Equal(signedInAgain, vault.LoadCredential(AgentProvider.Codex, initialCapture.Profile.ProfileId));
        Assert.Equal(initialCapture.Profile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
    }

    [Fact]
    public async Task FailedActiveStateWriteRemovesNewlyCapturedProfile()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] currentLogin = [2, 4, 6, 8];
        File.WriteAllBytes(authenticationPath, currentLogin);

        var writer = new FailOnceActiveStateWriter { Enabled = true };
        var vault = new AuthenticationProfileVault(
            System.IO.Path.Combine(temporary.Path, "vault"),
            atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);

        var result = await service.CaptureCurrentLoginAsync(adapter, "Personal");

        Assert.Equal(AccountOperationStatus.Failed, result.Status);
        Assert.Empty(vault.ListProfiles(AgentProvider.Codex));
        Assert.Null(vault.GetActiveProfile(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "*.vault", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "*.metadata.json", SearchOption.AllDirectories));
        Assert.Equal(currentLogin, File.ReadAllBytes(authenticationPath));
    }

    [Fact]
    public async Task FailedActiveStateWriteRestoresReplacedProfileExactly()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedLogin = [1, 3, 5, 7];
        byte[] replacementLogin = [2, 4, 6, 8];
        File.WriteAllBytes(authenticationPath, savedLogin);

        var writer = new FailOnceActiveStateWriter();
        var vault = new AuthenticationProfileVault(
            System.IO.Path.Combine(temporary.Path, "vault"),
            atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var initialCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var initialMetadata = initialCapture.Profile!;
        var initialActiveState = vault.GetActiveProfile(AgentProvider.Codex)!;
        File.WriteAllBytes(authenticationPath, replacementLogin);
        writer.Enabled = true;

        var result = await service.CaptureCurrentLoginAsync(
            adapter,
            "Personal",
            initialMetadata.ProfileId);

        Assert.Equal(AccountOperationStatus.Failed, result.Status);
        Assert.Equal(initialMetadata, vault.GetProfile(AgentProvider.Codex, initialMetadata.ProfileId));
        Assert.Equal(savedLogin, vault.LoadCredential(AgentProvider.Codex, initialMetadata.ProfileId));
        Assert.Equal(initialActiveState, vault.GetActiveProfile(AgentProvider.Codex));
        Assert.Single(vault.ListProfiles(AgentProvider.Codex));
        Assert.Equal(replacementLogin, File.ReadAllBytes(authenticationPath));
    }

    [Fact]
    public async Task FailedStateCommitRollsAuthenticationFileBack()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [1, 1, 1];
        byte[] target = [2, 2, 2];
        File.WriteAllBytes(authenticationPath, source);

        var writer = new FailOnceActiveStateWriter();
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"), atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        writer.Enabled = true;

        var result = await service.SwitchAsync(adapter, targetProfile.ProfileId);

        Assert.Equal(AccountOperationStatus.Failed, result.Status);
        Assert.True(result.RolledBack);
        Assert.Equal(source, File.ReadAllBytes(authenticationPath));
        Assert.Equal(sourceCapture.Profile!.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Theory]
    [InlineData(ProcessInspectionStatus.Running)]
    [InlineData(ProcessInspectionStatus.Unknown)]
    public async Task FailedSwitchDefersRollbackWhenFinalProcessCheckIsUnsafe(
        ProcessInspectionStatus inspectionStatus)
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [1, 4, 1, 4];
        byte[] target = [2, 7, 1, 8];
        File.WriteAllBytes(authenticationPath, source);

        var writer = new FailOnceActiveStateWriter();
        var vault = new AuthenticationProfileVault(
            System.IO.Path.Combine(temporary.Path, "vault"),
            atomicWriter: writer);
        var clearService = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await clearService.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        writer.Enabled = true;

        var unsafeInspection = new ProcessInspectionResult
        {
            Status = inspectionStatus,
            Processes = inspectionStatus == ProcessInspectionStatus.Running
                ? [new DetectedProcess(99, "codex")]
                : [],
            Issues = inspectionStatus == ProcessInspectionStatus.Unknown
                ? [new ProcessInspectionIssue("codex", "Synthetic inspection failure")]
                : []
        };
        var service = CreateService(
            vault,
            new SequencedProcessInspector(
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                unsafeInspection));

        var result = await service.SwitchAsync(adapter, targetProfile.ProfileId);

        Assert.Equal(AccountOperationStatus.RecoveryRequired, result.Status);
        Assert.False(result.RolledBack);
        Assert.Equal(unsafeInspection, result.ProcessInspection);
        Assert.Equal(target, File.ReadAllBytes(authenticationPath));
        Assert.Equal(sourceCapture.Profile!.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        var journal = Assert.IsType<SwitchTransactionJournal>(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Equal(SwitchJournalPhase.TargetInstalled, journal.Phase);
    }

    [Fact]
    public async Task AmbiguousAuthenticationWriteFailureAlwaysRestoresSourceBeforeDeletingJournal()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [1, 4, 1, 4];
        byte[] target = [2, 7, 1, 8];
        File.WriteAllBytes(authenticationPath, source);

        var writer = new ThrowAfterAuthenticationCommitWriter(authenticationPath);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"), atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        writer.Enabled = true;

        var result = await service.SwitchAsync(adapter, targetProfile.ProfileId);

        Assert.Equal(AccountOperationStatus.Failed, result.Status);
        Assert.True(result.RolledBack);
        Assert.Equal(source, File.ReadAllBytes(authenticationPath));
        Assert.Equal(sourceCapture.Profile!.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task InterruptedRollbackIsRecoveredFromJournalOnNextStart()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [3, 3, 3];
        byte[] target = [4, 4, 4];
        File.WriteAllBytes(authenticationPath, source);

        var writer = new FailStateAndRollbackWriter(authenticationPath);
        var vaultPath = System.IO.Path.Combine(temporary.Path, "vault");
        var vault = new AuthenticationProfileVault(vaultPath, atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        writer.Enabled = true;

        var failed = await service.SwitchAsync(adapter, targetProfile.ProfileId);

        Assert.Equal(AccountOperationStatus.RecoveryRequired, failed.Status);
        Assert.Equal(target, File.ReadAllBytes(authenticationPath));
        Assert.NotNull(vault.GetPendingJournal(AgentProvider.Codex));

        var restartedVault = new AuthenticationProfileVault(vaultPath);
        var restartedService = CreateService(restartedVault, ProcessInspectionResult.Clear);
        var recovery = await restartedService.RecoverAsync(adapter);

        Assert.Equal(RecoveryStatus.CompletedTargetActivation, recovery.Status);
        Assert.Equal(targetProfile.ProfileId, restartedVault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(restartedVault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task InterruptedConfirmedProfileSwitchRecoversExactPreSwitchBytesAndUpdatesSourceSnapshot()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSource = [1, 2, 3, 4];
        byte[] refreshedSource = [4, 3, 2, 1];
        byte[] target = [9, 8, 7, 6];
        File.WriteAllBytes(authenticationPath, savedSource);

        var writer = new CancelAfterPreparedJournalCommitWriter();
        var vaultPath = System.IO.Path.Combine(temporary.Path, "vault");
        var vault = new AuthenticationProfileVault(vaultPath, atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        File.WriteAllBytes(authenticationPath, refreshedSource);
        var confirmation = await service.SwitchAsync(adapter, targetProfile.ProfileId);
        Assert.Equal(AccountOperationStatus.ActiveProfileUpdateConfirmationRequired, confirmation.Status);
        writer.Enabled = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SwitchAsync(
                adapter,
                targetProfile.ProfileId,
                confirmation.ConfirmationFingerprint));

        Assert.Equal(refreshedSource, File.ReadAllBytes(authenticationPath));
        Assert.Equal(savedSource, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile!.ProfileId));
        var journal = Assert.IsType<SwitchTransactionJournal>(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Equal(SwitchTransactionKind.ProfileSwitch, journal.Kind);
        Assert.Equal(SwitchJournalPhase.Prepared, journal.Phase);
        Assert.Single(Directory.GetFiles(vaultPath, "recovery-*.vault", SearchOption.AllDirectories));

        var restartedVault = new AuthenticationProfileVault(vaultPath);
        var restartedService = CreateService(restartedVault, ProcessInspectionResult.Clear);
        var recovery = await restartedService.RecoverAsync(adapter);

        Assert.Equal(RecoveryStatus.RecoveredToSource, recovery.Status);
        Assert.Equal(refreshedSource, File.ReadAllBytes(authenticationPath));
        Assert.Equal(
            refreshedSource,
            restartedVault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile.ProfileId));
        Assert.Equal(
            sourceCapture.Profile.ProfileId,
            restartedVault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(restartedVault.GetPendingJournal(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vaultPath, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PreparedProfileSwitchKeepsEncryptedRecoveryForUnrecognizedLiveAuthentication()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSource = [1, 1, 2, 3];
        byte[] confirmedPreSwitch = [3, 2, 1, 1];
        byte[] target = [5, 8, 13, 21];
        byte[] unrecognizedLive = [34, 55, 89, 144];
        File.WriteAllBytes(authenticationPath, savedSource);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        var transactionId = Guid.NewGuid();
        var journal = new SwitchTransactionJournal
        {
            TransactionId = transactionId,
            Provider = AgentProvider.Codex,
            Kind = SwitchTransactionKind.ProfileSwitch,
            SourceProfileId = sourceCapture.Profile!.ProfileId,
            TargetProfileId = targetProfile.ProfileId,
            Phase = SwitchJournalPhase.Prepared,
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        vault.WriteTransactionRecoveryCredential(
            AgentProvider.Codex,
            transactionId,
            confirmedPreSwitch);
        vault.WritePendingJournal(journal);
        File.WriteAllBytes(authenticationPath, unrecognizedLive);

        var recovery = await service.RecoverAsync(adapter);

        Assert.Equal(RecoveryStatus.ManualInterventionRequired, recovery.Status);
        Assert.Equal(unrecognizedLive, File.ReadAllBytes(authenticationPath));
        Assert.Equal(savedSource, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile.ProfileId));
        Assert.Equal(journal, vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Single(Directory.GetFiles(vault.RootDirectory, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task InterruptedSavedSnapshotRestoreCompletesFromEncryptedRecoveryTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSnapshot = [6, 2, 6, 4];
        byte[] changedLiveLogin = [3, 3, 8, 3];
        File.WriteAllBytes(authenticationPath, savedSnapshot);

        var writer = new FailStateAndRollbackWriter(authenticationPath);
        var vaultPath = System.IO.Path.Combine(temporary.Path, "vault");
        var vault = new AuthenticationProfileVault(vaultPath, atomicWriter: writer);
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var capture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        File.WriteAllBytes(authenticationPath, changedLiveLogin);
        var confirmation = await service.SwitchAsync(adapter, capture.Profile!.ProfileId);
        writer.Enabled = true;

        var failed = await service.SwitchAsync(
            adapter,
            capture.Profile.ProfileId,
            confirmation.ConfirmationFingerprint);

        Assert.Equal(AccountOperationStatus.RecoveryRequired, failed.Status);
        Assert.Equal(savedSnapshot, File.ReadAllBytes(authenticationPath));
        Assert.NotNull(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Single(Directory.GetFiles(vaultPath, "recovery-*.vault", SearchOption.AllDirectories));

        var restartedVault = new AuthenticationProfileVault(vaultPath);
        var restartedService = CreateService(restartedVault, ProcessInspectionResult.Clear);
        var recovery = await restartedService.RecoverAsync(adapter);

        Assert.Equal(RecoveryStatus.CompletedTargetActivation, recovery.Status);
        Assert.Equal(savedSnapshot, File.ReadAllBytes(authenticationPath));
        Assert.Equal(capture.Profile.ProfileId, restartedVault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(restartedVault.GetPendingJournal(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vaultPath, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RestoreRecoveryCompletesWhenTargetIsLiveEvenIfRecoveryCredentialWasAlreadyCleaned()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSnapshot = [8, 6, 7, 5];
        File.WriteAllBytes(authenticationPath, savedSnapshot);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var capture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        vault.WritePendingJournal(new SwitchTransactionJournal
        {
            TransactionId = Guid.NewGuid(),
            Provider = AgentProvider.Codex,
            Kind = SwitchTransactionKind.SavedSnapshotRestore,
            SourceProfileId = capture.Profile!.ProfileId,
            TargetProfileId = capture.Profile.ProfileId,
            Phase = SwitchJournalPhase.TargetInstalled,
            StartedAtUtc = DateTimeOffset.UtcNow
        });

        var recovery = await service.RecoverAsync(adapter);

        Assert.Equal(RecoveryStatus.CompletedTargetActivation, recovery.Status);
        Assert.Equal(savedSnapshot, File.ReadAllBytes(authenticationPath));
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task LegacyPreparedProfileSwitchWithoutRecoveryBlobRestoresMissingAuthenticationFile()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [3, 1, 4, 1];
        byte[] target = [5, 9, 2, 6];
        File.WriteAllBytes(authenticationPath, source);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        var transactionId = Guid.NewGuid();
        vault.WritePendingJournal(new SwitchTransactionJournal
        {
            TransactionId = transactionId,
            Provider = AgentProvider.Codex,
            Kind = SwitchTransactionKind.ProfileSwitch,
            SourceProfileId = sourceCapture.Profile!.ProfileId,
            TargetProfileId = targetProfile.ProfileId,
            Phase = SwitchJournalPhase.Prepared,
            StartedAtUtc = DateTimeOffset.UtcNow
        });
        File.Delete(authenticationPath);
        var temporaryPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(authenticationPath)!,
            $".coding-agent-account-switcher.{System.IO.Path.GetFileName(authenticationPath)}.{transactionId:N}.tmp");
        var backupPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(authenticationPath)!,
            $".coding-agent-account-switcher.{System.IO.Path.GetFileName(authenticationPath)}.{transactionId:N}.backup");
        File.WriteAllBytes(temporaryPath, target);
        File.WriteAllBytes(backupPath, source);
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "recovery-*.vault", SearchOption.AllDirectories));

        var result = await service.RecoverAsync(adapter);

        Assert.Equal(RecoveryStatus.RecoveredToSource, result.Status);
        Assert.Equal(source, File.ReadAllBytes(authenticationPath));
        Assert.False(File.Exists(temporaryPath));
        Assert.False(File.Exists(backupPath));
        Assert.Equal(sourceCapture.Profile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CorruptedTargetProfileDoesNotModifyCurrentAuthenticationFile()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [5, 5, 5];
        File.WriteAllBytes(authenticationPath, source);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var target = vault.CreateProfile(AgentProvider.Codex, "Work", [6, 6, 6]);
        var blobPath = Directory.GetFiles(vault.RootDirectory, $"{target.ProfileId:N}.vault", SearchOption.AllDirectories).Single();
        File.WriteAllBytes(blobPath, [0xFF]);

        var result = await service.SwitchAsync(adapter, target.ProfileId);

        Assert.Equal(AccountOperationStatus.Failed, result.Status);
        Assert.Equal(source, File.ReadAllBytes(authenticationPath));
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task MissingCurrentAuthenticationFileDoesNotStartTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        File.WriteAllBytes(authenticationPath, [1, 2, 3]);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault, ProcessInspectionResult.Clear);
        await service.CaptureCurrentLoginAsync(adapter, "Personal");
        var target = vault.CreateProfile(AgentProvider.Codex, "Work", [4, 5, 6]);
        File.Delete(authenticationPath);

        var result = await service.SwitchAsync(adapter, target.ProfileId);

        Assert.Equal(AccountOperationStatus.AuthenticationFileMissing, result.Status);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.False(File.Exists(authenticationPath));
    }

    [Theory]
    [InlineData(true, ProcessInspectionStatus.Running, AccountOperationStatus.RecoveryRequired)]
    [InlineData(true, ProcessInspectionStatus.Unknown, AccountOperationStatus.RecoveryRequired)]
    [InlineData(false, ProcessInspectionStatus.Running, AccountOperationStatus.BlockedByRunningProcesses)]
    [InlineData(false, ProcessInspectionStatus.Unknown, AccountOperationStatus.ProcessInspectionUnknown)]
    public async Task IncompleteRecoveryBlocksEveryNewOperationWhenSecondProcessCheckIsUnsafe(
        bool captureOperation,
        ProcessInspectionStatus inspectionStatus,
        AccountOperationStatus expectedStatus)
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] source = [2, 7, 1, 8];
        byte[] target = [1, 6, 1, 8];
        File.WriteAllBytes(authenticationPath, source);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var clearService = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await clearService.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        var journal = new SwitchTransactionJournal
        {
            TransactionId = Guid.NewGuid(),
            Provider = AgentProvider.Codex,
            Kind = SwitchTransactionKind.ProfileSwitch,
            SourceProfileId = sourceCapture.Profile!.ProfileId,
            TargetProfileId = targetProfile.ProfileId,
            Phase = SwitchJournalPhase.Prepared,
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        vault.WritePendingJournal(journal);

        var unsafeInspection = new ProcessInspectionResult
        {
            Status = inspectionStatus,
            Processes = inspectionStatus == ProcessInspectionStatus.Running
                ? [new DetectedProcess(71, "codex")]
                : [],
            Issues = inspectionStatus == ProcessInspectionStatus.Unknown
                ? [new ProcessInspectionIssue("codex", "Synthetic inspection failure")]
                : []
        };
        var service = CreateService(
            vault,
            captureOperation
                ? new FixedProcessInspector(unsafeInspection)
                : new SequencedProcessInspector(ProcessInspectionResult.Clear, unsafeInspection));

        var actualStatus = captureOperation
            ? (await service.CaptureCurrentLoginAsync(adapter, "Unexpected")).Status
            : (await service.SwitchAsync(adapter, targetProfile.ProfileId)).Status;

        Assert.Equal(expectedStatus, actualStatus);
        Assert.Equal(source, File.ReadAllBytes(authenticationPath));
        Assert.Equal(2, vault.ListProfiles(AgentProvider.Codex).Count);
        Assert.Equal(journal, vault.GetPendingJournal(AgentProvider.Codex));
    }

    [Theory]
    [InlineData(false, ProcessInspectionStatus.Running, AccountOperationStatus.BlockedByRunningProcesses)]
    [InlineData(false, ProcessInspectionStatus.Unknown, AccountOperationStatus.ProcessInspectionUnknown)]
    [InlineData(true, ProcessInspectionStatus.Running, AccountOperationStatus.BlockedByRunningProcesses)]
    [InlineData(true, ProcessInspectionStatus.Unknown, AccountOperationStatus.ProcessInspectionUnknown)]
    public async Task ProcessStartingAfterPreparationBlocksAuthenticationWriteAndCleansPreparedState(
        bool restoreLastSelectedSnapshot,
        ProcessInspectionStatus inspectionStatus,
        AccountOperationStatus expectedStatus)
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSource = [1, 4, 2, 8];
        byte[] changedLiveLogin = [5, 7, 7, 2];
        byte[] target = [9, 9, 1, 3];
        File.WriteAllBytes(authenticationPath, savedSource);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var clearService = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await clearService.CaptureCurrentLoginAsync(adapter, "Personal");

        Guid targetProfileId;
        string? confirmationFingerprint = null;
        byte[] expectedLiveAuthentication;
        if (restoreLastSelectedSnapshot)
        {
            targetProfileId = sourceCapture.Profile!.ProfileId;
            File.WriteAllBytes(authenticationPath, changedLiveLogin);
            expectedLiveAuthentication = changedLiveLogin;
            var confirmation = await clearService.SwitchAsync(adapter, targetProfileId);
            confirmationFingerprint = confirmation.ConfirmationFingerprint;
            Assert.Equal(AccountOperationStatus.SavedSnapshotRestoreConfirmationRequired, confirmation.Status);
        }
        else
        {
            targetProfileId = vault.CreateProfile(AgentProvider.Codex, "Work", target).ProfileId;
            expectedLiveAuthentication = savedSource;
        }

        var unsafeInspection = new ProcessInspectionResult
        {
            Status = inspectionStatus,
            Processes = inspectionStatus == ProcessInspectionStatus.Running
                ? [new DetectedProcess(91, "codex")]
                : [],
            Issues = inspectionStatus == ProcessInspectionStatus.Unknown
                ? [new ProcessInspectionIssue("codex", "Synthetic inspection failure")]
                : []
        };
        var service = CreateService(
            vault,
            new SequencedProcessInspector(
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                unsafeInspection));

        var result = await service.SwitchAsync(adapter, targetProfileId, confirmationFingerprint);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.ConfirmationFingerprint);
        Assert.Equal(expectedLiveAuthentication, File.ReadAllBytes(authenticationPath));
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.Empty(Directory.GetFiles(vault.RootDirectory, "recovery-*.vault", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(ProcessInspectionStatus.Running, AccountOperationStatus.BlockedByRunningProcesses)]
    [InlineData(ProcessInspectionStatus.Unknown, AccountOperationStatus.ProcessInspectionUnknown)]
    public async Task UnsafeCommitCheckDoesNotUpdateConfirmedSourceSnapshot(
        ProcessInspectionStatus inspectionStatus,
        AccountOperationStatus expectedStatus)
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateCodexAdapter(temporary.Path, out var authenticationPath, out _);
        byte[] savedSource = [1, 2, 3, 4];
        byte[] refreshedSource = [4, 3, 2, 1];
        byte[] target = [9, 8, 7, 6];
        File.WriteAllBytes(authenticationPath, savedSource);
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var clearService = CreateService(vault, ProcessInspectionResult.Clear);
        var sourceCapture = await clearService.CaptureCurrentLoginAsync(adapter, "Personal");
        var targetProfile = vault.CreateProfile(AgentProvider.Codex, "Work", target);
        File.WriteAllBytes(authenticationPath, refreshedSource);
        var confirmation = await clearService.SwitchAsync(adapter, targetProfile.ProfileId);
        Assert.Equal(AccountOperationStatus.ActiveProfileUpdateConfirmationRequired, confirmation.Status);

        var unsafeInspection = new ProcessInspectionResult
        {
            Status = inspectionStatus,
            Processes = inspectionStatus == ProcessInspectionStatus.Running
                ? [new DetectedProcess(100, "codex")]
                : [],
            Issues = inspectionStatus == ProcessInspectionStatus.Unknown
                ? [new ProcessInspectionIssue("codex", "Synthetic inspection failure")]
                : []
        };
        var service = CreateService(
            vault,
            new SequencedProcessInspector(
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                unsafeInspection));

        var result = await service.SwitchAsync(
            adapter,
            targetProfile.ProfileId,
            confirmation.ConfirmationFingerprint);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(refreshedSource, File.ReadAllBytes(authenticationPath));
        Assert.Equal(savedSource, vault.LoadCredential(AgentProvider.Codex, sourceCapture.Profile!.ProfileId));
        Assert.Equal(sourceCapture.Profile.ProfileId, vault.GetActiveProfile(AgentProvider.Codex)!.ProfileId);
        Assert.Null(vault.GetPendingJournal(AgentProvider.Codex));
    }

    private static CodexAuthenticationAdapter CreateCodexAdapter(
        string root,
        out string authenticationPath,
        out string configurationPath)
    {
        var userProfile = System.IO.Path.Combine(root, "user");
        // Most transaction tests exercise the legacy single-file contract with
        // arbitrary binary fixtures. API-configuration behavior has dedicated
        // composite-snapshot tests below.
        var adapter = new CodexAuthenticationAdapter(
            userProfile,
            manageApiConfiguration: false);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        authenticationPath = adapter.AuthenticationFilePath;
        configurationPath = System.IO.Path.Combine(userProfile, ".codex", "config.toml");
        return adapter;
    }

    private static AccountSwitchService CreateService(
        AuthenticationProfileVault vault,
        ProcessInspectionResult inspection) => new(
        vault,
        new FixedProcessInspector(inspection),
        mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

    private static AccountSwitchService CreateService(
        AuthenticationProfileVault vault,
        IProcessInspector processInspector) => new(
        vault,
        processInspector,
        mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

    private sealed class SequencedProcessInspector : IProcessInspector
    {
        private readonly Queue<ProcessInspectionResult> _results;
        private ProcessInspectionResult _lastResult;

        public SequencedProcessInspector(params ProcessInspectionResult[] results)
        {
            if (results.Length == 0)
            {
                throw new ArgumentException("At least one inspection result is required.", nameof(results));
            }

            _results = new Queue<ProcessInspectionResult>(results);
            _lastResult = results[^1];
        }

        public ProcessInspectionResult Inspect(IAuthenticationAdapter adapter)
        {
            if (_results.TryDequeue(out var result))
            {
                _lastResult = result;
            }

            return _lastResult;
        }
    }

    private sealed class FailOnUseProcessInspector : IProcessInspector
    {
        public ProcessInspectionResult Inspect(IAuthenticationAdapter adapter) =>
            throw new InvalidOperationException("Capture must not inspect provider processes.");
    }

    private sealed class FailOnceActiveStateWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private bool _hasFailed;

        public bool Enabled { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            if (Enabled && !_hasFailed && System.IO.Path.GetFileName(destinationPath).StartsWith("active-"))
            {
                _hasFailed = true;
                throw new IOException("Injected active-state write failure.");
            }

            _inner.WriteAllBytes(destinationPath, contents, transactionId);
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }

    private sealed class ThrowAfterAuthenticationCommitWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private readonly string _authenticationPath;
        private bool _hasThrown;

        public ThrowAfterAuthenticationCommitWriter(string authenticationPath)
        {
            _authenticationPath = System.IO.Path.GetFullPath(authenticationPath);
        }

        public bool Enabled { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            _inner.WriteAllBytes(destinationPath, contents, transactionId);
            if (Enabled && !_hasThrown && System.IO.Path.GetFullPath(destinationPath) == _authenticationPath)
            {
                _hasThrown = true;
                throw new IOException("Injected post-commit authentication write failure.");
            }
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }

    private sealed class FailStateAndRollbackWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private readonly string _authenticationPath;
        private int _authenticationWrites;
        private bool _stateFailed;

        public FailStateAndRollbackWriter(string authenticationPath)
        {
            _authenticationPath = System.IO.Path.GetFullPath(authenticationPath);
        }

        public bool Enabled { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            if (Enabled && System.IO.Path.GetFullPath(destinationPath) == _authenticationPath)
            {
                _authenticationWrites++;
                if (_authenticationWrites == 2)
                {
                    throw new IOException("Injected rollback write failure.");
                }
            }

            if (Enabled && !_stateFailed && System.IO.Path.GetFileName(destinationPath).StartsWith("active-"))
            {
                _stateFailed = true;
                throw new IOException("Injected active-state write failure.");
            }

            _inner.WriteAllBytes(destinationPath, contents, transactionId);
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }

    private sealed class CancelAfterPreparedJournalCommitWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private bool _hasCancelled;

        public bool Enabled { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            _inner.WriteAllBytes(destinationPath, contents, transactionId);
            if (Enabled && !_hasCancelled &&
                System.IO.Path.GetFileName(destinationPath).StartsWith("journal-", StringComparison.Ordinal))
            {
                _hasCancelled = true;
                throw new OperationCanceledException("Injected interruption after the prepared journal commit.");
            }
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }
}
