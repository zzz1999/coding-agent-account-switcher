using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class ProfileManagementTests
{
    [Fact]
    public async Task RenameTrimsDisplayNameAndPreservesCiphertextAndTimestamps()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var created = vault.CreateProfile(AgentProvider.Codex, "Personal", [1, 2, 3]);
        var before = vault.MarkActivated(AgentProvider.Codex, created.ProfileId);
        var blobPath = FindProfileFile(temporary.Path, created.ProfileId, ".vault");
        var protectedBlobBefore = File.ReadAllBytes(blobPath);
        var service = CreateService(vault);

        var result = await service.RenameProfileAsync(
            AgentProvider.Codex,
            created.ProfileId,
            "  pErSoNaL  ");

        Assert.Equal(ProfileManagementStatus.Success, result.Status);
        var renamed = Assert.IsType<AuthenticationProfileMetadata>(result.Profile);
        Assert.Equal("pErSoNaL", renamed.DisplayName);
        Assert.Equal(before.ProfileId, renamed.ProfileId);
        Assert.Equal(before.Provider, renamed.Provider);
        Assert.Equal(before.CreatedAtUtc, renamed.CreatedAtUtc);
        Assert.Equal(before.CapturedAtUtc, renamed.CapturedAtUtc);
        Assert.Equal(before.LastActivatedAtUtc, renamed.LastActivatedAtUtc);
        Assert.Equal(renamed, vault.GetProfile(AgentProvider.Codex, created.ProfileId));
        Assert.Equal(protectedBlobBefore, File.ReadAllBytes(blobPath));

        var credential = vault.LoadCredential(AgentProvider.Codex, created.ProfileId);
        try
        {
            Assert.Equal([1, 2, 3], credential);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
            CryptographicOperations.ZeroMemory(protectedBlobBefore);
        }
    }

    [Fact]
    public async Task RenameRejectsTrimmedCaseInsensitiveConflictWithoutWritingMetadata()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var personal = vault.CreateProfile(AgentProvider.ClaudeCode, "Personal", [1, 2, 3]);
        var work = vault.CreateProfile(AgentProvider.ClaudeCode, "Work", [4, 5, 6]);
        var personalMetadataPath = FindProfileFile(
            temporary.Path,
            personal.ProfileId,
            ".metadata.json");
        var workMetadataPath = FindProfileFile(temporary.Path, work.ProfileId, ".metadata.json");
        var personalMetadataBefore = File.ReadAllBytes(personalMetadataPath);
        var workMetadataBefore = File.ReadAllBytes(workMetadataPath);
        var service = CreateService(vault);

        var result = await service.RenameProfileAsync(
            AgentProvider.ClaudeCode,
            work.ProfileId,
            "  pErSoNaL  ");

        Assert.Equal(ProfileManagementStatus.DisplayNameConflict, result.Status);
        Assert.Null(result.Profile);
        Assert.Equal(personalMetadataBefore, File.ReadAllBytes(personalMetadataPath));
        Assert.Equal(workMetadataBefore, File.ReadAllBytes(workMetadataPath));
        Assert.Equal(personal, vault.GetProfile(AgentProvider.ClaudeCode, personal.ProfileId));
        Assert.Equal(work, vault.GetProfile(AgentProvider.ClaudeCode, work.ProfileId));
    }

    [Fact]
    public async Task RenameIgnoresUnreadableSnapshotButStillReservesItsValidatedDisplayName()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var personal = vault.CreateProfile(AgentProvider.Codex, "Personal", [1, 2, 3]);
        var damaged = vault.CreateProfile(AgentProvider.Codex, "Work", [4, 5, 6]);
        File.WriteAllBytes(FindProfileFile(temporary.Path, damaged.ProfileId, ".vault"), [9, 8, 7]);
        var service = CreateService(vault);

        var conflict = await service.RenameProfileAsync(
            AgentProvider.Codex,
            personal.ProfileId,
            "work");
        var renamed = await service.RenameProfileAsync(
            AgentProvider.Codex,
            personal.ProfileId,
            "Private");

        Assert.Equal(ProfileManagementStatus.DisplayNameConflict, conflict.Status);
        Assert.Equal(ProfileManagementStatus.Success, renamed.Status);
        Assert.Equal("Private", renamed.Profile!.DisplayName);
        Assert.Equal("Work", vault.GetProfile(AgentProvider.Codex, damaged.ProfileId).DisplayName);
    }

    [Fact]
    public async Task DeleteInactiveProfileLeavesActiveSelectionAndProfileUntouched()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var active = vault.CaptureProfileAndSetActive(
            AgentProvider.OpenCode,
            "Personal",
            [1, 2, 3],
            profileToReplace: null);
        var inactive = vault.CreateProfile(AgentProvider.OpenCode, "Work", [4, 5, 6]);
        var activeStateBefore = Assert.IsType<ActiveProfileState>(
            vault.GetActiveProfile(AgentProvider.OpenCode));
        var activeMetadataBefore = vault.GetProfile(AgentProvider.OpenCode, active.ProfileId);
        var activeBlobPath = FindProfileFile(temporary.Path, active.ProfileId, ".vault");
        var activeBlobBefore = File.ReadAllBytes(activeBlobPath);
        var inactiveBlobPath = FindProfileFile(temporary.Path, inactive.ProfileId, ".vault");
        var service = CreateService(vault);

        var result = await service.DeleteProfileAsync(AgentProvider.OpenCode, inactive.ProfileId);

        Assert.Equal(ProfileManagementStatus.Success, result.Status);
        Assert.False(result.RemovedActiveSelection);
        Assert.Equal(activeStateBefore, vault.GetActiveProfile(AgentProvider.OpenCode));
        Assert.Equal(activeMetadataBefore, vault.GetProfile(AgentProvider.OpenCode, active.ProfileId));
        Assert.Equal(activeBlobBefore, File.ReadAllBytes(activeBlobPath));
        Assert.Throws<FileNotFoundException>(() =>
            vault.GetProfile(AgentProvider.OpenCode, inactive.ProfileId));
        Assert.False(File.Exists(inactiveBlobPath));
    }

    [Fact]
    public async Task DeleteActiveProfileRequiresConfirmationThenClearsActiveSelection()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var active = vault.CaptureProfileAndSetActive(
            AgentProvider.Codex,
            "Personal",
            [1, 2, 3],
            profileToReplace: null);
        var metadataPath = FindProfileFile(temporary.Path, active.ProfileId, ".metadata.json");
        var blobPath = FindProfileFile(temporary.Path, active.ProfileId, ".vault");
        var service = CreateService(vault);

        var confirmation = await service.DeleteProfileAsync(AgentProvider.Codex, active.ProfileId);

        Assert.Equal(
            ProfileManagementStatus.ActiveProfileConfirmationRequired,
            confirmation.Status);
        Assert.NotNull(vault.GetActiveProfile(AgentProvider.Codex));
        Assert.True(File.Exists(metadataPath));
        Assert.True(File.Exists(blobPath));

        var deleted = await service.DeleteProfileAsync(
            AgentProvider.Codex,
            active.ProfileId,
            confirmActiveSelectionRemoval: true);

        Assert.Equal(ProfileManagementStatus.Success, deleted.Status);
        Assert.True(deleted.RemovedActiveSelection);
        Assert.Null(vault.GetActiveProfile(AgentProvider.Codex));
        Assert.False(File.Exists(metadataPath));
        Assert.False(File.Exists(blobPath));
        Assert.Throws<FileNotFoundException>(() =>
            vault.GetProfile(AgentProvider.Codex, active.ProfileId));
    }

    [Fact]
    public async Task PendingSwitchJournalRejectsRenameAndDeleteWithoutMutation()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var source = vault.CaptureProfileAndSetActive(
            AgentProvider.ClaudeCode,
            "Personal",
            [1, 2, 3],
            profileToReplace: null);
        var target = vault.CreateProfile(AgentProvider.ClaudeCode, "Work", [4, 5, 6]);
        var journal = new SwitchTransactionJournal
        {
            TransactionId = Guid.NewGuid(),
            Provider = AgentProvider.ClaudeCode,
            Kind = SwitchTransactionKind.ProfileSwitch,
            SourceProfileId = source.ProfileId,
            TargetProfileId = target.ProfileId,
            Phase = SwitchJournalPhase.Prepared,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        vault.WritePendingJournal(journal);
        var sourceMetadataPath = FindProfileFile(
            temporary.Path,
            source.ProfileId,
            ".metadata.json");
        var targetMetadataPath = FindProfileFile(
            temporary.Path,
            target.ProfileId,
            ".metadata.json");
        var sourceMetadataBefore = File.ReadAllBytes(sourceMetadataPath);
        var targetMetadataBefore = File.ReadAllBytes(targetMetadataPath);
        var activeStateBefore = vault.GetActiveProfile(AgentProvider.ClaudeCode);
        var service = CreateService(vault);

        var rename = await service.RenameProfileAsync(
            AgentProvider.ClaudeCode,
            source.ProfileId,
            "Renamed");
        var delete = await service.DeleteProfileAsync(
            AgentProvider.ClaudeCode,
            target.ProfileId,
            confirmActiveSelectionRemoval: true);

        Assert.Equal(ProfileManagementStatus.RecoveryRequired, rename.Status);
        Assert.Equal(ProfileManagementStatus.RecoveryRequired, delete.Status);
        Assert.Equal(journal, vault.GetPendingJournal(AgentProvider.ClaudeCode));
        Assert.Equal(activeStateBefore, vault.GetActiveProfile(AgentProvider.ClaudeCode));
        Assert.Equal(sourceMetadataBefore, File.ReadAllBytes(sourceMetadataPath));
        Assert.Equal(targetMetadataBefore, File.ReadAllBytes(targetMetadataPath));
        Assert.Equal(source, vault.GetProfile(AgentProvider.ClaudeCode, source.ProfileId));
        Assert.Equal(target, vault.GetProfile(AgentProvider.ClaudeCode, target.ProfileId));
    }

    [Fact]
    public async Task RenameFailureAfterMetadataCommitRestoresExactPreviousMetadata()
    {
        using var temporary = new TemporaryDirectory();
        var writer = new CommitThenFailMetadataWriter();
        var vault = new AuthenticationProfileVault(temporary.Path, atomicWriter: writer);
        var profile = vault.CreateProfile(AgentProvider.Codex, "Personal", [1, 2, 3]);
        var metadataPath = FindProfileFile(temporary.Path, profile.ProfileId, ".metadata.json");
        var blobPath = FindProfileFile(temporary.Path, profile.ProfileId, ".vault");
        var metadataBefore = File.ReadAllBytes(metadataPath);
        var blobBefore = File.ReadAllBytes(blobPath);
        writer.FailNextMetadataWrite = true;
        var service = CreateService(vault);

        var result = await service.RenameProfileAsync(
            AgentProvider.Codex,
            profile.ProfileId,
            "Renamed");

        Assert.Equal(ProfileManagementStatus.Failed, result.Status);
        Assert.Equal(metadataBefore, File.ReadAllBytes(metadataPath));
        Assert.Equal(blobBefore, File.ReadAllBytes(blobPath));
        Assert.Equal(profile, vault.GetProfile(AgentProvider.Codex, profile.ProfileId));
    }

    [Fact]
    public async Task ActiveDeleteFailureAfterFinalDeleteRestoresProfileAndActiveStateExactly()
    {
        using var temporary = new TemporaryDirectory();
        var writer = new CommitThenFailDeleteWriter();
        var vault = new AuthenticationProfileVault(temporary.Path, atomicWriter: writer);
        var profile = vault.CaptureProfileAndSetActive(
            AgentProvider.OpenCode,
            "Personal",
            [1, 2, 3],
            profileToReplace: null);
        var metadataPath = FindProfileFile(temporary.Path, profile.ProfileId, ".metadata.json");
        var blobPath = FindProfileFile(temporary.Path, profile.ProfileId, ".vault");
        var activeStatePath = Path.Combine(temporary.Path, "state", "active-opencode.json");
        var metadataBefore = File.ReadAllBytes(metadataPath);
        var blobBefore = File.ReadAllBytes(blobPath);
        var activeStateBefore = File.ReadAllBytes(activeStatePath);
        writer.FailOnDeleteNumber = 3;
        var service = CreateService(vault);

        var result = await service.DeleteProfileAsync(
            AgentProvider.OpenCode,
            profile.ProfileId,
            confirmActiveSelectionRemoval: true);

        Assert.Equal(ProfileManagementStatus.Failed, result.Status);
        Assert.Equal(metadataBefore, File.ReadAllBytes(metadataPath));
        Assert.Equal(blobBefore, File.ReadAllBytes(blobPath));
        Assert.Equal(activeStateBefore, File.ReadAllBytes(activeStatePath));
        Assert.Equal(profile, vault.GetProfile(AgentProvider.OpenCode, profile.ProfileId));
        Assert.Equal(profile.ProfileId, vault.GetActiveProfile(AgentProvider.OpenCode)!.ProfileId);

        var credential = vault.LoadCredential(AgentProvider.OpenCode, profile.ProfileId);
        try
        {
            Assert.Equal([1, 2, 3], credential);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
        }
    }

    private static AccountSwitchService CreateService(AuthenticationProfileVault vault) => new(
        vault,
        new FixedProcessInspector(ProcessInspectionResult.Clear),
        mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

    private static string FindProfileFile(string root, Guid profileId, string suffix) =>
        Directory.GetFiles(root, $"{profileId:N}{suffix}", SearchOption.AllDirectories).Single();

    private sealed class CommitThenFailMetadataWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();

        public bool FailNextMetadataWrite { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            _inner.WriteAllBytes(destinationPath, contents, transactionId);
            if (FailNextMetadataWrite &&
                destinationPath.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
            {
                FailNextMetadataWrite = false;
                throw new IOException("Injected failure after metadata commit.");
            }
        }

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);
    }

    private sealed class CommitThenFailDeleteWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private int _deleteCount;

        public int FailOnDeleteNumber { get; set; } = int.MaxValue;

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null) =>
            _inner.WriteAllBytes(destinationPath, contents, transactionId);

        public void DeleteFile(string destinationPath)
        {
            _deleteCount++;
            _inner.DeleteFile(destinationPath);
            if (_deleteCount == FailOnDeleteNumber)
            {
                throw new IOException("Injected failure after file deletion.");
            }
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);
    }
}
