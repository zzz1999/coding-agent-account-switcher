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

        var inspection = _processInspector.Inspect(adapter);
        if (inspection.Status != ProcessInspectionStatus.Clear)
        {
            return new CaptureProfileResult
            {
                Status = inspection.Status == ProcessInspectionStatus.Running
                    ? AccountOperationStatus.BlockedByRunningProcesses
                    : AccountOperationStatus.ProcessInspectionUnknown,
                ProcessInspection = inspection
            };
        }

        var recovery = RecoverCore(adapter, inspection);
        if (!RecoveryPermitsNewOperation(recovery.Status))
        {
            return new CaptureProfileResult
            {
                Status = MapRecoveryStatus(recovery.Status),
                ProcessInspection = recovery.ProcessInspection,
                ErrorMessage = recovery.ErrorMessage
            };
        }

        byte[]? currentCredential = null;
        var authenticationFileRead = false;
        try
        {
            currentCredential = ReadAuthenticationFile(adapter.AuthenticationFilePath);
            authenticationFileRead = true;
            var finalInspection = _processInspector.Inspect(adapter);
            if (finalInspection.Status != ProcessInspectionStatus.Clear)
            {
                return new CaptureProfileResult
                {
                    Status = finalInspection.Status == ProcessInspectionStatus.Running
                        ? AccountOperationStatus.BlockedByRunningProcesses
                        : AccountOperationStatus.ProcessInspectionUnknown,
                    ProcessInspection = finalInspection
                };
            }

            AuthenticationProfileMetadata profile;
            if (profileToReplace.HasValue)
            {
                var existingProfile = _vault.GetProfile(adapter.Provider, profileToReplace.Value);
                if (!string.Equals(existingProfile.DisplayName, displayName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The replacement profile label does not match.");
                }

                profile = _vault.UpdateCredential(adapter.Provider, profileToReplace.Value, currentCredential);
            }
            else
            {
                profile = _vault.CreateProfile(adapter.Provider, displayName, currentCredential);
            }
            _vault.WriteActiveProfile(new ActiveProfileState
            {
                Provider = adapter.Provider,
                ProfileId = profile.ProfileId,
                ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
            });

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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            targetCredential = _vault.LoadCredential(adapter.Provider, targetProfileId);
            currentCredential = ReadAuthenticationFile(adapter.AuthenticationFilePath);
            if (!isSavedSnapshotRestore)
            {
                sourceCredential = _vault.LoadCredential(adapter.Provider, activeState.ProfileId);
            }

            var activeProfileChanged = !BytesEqual(
                currentCredential,
                isSavedSnapshotRestore ? targetCredential : sourceCredential!);
            if (!activeProfileChanged && isSavedSnapshotRestore)
            {
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
            latestCredential = ReadAuthenticationFile(adapter.AuthenticationFilePath);
            if (!BytesEqual(currentCredential, latestCredential))
            {
                CryptographicOperations.ZeroMemory(currentCredential);
                currentCredential = latestCredential;
                latestCredential = null;
                activeProfileChanged = !BytesEqual(
                    currentCredential,
                    isSavedSnapshotRestore ? targetCredential : sourceCredential!);

                if (!activeProfileChanged && isSavedSnapshotRestore)
                {
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

            if (isSavedSnapshotRestore)
            {
                _vault.WriteTransactionRecoveryCredential(
                    adapter.Provider,
                    journal.TransactionId,
                    currentCredential);
                recoveryCredentialWritten = true;
            }
            else if (activeProfileChanged)
            {
                // Persist a confirmed token refresh before switching away.
                _vault.UpdateCredential(adapter.Provider, activeState.ProfileId, currentCredential);
            }

            _vault.WritePendingJournal(journal);
            journalWritten = true;

            var commitInspection = _processInspector.Inspect(adapter);
            if (commitInspection.Status != ProcessInspectionStatus.Clear)
            {
                try
                {
                    _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                        adapter.AuthenticationFilePath,
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

            cancellationToken.ThrowIfCancellationRequested();
            _vault.AtomicWriter.WriteAllBytes(
                adapter.AuthenticationFilePath,
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
            _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                adapter.AuthenticationFilePath,
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

            try
            {
                if (currentCredential is not null && journal is not null)
                {
                    _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                        adapter.AuthenticationFilePath,
                        journal.TransactionId);
                    _vault.AtomicWriter.WriteAllBytes(
                        adapter.AuthenticationFilePath,
                        currentCredential,
                        journal.TransactionId);
                }

                _vault.WriteActiveProfile(activeState);
                if (journal is not null)
                {
                    _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                        adapter.AuthenticationFilePath,
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
        try
        {
            sourceCredential = _vault.LoadCredential(adapter.Provider, journal.SourceProfileId);

            try
            {
                currentCredential = ReadAuthenticationFile(adapter.AuthenticationFilePath);
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

                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
                    journal.TransactionId);
                _vault.AtomicWriter.WriteAllBytes(
                    adapter.AuthenticationFilePath,
                    sourceCredential,
                    journal.TransactionId);
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
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

            if (BytesEqual(currentCredential, sourceCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.RecoveredToSource,
                    ActiveProfileId = journal.SourceProfileId,
                    ProcessInspection = inspection
                };
            }

            targetCredential = _vault.LoadCredential(adapter.Provider, journal.TargetProfileId);
            if (BytesEqual(currentCredential, targetCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.TargetProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.MarkActivated(adapter.Provider, journal.TargetProfileId);
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
                    journal.TransactionId);
                _vault.DeletePendingJournal(adapter.Provider);
                return new RecoveryResult
                {
                    Status = RecoveryStatus.CompletedTargetActivation,
                    ActiveProfileId = journal.TargetProfileId,
                    ProcessInspection = inspection
                };
            }

            _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                adapter.AuthenticationFilePath,
                journal.TransactionId);
            return new RecoveryResult
            {
                Status = RecoveryStatus.ManualInterventionRequired,
                ProcessInspection = inspection,
                ErrorMessage = "The authentication file matches neither side of the interrupted switch."
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
                currentCredential = ReadAuthenticationFile(adapter.AuthenticationFilePath);
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
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
                    journal.TransactionId);
                _vault.AtomicWriter.WriteAllBytes(
                    adapter.AuthenticationFilePath,
                    recoveryCredential,
                    journal.TransactionId);
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
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

            if (BytesEqual(currentCredential, targetCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.TargetProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.MarkActivated(adapter.Provider, journal.TargetProfileId);
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
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
            if (BytesEqual(currentCredential, recoveryCredential))
            {
                _vault.WriteActiveProfile(new ActiveProfileState
                {
                    Provider = adapter.Provider,
                    ProfileId = journal.SourceProfileId,
                    ActivatedAtUtc = _vault.TimeProvider.GetUtcNow()
                });
                _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                    adapter.AuthenticationFilePath,
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
            _vault.AtomicWriter.DeleteOwnedTransactionFiles(
                adapter.AuthenticationFilePath,
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

    private static byte[] ReadAuthenticationFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > AuthenticationProfileVault.MaximumCredentialSizeBytes ||
            stream.Length > int.MaxValue)
        {
            throw new InvalidDataException("The authentication file has an invalid size.");
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static bool BytesEqual(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);

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
