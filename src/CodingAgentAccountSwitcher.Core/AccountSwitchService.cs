using System.Diagnostics;
using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core;

public sealed class AccountSwitchService
{
    private readonly AuthenticationProfileVault _vault;
    private readonly IProcessInspector _processInspector;
    private readonly TimeSpan _mutexTimeout;
    private readonly string _mutexNamePrefix;

    public AccountSwitchService(
        AuthenticationProfileVault vault,
        IProcessInspector? processInspector = null,
        TimeSpan? mutexTimeout = null,
        string mutexNamePrefix = "CodingAgentAccountSwitcher")
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexNamePrefix);
        if (mutexNamePrefix.Contains('\\'))
        {
            throw new ArgumentException("The mutex prefix cannot contain a backslash.", nameof(mutexNamePrefix));
        }

        _vault = vault;
        _processInspector = processInspector ?? new SystemProcessInspector();
        _mutexTimeout = mutexTimeout ?? TimeSpan.FromSeconds(5);
        _mutexNamePrefix = mutexNamePrefix;
    }

    public Task<CaptureProfileResult> CaptureCurrentLoginAsync(
        IAuthenticationAdapter adapter,
        string displayName,
        Guid? profileToReplace = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        return Task.Run(
            () => CaptureCurrentLogin(adapter, displayName, profileToReplace, cancellationToken),
            cancellationToken);
    }

    public Task<CaptureProfileResult> CaptureCurrentLoginAsync(
        IAuthenticationAdapter adapter,
        string displayName,
        CancellationToken cancellationToken) =>
        CaptureCurrentLoginAsync(
            adapter,
            displayName,
            profileToReplace: null,
            cancellationToken: cancellationToken);

    public Task<SwitchProfileResult> SwitchAsync(
        IAuthenticationAdapter adapter,
        Guid targetProfileId,
        string? confirmationFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        return Task.Run(
            () => Switch(adapter, targetProfileId, confirmationFingerprint, cancellationToken),
            cancellationToken);
    }

    public Task<SwitchProfileResult> SwitchAsync(
        IAuthenticationAdapter adapter,
        Guid targetProfileId,
        CancellationToken cancellationToken) =>
        SwitchAsync(
            adapter,
            targetProfileId,
            confirmationFingerprint: null,
            cancellationToken: cancellationToken);

    public Task<RecoveryResult> RecoverAsync(
        IAuthenticationAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        return Task.Run(() => Recover(adapter, cancellationToken), cancellationToken);
    }

    public Task<ProfileManagementResult> RenameProfileAsync(
        AgentProvider provider,
        Guid profileId,
        string displayName,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => RenameProfile(provider, profileId, displayName, cancellationToken),
            cancellationToken);

    public Task<ProfileManagementResult> DeleteProfileAsync(
        AgentProvider provider,
        Guid profileId,
        bool confirmActiveSelectionRemoval = false,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => DeleteProfile(
                provider,
                profileId,
                confirmActiveSelectionRemoval,
                cancellationToken),
            cancellationToken);

    private ProfileManagementResult RenameProfile(
        AgentProvider provider,
        Guid profileId,
        string displayName,
        CancellationToken cancellationToken)
    {
        using var operationLock = TryAcquireMutex(provider, cancellationToken);
        if (operationLock is null)
        {
            return new ProfileManagementResult { Status = ProfileManagementStatus.LockUnavailable };
        }

        try
        {
            if (_vault.GetPendingJournal(provider) is not null)
            {
                return new ProfileManagementResult { Status = ProfileManagementStatus.RecoveryRequired };
            }

            cancellationToken.ThrowIfCancellationRequested();
            var trimmedDisplayName = displayName.Trim();
            if (_vault.ListProfileMetadataWithIssues(provider).Profiles.Any(profile =>
                    profile.ProfileId != profileId &&
                    string.Equals(
                        profile.DisplayName,
                        trimmedDisplayName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return new ProfileManagementResult { Status = ProfileManagementStatus.DisplayNameConflict };
            }

            var profile = _vault.RenameProfile(provider, profileId, trimmedDisplayName);
            return new ProfileManagementResult
            {
                Status = ProfileManagementStatus.Success,
                Profile = profile,
            };
        }
        catch (FileNotFoundException)
        {
            return new ProfileManagementResult { Status = ProfileManagementStatus.ProfileNotFound };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProfileManagementResult
            {
                Status = ProfileManagementStatus.Failed,
                ErrorMessage = exception.Message,
            };
        }
    }

    private ProfileManagementResult DeleteProfile(
        AgentProvider provider,
        Guid profileId,
        bool confirmActiveSelectionRemoval,
        CancellationToken cancellationToken)
    {
        using var operationLock = TryAcquireMutex(provider, cancellationToken);
        if (operationLock is null)
        {
            return new ProfileManagementResult { Status = ProfileManagementStatus.LockUnavailable };
        }

        try
        {
            if (_vault.GetPendingJournal(provider) is not null)
            {
                return new ProfileManagementResult { Status = ProfileManagementStatus.RecoveryRequired };
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_vault.GetActiveProfile(provider)?.ProfileId == profileId &&
                !confirmActiveSelectionRemoval)
            {
                return new ProfileManagementResult
                {
                    Status = ProfileManagementStatus.ActiveProfileConfirmationRequired,
                };
            }

            var removedActiveSelection = _vault.DeleteProfile(provider, profileId);
            return new ProfileManagementResult
            {
                Status = ProfileManagementStatus.Success,
                RemovedActiveSelection = removedActiveSelection,
            };
        }
        catch (FileNotFoundException)
        {
            return new ProfileManagementResult { Status = ProfileManagementStatus.ProfileNotFound };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProfileManagementResult
            {
                Status = ProfileManagementStatus.Failed,
                ErrorMessage = exception.Message,
            };
        }
    }

    private CaptureProfileResult CaptureCurrentLogin(
        IAuthenticationAdapter adapter,
        string displayName,
        Guid? profileToReplace,
        CancellationToken cancellationToken)
    {
        using var operationLock = TryAcquireMutex(adapter.Provider, cancellationToken);
        if (operationLock is null)
        {
            return new CaptureProfileResult { Status = AccountOperationStatus.LockUnavailable };
        }

        var inspection = ProcessInspectionResult.Clear;
        try
        {
            // Saving is read-only with respect to provider files, so it remains
            // available while the provider is running. Never perform recovery
            // writes as a side effect of a capture request.
            if (_vault.GetPendingJournal(adapter.Provider) is not null)
            {
                return new CaptureProfileResult
                {
                    Status = AccountOperationStatus.RecoveryRequired,
                    ProcessInspection = inspection,
                    ErrorMessage = "Finish the pending account-switch recovery before saving another profile."
                };
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new CaptureProfileResult
            {
                Status = AccountOperationStatus.RecoveryRequired,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }

        byte[]? currentCredential = null;
        var authenticationFileRead = false;
        try
        {
            var trimmedDisplayName = displayName.Trim();
            var matchingProfile = _vault.ListProfilesWithIssues(adapter.Provider).Profiles
                .FirstOrDefault(profile => string.Equals(
                    profile.DisplayName,
                    trimmedDisplayName,
                    StringComparison.OrdinalIgnoreCase));
            if (!profileToReplace.HasValue && matchingProfile is not null)
            {
                return new CaptureProfileResult
                {
                    Status = AccountOperationStatus.DisplayNameConflict,
                    Profile = matchingProfile,
                    ProcessInspection = inspection
                };
            }

            currentCredential = adapter.ReadSnapshot();
            authenticationFileRead = true;

            if (profileToReplace.HasValue)
            {
                var existingProfile = _vault.GetProfile(adapter.Provider, profileToReplace.Value);
                if (!string.Equals(existingProfile.DisplayName, trimmedDisplayName, StringComparison.Ordinal) ||
                    (matchingProfile is not null && matchingProfile.ProfileId != existingProfile.ProfileId))
                {
                    return new CaptureProfileResult
                    {
                        Status = AccountOperationStatus.DisplayNameConflict,
                        Profile = matchingProfile,
                        ProcessInspection = inspection
                    };
                }
            }

            var profile = _vault.CaptureProfileAndSetActive(
                adapter.Provider,
                trimmedDisplayName,
                currentCredential,
                profileToReplace);

            return new CaptureProfileResult
            {
                Status = AccountOperationStatus.Success,
                Profile = profile,
                ProcessInspection = inspection
            };
        }
        catch (FileNotFoundException) when (!authenticationFileRead)
        {
            return new CaptureProfileResult
            {
                Status = AccountOperationStatus.AuthenticationFileMissing,
                ProcessInspection = inspection
            };
        }
        catch (FileNotFoundException exception)
        {
            return new CaptureProfileResult
            {
                Status = AccountOperationStatus.ProfileNotFound,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        catch (InvalidDataException exception) when (currentCredential is null)
        {
            return new CaptureProfileResult
            {
                Status = AccountOperationStatus.AuthenticationFileEmpty,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new CaptureProfileResult
            {
                Status = AccountOperationStatus.Failed,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        finally
        {
            if (currentCredential is not null)
            {
                CryptographicOperations.ZeroMemory(currentCredential);
            }
        }
    }

    private SwitchProfileResult Switch(
        IAuthenticationAdapter adapter,
        Guid targetProfileId,
        string? confirmationFingerprint,
        CancellationToken cancellationToken)
    {
        using var operationLock = TryAcquireMutex(adapter.Provider, cancellationToken);
        if (operationLock is null)
        {
            return new SwitchProfileResult
            {
                Status = AccountOperationStatus.LockUnavailable,
                TargetProfileId = targetProfileId
            };
        }

        var inspection = _processInspector.Inspect(adapter);
        if (inspection.Status != ProcessInspectionStatus.Clear)
        {
            return new SwitchProfileResult
            {
                Status = inspection.Status == ProcessInspectionStatus.Running
                    ? AccountOperationStatus.BlockedByRunningProcesses
                    : AccountOperationStatus.ProcessInspectionUnknown,
                TargetProfileId = targetProfileId,
                ProcessInspection = inspection
            };
        }

        var recovery = RecoverCore(adapter, inspection);
        if (!RecoveryPermitsNewOperation(recovery.Status))
        {
            return new SwitchProfileResult
            {
                Status = MapRecoveryStatus(recovery.Status),
                TargetProfileId = targetProfileId,
                ProcessInspection = recovery.ProcessInspection,
                ErrorMessage = recovery.ErrorMessage
            };
        }

        ActiveProfileState? activeState;
        try
        {
            activeState = _vault.GetActiveProfile(adapter.Provider);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return FailedSwitch(targetProfileId, inspection, exception.Message);
        }

        if (activeState is null)
        {
            return new SwitchProfileResult
            {
                Status = AccountOperationStatus.RecoveryRequired,
                TargetProfileId = targetProfileId,
                ProcessInspection = inspection,
                ErrorMessage = "Capture the currently active login before switching profiles."
            };
        }

        var isSavedSnapshotRestore = activeState.ProfileId == targetProfileId;
        byte[]? targetCredential = null;
        byte[]? currentCredential = null;
        byte[]? sourceCredential = null;
        byte[]? latestCredential = null;
        SwitchTransactionJournal? journal = null;
        var journalWritten = false;
        var recoveryCredentialWritten = false;
        var sourceSnapshotNeedsUpgrade = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetCredential = _vault.LoadCredential(adapter.Provider, targetProfileId);
            currentCredential = adapter.ReadSnapshot();
            if (!isSavedSnapshotRestore)
            {
                sourceCredential = _vault.LoadCredential(adapter.Provider, activeState.ProfileId);
                sourceSnapshotNeedsUpgrade = adapter.IsLegacySnapshot(sourceCredential);
            }

            var activeProfileChanged = !adapter.SnapshotsEqual(
                currentCredential,
                isSavedSnapshotRestore ? targetCredential : sourceCredential!);
            if (!activeProfileChanged && isSavedSnapshotRestore)
            {
                if (adapter.IsLegacySnapshot(targetCredential))
                {
                    _vault.UpdateCredential(adapter.Provider, targetProfileId, currentCredential);
                }
                return new SwitchProfileResult
                {
                    Status = AccountOperationStatus.AlreadyActive,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = inspection
                };
            }

            if (activeProfileChanged && !FingerprintMatches(currentCredential, confirmationFingerprint))
            {
                return new SwitchProfileResult
                {
                    Status = isSavedSnapshotRestore
                        ? AccountOperationStatus.SavedSnapshotRestoreConfirmationRequired
                        : AccountOperationStatus.ActiveProfileUpdateConfirmationRequired,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = inspection,
                    ConfirmationFingerprint = CreateFingerprint(currentCredential)
                };
            }

            var finalInspection = _processInspector.Inspect(adapter);
            if (finalInspection.Status != ProcessInspectionStatus.Clear)
            {
                return new SwitchProfileResult
                {
                    Status = finalInspection.Status == ProcessInspectionStatus.Running
                        ? AccountOperationStatus.BlockedByRunningProcesses
                        : AccountOperationStatus.ProcessInspectionUnknown,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = finalInspection
                };
            }

            // Bind a confirmation to the exact bytes that were inspected. If anything changed
            // while the confirmation dialog was open, require a fresh confirmation.
            latestCredential = adapter.ReadSnapshot();
            if (!adapter.SnapshotsEqual(currentCredential, latestCredential))
            {
                CryptographicOperations.ZeroMemory(currentCredential);
                currentCredential = latestCredential;
                latestCredential = null;
                activeProfileChanged = !adapter.SnapshotsEqual(
                    currentCredential,
                    isSavedSnapshotRestore ? targetCredential : sourceCredential!);

                if (!activeProfileChanged && isSavedSnapshotRestore)
                {
                    if (adapter.IsLegacySnapshot(targetCredential))
                    {
                        _vault.UpdateCredential(adapter.Provider, targetProfileId, currentCredential);
                    }
                    return new SwitchProfileResult
                    {
                        Status = AccountOperationStatus.AlreadyActive,
                        SourceProfileId = activeState.ProfileId,
                        TargetProfileId = targetProfileId,
                        ProcessInspection = inspection
                    };
                }

                if (activeProfileChanged && !FingerprintMatches(currentCredential, confirmationFingerprint))
                {
                    return new SwitchProfileResult
                    {
                        Status = isSavedSnapshotRestore
                            ? AccountOperationStatus.SavedSnapshotRestoreConfirmationRequired
                            : AccountOperationStatus.ActiveProfileUpdateConfirmationRequired,
                        SourceProfileId = activeState.ProfileId,
                        TargetProfileId = targetProfileId,
                        ProcessInspection = inspection,
                        ConfirmationFingerprint = CreateFingerprint(currentCredential)
                    };
                }
            }

            var writeInspection = _processInspector.Inspect(adapter);
            if (writeInspection.Status != ProcessInspectionStatus.Clear)
            {
                return new SwitchProfileResult
                {
                    Status = writeInspection.Status == ProcessInspectionStatus.Running
                        ? AccountOperationStatus.BlockedByRunningProcesses
                        : AccountOperationStatus.ProcessInspectionUnknown,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = writeInspection
                };
            }

            journal = new SwitchTransactionJournal
            {
                TransactionId = Guid.NewGuid(),
                Provider = adapter.Provider,
                Kind = isSavedSnapshotRestore
                    ? SwitchTransactionKind.SavedSnapshotRestore
                    : SwitchTransactionKind.ProfileSwitch,
                SourceProfileId = activeState.ProfileId,
                TargetProfileId = targetProfileId,
                Phase = SwitchJournalPhase.Prepared,
                StartedAtUtc = _vault.TimeProvider.GetUtcNow()
            };

            if (isSavedSnapshotRestore || activeProfileChanged || adapter.ManagedFilePaths.Count > 1)
            {
                // Preserve the exact confirmed pre-switch bytes before the journal is
                // made durable. Composite adapters always preserve it because a legacy
                // authentication-only source lacks the config required for exact rollback.
                _vault.WriteTransactionRecoveryCredential(
                    adapter.Provider,
                    journal.TransactionId,
                    currentCredential);
                recoveryCredentialWritten = true;
            }
            _vault.WritePendingJournal(journal);
            journalWritten = true;

            var commitInspection = _processInspector.Inspect(adapter);
            if (commitInspection.Status != ProcessInspectionStatus.Clear)
            {
                try
                {
                    adapter.DeleteOwnedTransactionFiles(
                        _vault.AtomicWriter,
                        journal.TransactionId);
                    _vault.DeletePendingJournal(adapter.Provider);
                    if (recoveryCredentialWritten)
                    {
                        TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                    }
                }
                catch (Exception cleanupException) when (cleanupException is not OperationCanceledException)
                {
                    return new SwitchProfileResult
                    {
                        Status = AccountOperationStatus.RecoveryRequired,
                        SourceProfileId = activeState.ProfileId,
                        TargetProfileId = targetProfileId,
                        ProcessInspection = commitInspection,
                        ErrorMessage = $"The prepared switch could not be cancelled safely: {cleanupException.Message}"
                    };
                }

                return new SwitchProfileResult
                {
                    Status = commitInspection.Status == ProcessInspectionStatus.Running
                        ? AccountOperationStatus.BlockedByRunningProcesses
                        : AccountOperationStatus.ProcessInspectionUnknown,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = commitInspection
                };
            }

            if (!isSavedSnapshotRestore && (activeProfileChanged || sourceSnapshotNeedsUpgrade))
            {
                // Persist a confirmed token refresh only after the last process-safety
                // check has authorized the transaction to commit. This also upgrades
                // authentication-only profiles to the composite snapshot schema.
                _vault.UpdateCredential(adapter.Provider, activeState.ProfileId, currentCredential);
            }

            cancellationToken.ThrowIfCancellationRequested();
            adapter.WriteSnapshot(
                _vault.AtomicWriter,
                targetCredential,
                journal.TransactionId);
            _vault.WritePendingJournal(journal with { Phase = SwitchJournalPhase.TargetInstalled });

            _vault.WriteActiveProfile(new ActiveProfileState
            {
                Provider = adapter.Provider,
                ProfileId = targetProfileId,
                ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
            });
            _vault.MarkActivated(adapter.Provider, targetProfileId);
            adapter.DeleteOwnedTransactionFiles(
                _vault.AtomicWriter,
                journal.TransactionId);
            if (recoveryCredentialWritten)
            {
                _vault.DeletePendingJournal(adapter.Provider);
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
            }
            else
            {
                _vault.DeletePendingJournal(adapter.Provider);
            }

            return new SwitchProfileResult
            {
                Status = AccountOperationStatus.Success,
                SourceProfileId = activeState.ProfileId,
                TargetProfileId = targetProfileId,
                ProcessInspection = inspection
            };
        }
        catch (FileNotFoundException exception) when (!journalWritten)
        {
            if (recoveryCredentialWritten && journal is not null)
            {
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
            }

            return new SwitchProfileResult
            {
                Status = targetCredential is null
                    ? AccountOperationStatus.ProfileNotFound
                    : currentCredential is null
                        ? AccountOperationStatus.AuthenticationFileMissing
                        : AccountOperationStatus.Failed,
                SourceProfileId = activeState.ProfileId,
                TargetProfileId = targetProfileId,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        catch (InvalidDataException exception) when (!journalWritten && targetCredential is not null && currentCredential is null)
        {
            if (recoveryCredentialWritten && journal is not null)
            {
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
            }

            return new SwitchProfileResult
            {
                Status = AccountOperationStatus.AuthenticationFileEmpty,
                SourceProfileId = activeState.ProfileId,
                TargetProfileId = targetProfileId,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!journalWritten)
            {
                if (recoveryCredentialWritten && journal is not null)
                {
                    TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                }

                return FailedSwitch(targetProfileId, inspection, exception.Message, activeState.ProfileId);
            }

            var rollbackInspection = _processInspector.Inspect(adapter);
            if (rollbackInspection.Status != ProcessInspectionStatus.Clear)
            {
                // Keep the journal and any transaction-owned files intact. Recovery can
                // determine which side is live once the provider is safely stopped.
                return new SwitchProfileResult
                {
                    Status = AccountOperationStatus.RecoveryRequired,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = rollbackInspection,
                    ErrorMessage = "Switch failed, but rollback was deferred because provider process safety could not be confirmed."
                };
            }

            try
            {
                if (currentCredential is not null && journal is not null)
                {
                    adapter.DeleteOwnedTransactionFiles(
                        _vault.AtomicWriter,
                        journal.TransactionId);
                    adapter.WriteSnapshot(
                        _vault.AtomicWriter,
                        currentCredential,
                        journal.TransactionId);
                }

                _vault.WriteActiveProfile(activeState);
                if (journal is not null)
                {
                    adapter.DeleteOwnedTransactionFiles(
                        _vault.AtomicWriter,
                        journal.TransactionId);
                }

                _vault.DeletePendingJournal(adapter.Provider);
                if (recoveryCredentialWritten && journal is not null)
                {
                    TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                }

                return new SwitchProfileResult
                {
                    Status = AccountOperationStatus.Failed,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    RolledBack = true,
                    ProcessInspection = inspection,
                    ErrorMessage = exception.Message
                };
            }
            catch (Exception rollbackException) when (rollbackException is not OperationCanceledException)
            {
                return new SwitchProfileResult
                {
                    Status = AccountOperationStatus.RecoveryRequired,
                    SourceProfileId = activeState.ProfileId,
                    TargetProfileId = targetProfileId,
                    ProcessInspection = inspection,
                    ErrorMessage = $"Switch failed and rollback could not complete: {rollbackException.Message}"
                };
            }
        }
        finally
        {
            if (targetCredential is not null)
            {
                CryptographicOperations.ZeroMemory(targetCredential);
            }

            if (currentCredential is not null)
            {
                CryptographicOperations.ZeroMemory(currentCredential);
            }

            if (sourceCredential is not null)
            {
                CryptographicOperations.ZeroMemory(sourceCredential);
            }

            if (latestCredential is not null)
            {
                CryptographicOperations.ZeroMemory(latestCredential);
            }
        }
    }

    private RecoveryResult Recover(IAuthenticationAdapter adapter, CancellationToken cancellationToken)
    {
        using var operationLock = TryAcquireMutex(adapter.Provider, cancellationToken);
        if (operationLock is null)
        {
            return new RecoveryResult { Status = RecoveryStatus.LockUnavailable };
        }

        var inspection = _processInspector.Inspect(adapter);
        if (inspection.Status != ProcessInspectionStatus.Clear)
        {
            return new RecoveryResult
            {
                Status = inspection.Status == ProcessInspectionStatus.Running
                    ? RecoveryStatus.BlockedByRunningProcesses
                    : RecoveryStatus.ProcessInspectionUnknown,
                ProcessInspection = inspection
            };
        }

        return RecoverCore(adapter, inspection);
    }

    private RecoveryResult RecoverCore(IAuthenticationAdapter adapter, ProcessInspectionResult inspection)
    {
        SwitchTransactionJournal? journal;
        try
        {
            journal = _vault.GetPendingJournal(adapter.Provider);
        }
        catch (Exception exception)
        {
            return new RecoveryResult
            {
                Status = RecoveryStatus.ManualInterventionRequired,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }

        if (journal is null)
        {
            return new RecoveryResult
            {
                Status = RecoveryStatus.NoPendingTransaction,
                ProcessInspection = inspection
            };
        }

        if (journal.Kind == SwitchTransactionKind.SavedSnapshotRestore)
        {
            return RecoverSavedSnapshotRestore(adapter, inspection, journal);
        }

        byte[]? currentCredential = null;
        byte[]? sourceCredential = null;
        byte[]? targetCredential = null;
        var sourceCredentialCameFromRecovery = false;
        try
        {
            try
            {
                // New journals with a confirmed refreshed source preserve those exact
                // bytes in a transaction-only DPAPI blob. A missing blob identifies a
                // legacy journal and intentionally falls back to the saved source.
                sourceCredential = _vault.LoadTransactionRecoveryCredential(
                    adapter.Provider,
                    journal.TransactionId);
                sourceCredentialCameFromRecovery = true;
            }
            catch (FileNotFoundException)
            {
                sourceCredential = _vault.LoadCredential(adapter.Provider, journal.SourceProfileId);
            }

            try
            {
                currentCredential = adapter.ReadSnapshot(allowIncomplete: true);
            }
            catch (FileNotFoundException)
            {
                var missingFileInspection = _processInspector.Inspect(adapter);
                if (missingFileInspection.Status != ProcessInspectionStatus.Clear)
                {
                    return new RecoveryResult
                    {
                        Status = missingFileInspection.Status == ProcessInspectionStatus.Running
                            ? RecoveryStatus.BlockedByRunningProcesses
                            : RecoveryStatus.ProcessInspectionUnknown,
                        ProcessInspection = missingFileInspection
                    };
                }

                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                adapter.WriteSnapshot(
                    _vault.AtomicWriter,
                    sourceCredential,
                    journal.TransactionId);
                CompleteProfileSwitchSourceRecovery(
                    adapter,
                    journal,
                    sourceCredential,
                    sourceCredentialCameFromRecovery);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            var finalInspection = _processInspector.Inspect(adapter);
            if (finalInspection.Status != ProcessInspectionStatus.Clear)
            {
                return new RecoveryResult
                {
                    Status = finalInspection.Status == ProcessInspectionStatus.Running
                        ? RecoveryStatus.BlockedByRunningProcesses
                        : RecoveryStatus.ProcessInspectionUnknown,
                    ProcessInspection = finalInspection
                };
            }

            if (adapter.SnapshotsEqual(currentCredential, sourceCredential))
            {
                CompleteProfileSwitchSourceRecovery(
                    adapter,
                    journal,
                    sourceCredential,
                    sourceCredentialCameFromRecovery);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            targetCredential = _vault.LoadCredential(adapter.Provider, journal.TargetProfileId);
            if (adapter.SnapshotsEqual(currentCredential, targetCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.TargetProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.MarkActivated(adapter.Provider, journal.TargetProfileId);
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                if (sourceCredentialCameFromRecovery)
                {
                    TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                }
                return new RecoveryResult
                {
                    Status = RecoveryStatus.CompletedTargetActivation,
                    ActiveProfileId = journal.TargetProfileId,
                    ProcessInspection = inspection
                };
            }

            if (journal.Phase == SwitchJournalPhase.Prepared &&
                adapter.ManagedFilePaths.Count > 1 &&
                adapter.IsRecognizedPartialSnapshot(sourceCredential, targetCredential, currentCredential))
            {
                // A composite snapshot can span more than one file. A crash between
                // those atomic file replacements yields a deliberate partial state;
                // the durable source snapshot is therefore authoritative in Prepared.
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                adapter.WriteSnapshot(
                    _vault.AtomicWriter,
                    sourceCredential,
                    journal.TransactionId);
                CompleteProfileSwitchSourceRecovery(
                    adapter,
                    journal,
                    sourceCredential,
                    sourceCredentialCameFromRecovery);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            adapter.DeleteOwnedTransactionFiles(
                _vault.AtomicWriter,
                journal.TransactionId);
            return new RecoveryResult
            {
                Status = RecoveryStatus.ManualInterventionRequired,
                ProcessInspection = inspection,
                ErrorMessage = "The managed account files match neither side of the interrupted switch."
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new RecoveryResult
            {
                Status = RecoveryStatus.ManualInterventionRequired,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        finally
        {
            ZeroIfPresent(currentCredential);
            ZeroIfPresent(sourceCredential);
            ZeroIfPresent(targetCredential);
        }
    }

    private void CompleteProfileSwitchSourceRecovery(
        IAuthenticationAdapter adapter,
        SwitchTransactionJournal journal,
        byte[] sourceCredential,
        bool sourceCredentialCameFromRecovery)
    {
        if (sourceCredentialCameFromRecovery)
        {
            // The live login was confirmed before the interrupted switch, so make the
            // source snapshot agree with the exact bytes selected during recovery.
            _vault.UpdateCredential(adapter.Provider, journal.SourceProfileId, sourceCredential);
        }

        _vault.WriteActiveProfile(new ActiveProfileState
        {
            Provider = adapter.Provider,
            ProfileId = journal.SourceProfileId,
            ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
        });
        adapter.DeleteOwnedTransactionFiles(
            _vault.AtomicWriter,
            journal.TransactionId);
        _vault.DeletePendingJournal(adapter.Provider);
        if (sourceCredentialCameFromRecovery)
        {
            // Delete recovery evidence only after the journal no longer advertises an
            // incomplete transaction. Failure leaves an encrypted, harmless orphan.
            TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
        }
    }

    private RecoveryResult RecoverSavedSnapshotRestore(
        IAuthenticationAdapter adapter,
        ProcessInspectionResult inspection,
        SwitchTransactionJournal journal)
    {
        byte[]? currentCredential = null;
        byte[]? recoveryCredential = null;
        byte[]? targetCredential = null;
        try
        {
            targetCredential = _vault.LoadCredential(adapter.Provider, journal.TargetProfileId);

            try
            {
                currentCredential = adapter.ReadSnapshot(allowIncomplete: true);
            }
            catch (FileNotFoundException)
            {
                var missingFileInspection = _processInspector.Inspect(adapter);
                if (missingFileInspection.Status != ProcessInspectionStatus.Clear)
                {
                    return new RecoveryResult
                    {
                        Status = missingFileInspection.Status == ProcessInspectionStatus.Running
                            ? RecoveryStatus.BlockedByRunningProcesses
                            : RecoveryStatus.ProcessInspectionUnknown,
                        ProcessInspection = missingFileInspection
                    };
                }

                recoveryCredential = _vault.LoadTransactionRecoveryCredential(
                    adapter.Provider,
                    journal.TransactionId);
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                adapter.WriteSnapshot(
                    _vault.AtomicWriter,
                    recoveryCredential,
                    journal.TransactionId);
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            var finalInspection = _processInspector.Inspect(adapter);
            if (finalInspection.Status != ProcessInspectionStatus.Clear)
            {
                return new RecoveryResult
                {
                    Status = finalInspection.Status == ProcessInspectionStatus.Running
                        ? RecoveryStatus.BlockedByRunningProcesses
                        : RecoveryStatus.ProcessInspectionUnknown,
                    ProcessInspection = finalInspection
                };
            }

            if (adapter.SnapshotsEqual(currentCredential, targetCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.TargetProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.MarkActivated(adapter.Provider, journal.TargetProfileId);
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.CompletedTargetActivation,
                    ActiveProfileId = journal.TargetProfileId,
                    ProcessInspection = inspection
                };
            }

            recoveryCredential = _vault.LoadTransactionRecoveryCredential(
                adapter.Provider,
                journal.TransactionId);
            if (adapter.SnapshotsEqual(currentCredential, recoveryCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            // The encrypted pre-restore bytes remain available for manual recovery, so exact
            // transaction-owned plaintext staging files can be removed safely.
            if (journal.Phase == SwitchJournalPhase.Prepared &&
                adapter.ManagedFilePaths.Count > 1 &&
                adapter.IsRecognizedPartialSnapshot(recoveryCredential, targetCredential, currentCredential))
            {
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                adapter.WriteSnapshot(
                    _vault.AtomicWriter,
                    recoveryCredential,
                    journal.TransactionId);
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                adapter.DeleteOwnedTransactionFiles(
                    _vault.AtomicWriter,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                TryDeleteTransactionRecoveryCredential(adapter.Provider, journal.TransactionId);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            adapter.DeleteOwnedTransactionFiles(
                _vault.AtomicWriter,
                journal.TransactionId);
            return new RecoveryResult
            {
                Status = RecoveryStatus.ManualInterventionRequired,
                ProcessInspection = inspection,
                ErrorMessage = "The authentication file changed after the saved-snapshot restore was interrupted."
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new RecoveryResult
            {
                Status = RecoveryStatus.ManualInterventionRequired,
                ProcessInspection = inspection,
                ErrorMessage = exception.Message
            };
        }
        finally
        {
            ZeroIfPresent(currentCredential);
            ZeroIfPresent(recoveryCredential);
            ZeroIfPresent(targetCredential);
        }
    }

    private IDisposable? TryAcquireMutex(AgentProvider provider, CancellationToken cancellationToken)
    {
        var name = $"Local\\{_mutexNamePrefix}.{provider.ToStorageKey()}.AuthenticationSwitch";
        var mutex = new Mutex(initiallyOwned: false, name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (stopwatch.Elapsed < _mutexTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (mutex.WaitOne(TimeSpan.FromMilliseconds(100)))
                    {
                        return new MutexLease(mutex);
                    }
                }
                catch (AbandonedMutexException)
                {
                    return new MutexLease(mutex);
                }
            }

            mutex.Dispose();
            return null;
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private static string CreateFingerprint(byte[] credential)
    {
        var hash = SHA256.HashData(credential);
        try
        {
            return Convert.ToBase64String(hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    private static bool FingerprintMatches(byte[] credential, string? expectedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(expectedFingerprint))
        {
            return false;
        }

        byte[]? expectedHash = null;
        byte[]? actualHash = null;
        try
        {
            expectedHash = Convert.FromBase64String(expectedFingerprint);
            if (expectedHash.Length != SHA256.HashSizeInBytes)
            {
                return false;
            }

            actualHash = SHA256.HashData(credential);
            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
        finally
        {
            ZeroIfPresent(expectedHash);
            ZeroIfPresent(actualHash);
        }
    }

    private static bool RecoveryPermitsNewOperation(RecoveryStatus status) =>
        status is RecoveryStatus.NoPendingTransaction or
            RecoveryStatus.RecoveredToSource or
            RecoveryStatus.CompletedTargetActivation;

    private static AccountOperationStatus MapRecoveryStatus(RecoveryStatus status) => status switch
    {
        RecoveryStatus.BlockedByRunningProcesses => AccountOperationStatus.BlockedByRunningProcesses,
        RecoveryStatus.ProcessInspectionUnknown => AccountOperationStatus.ProcessInspectionUnknown,
        RecoveryStatus.LockUnavailable => AccountOperationStatus.LockUnavailable,
        _ => AccountOperationStatus.RecoveryRequired
    };

    private void TryDeleteTransactionRecoveryCredential(AgentProvider provider, Guid transactionId)
    {
        try
        {
            _vault.DeleteTransactionRecoveryCredential(provider, transactionId);
        }
        catch (IOException)
        {
            // The journal is already gone, so an orphan here is encrypted and cannot affect recovery.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup avoids recreating a journal that no longer has an active transaction.
        }
    }

    private static void ZeroIfPresent(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static SwitchProfileResult FailedSwitch(
        Guid targetProfileId,
        ProcessInspectionResult inspection,
        string errorMessage,
        Guid? sourceProfileId = null) => new()
        {
            Status = AccountOperationStatus.Failed,
            SourceProfileId = sourceProfileId,
            TargetProfileId = targetProfileId,
            ProcessInspection = inspection,
            ErrorMessage = errorMessage
        };

    private sealed class MutexLease : IDisposable
    {
        private Mutex? _mutex;

        public MutexLease(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose()
        {
            var mutex = Interlocked.Exchange(ref _mutex, null);
            if (mutex is null)
            {
                return;
            }

            try
            {
                mutex.ReleaseMutex();
            }
            finally
            {
                mutex.Dispose();
            }
        }
    }
}
