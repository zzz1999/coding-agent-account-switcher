namespace CodingAgentAccountSwitcher.Core;

public enum AccountOperationStatus
{
    Success,
    AlreadyActive,
    BlockedByRunningProcesses,
    ProcessInspectionUnknown,
    AuthenticationFileMissing,
    AuthenticationFileEmpty,
    ProfileNotFound,
    ActiveProfileUpdateConfirmationRequired,
    SavedSnapshotRestoreConfirmationRequired,
    LockUnavailable,
    RecoveryRequired,
    Failed
}

public sealed record CaptureProfileResult
{
    public required AccountOperationStatus Status { get; init; }

    public AuthenticationProfileMetadata? Profile { get; init; }

    public ProcessInspectionResult ProcessInspection { get; init; } = ProcessInspectionResult.Clear;

    public string? ErrorMessage { get; init; }
}

public sealed record SwitchProfileResult
{
    public required AccountOperationStatus Status { get; init; }

    public Guid? SourceProfileId { get; init; }

    public required Guid TargetProfileId { get; init; }

    public bool RolledBack { get; init; }

    public ProcessInspectionResult ProcessInspection { get; init; } = ProcessInspectionResult.Clear;

    public string? ConfirmationFingerprint { get; init; }

    public string? ErrorMessage { get; init; }
}

public enum RecoveryStatus
{
    NoPendingTransaction,
    RecoveredToSource,
    CompletedTargetActivation,
    BlockedByRunningProcesses,
    ProcessInspectionUnknown,
    LockUnavailable,
    ManualInterventionRequired,
    Failed
}

public sealed record RecoveryResult
{
    public required RecoveryStatus Status { get; init; }

    public Guid? ActiveProfileId { get; init; }

    public ProcessInspectionResult ProcessInspection { get; init; } = ProcessInspectionResult.Clear;

    public string? ErrorMessage { get; init; }
}

public enum ProfileManagementStatus
{
    Success,
    ProfileNotFound,
    DisplayNameConflict,
    ActiveProfileConfirmationRequired,
    LockUnavailable,
    RecoveryRequired,
    Failed,
}

public sealed record ProfileManagementResult
{
    public required ProfileManagementStatus Status { get; init; }

    public AuthenticationProfileMetadata? Profile { get; init; }

    public bool RemovedActiveSelection { get; init; }

    public string? ErrorMessage { get; init; }
}
