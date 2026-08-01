namespace CodingAgentAccountSwitcher.Core;

public sealed record AuthenticationProfileMetadata
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid ProfileId { get; init; }

    public required AgentProvider Provider { get; init; }

    public required string DisplayName { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public DateTimeOffset? LastActivatedAtUtc { get; init; }
}

public sealed record AuthenticationProfileLoadIssue
{
    public required string FileName { get; init; }

    public required string Message { get; init; }
}

public sealed record AuthenticationProfileListResult
{
    public IReadOnlyList<AuthenticationProfileMetadata> Profiles { get; init; } =
        Array.Empty<AuthenticationProfileMetadata>();

    public IReadOnlyList<AuthenticationProfileLoadIssue> Issues { get; init; } =
        Array.Empty<AuthenticationProfileLoadIssue>();
}

public sealed record ActiveProfileState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required AgentProvider Provider { get; init; }

    public required Guid ProfileId { get; init; }

    public required DateTimeOffset ActivatedAtUtc { get; init; }
}

public enum SwitchJournalPhase
{
    Prepared,
    TargetInstalled
}

public enum SwitchTransactionKind
{
    ProfileSwitch,
    SavedSnapshotRestore
}

public sealed record SwitchTransactionJournal
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid TransactionId { get; init; }

    public required AgentProvider Provider { get; init; }

    public required SwitchTransactionKind Kind { get; init; }

    public required Guid SourceProfileId { get; init; }

    public required Guid TargetProfileId { get; init; }

    public required SwitchJournalPhase Phase { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }
}
