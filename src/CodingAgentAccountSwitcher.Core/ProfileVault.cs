using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingAgentAccountSwitcher.Core;

public sealed class AuthenticationProfileVault
{
    public const int MaximumCredentialSizeBytes = 16 * 1024 * 1024;
    private const int MaximumProtectedBlobSizeBytes = MaximumCredentialSizeBytes + (1024 * 1024);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    private readonly IByteProtector _protector;
    private readonly IAtomicFileWriter _atomicWriter;
    private readonly TimeProvider _timeProvider;

    public AuthenticationProfileVault(
        string rootDirectory,
        IByteProtector? protector = null,
        IAtomicFileWriter? atomicWriter = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        _protector = protector ?? new DpapiCurrentUserProtector();
        _atomicWriter = atomicWriter ?? new AtomicFileWriter();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string RootDirectory { get; }

    internal IAtomicFileWriter AtomicWriter => _atomicWriter;

    internal TimeProvider TimeProvider => _timeProvider;

    public AuthenticationProfileMetadata CreateProfile(
        AgentProvider provider,
        string displayName,
        byte[] credentialBytes)
    {
        ValidateDisplayName(displayName);
        ValidateCredential(credentialBytes);

        var now = _timeProvider.GetUtcNow();
        var metadata = new AuthenticationProfileMetadata
        {
            ProfileId = Guid.NewGuid(),
            Provider = provider,
            DisplayName = displayName.Trim(),
            CreatedAtUtc = now,
            CapturedAtUtc = now
        };

        var blobPath = GetBlobPath(provider, metadata.ProfileId);
        var metadataPath = GetMetadataPath(provider, metadata.ProfileId);
        var protectedBytes = ProtectCredential(metadata, credentialBytes);

        try
        {
            _atomicWriter.WriteAllBytes(blobPath, protectedBytes);
            try
            {
                WriteJson(metadataPath, metadata);
            }
            catch
            {
                File.Delete(blobPath);
                throw;
            }

            return metadata;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public AuthenticationProfileMetadata UpdateCredential(
        AgentProvider provider,
        Guid profileId,
        byte[] credentialBytes)
    {
        ValidateCredential(credentialBytes);
        var metadata = GetProfile(provider, profileId);
        var protectedBytes = ProtectCredential(metadata, credentialBytes);

        try
        {
            _atomicWriter.WriteAllBytes(GetBlobPath(provider, profileId), protectedBytes);
            var updated = metadata with { CapturedAtUtc = _timeProvider.GetUtcNow() };
            WriteJson(GetMetadataPath(provider, profileId), updated);
            return updated;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public AuthenticationProfileMetadata MarkActivated(AgentProvider provider, Guid profileId)
    {
        var metadata = GetProfile(provider, profileId);
        var updated = metadata with { LastActivatedAtUtc = _timeProvider.GetUtcNow() };
        WriteJson(GetMetadataPath(provider, profileId), updated);
        return updated;
    }

    public AuthenticationProfileMetadata GetProfile(AgentProvider provider, Guid profileId)
    {
        var path = GetMetadataPath(provider, profileId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The authentication profile does not exist.", path);
        }

        var metadata = ReadJson<AuthenticationProfileMetadata>(path);
        ValidateMetadata(metadata, provider, profileId);
        return metadata;
    }

    public IReadOnlyList<AuthenticationProfileMetadata> ListProfiles(AgentProvider provider)
    {
        var directory = GetProviderDirectory(provider);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<AuthenticationProfileMetadata>();
        }

        var profiles = new List<AuthenticationProfileMetadata>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.metadata.json", SearchOption.TopDirectoryOnly))
        {
            var metadata = ReadJson<AuthenticationProfileMetadata>(path);
            ValidateMetadata(metadata, provider, metadata.ProfileId);
            profiles.Add(metadata);
        }

        return profiles
            .OrderBy(profile => profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(profile => profile.ProfileId)
            .ToArray();
    }

    public byte[] LoadCredential(AgentProvider provider, Guid profileId)
    {
        var metadata = GetProfile(provider, profileId);
        var path = GetBlobPath(provider, profileId);
        var protectedBytes = ReadBoundedFile(path, MaximumProtectedBlobSizeBytes);
        var entropy = CreateEntropy(metadata);

        try
        {
            var plaintext = _protector.Unprotect(protectedBytes, entropy);
            try
            {
                ValidateCredential(plaintext);
                return plaintext;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    public ActiveProfileState? GetActiveProfile(AgentProvider provider)
    {
        var path = GetActiveStatePath(provider);
        if (!File.Exists(path))
        {
            return null;
        }

        var state = ReadJson<ActiveProfileState>(path);
        if (state.SchemaVersion != ActiveProfileState.CurrentSchemaVersion || state.Provider != provider ||
            state.ProfileId == Guid.Empty)
        {
            throw new InvalidDataException("The active profile state is invalid.");
        }

        return state;
    }

    public void WriteActiveProfile(ActiveProfileState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != ActiveProfileState.CurrentSchemaVersion || state.ProfileId == Guid.Empty)
        {
            throw new InvalidDataException("The active profile state is invalid.");
        }

        GetProfile(state.Provider, state.ProfileId);
        WriteJson(GetActiveStatePath(state.Provider), state);
    }

    public SwitchTransactionJournal? GetPendingJournal(AgentProvider provider)
    {
        var path = GetJournalPath(provider);
        if (!File.Exists(path))
        {
            return null;
        }

        var journal = ReadJson<SwitchTransactionJournal>(path);
        ValidateJournal(journal, provider);
        return journal;
    }

    public void WritePendingJournal(SwitchTransactionJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ValidateJournal(journal, journal.Provider);
        WriteJson(GetJournalPath(journal.Provider), journal);
    }

    public void DeletePendingJournal(AgentProvider provider)
    {
        File.Delete(GetJournalPath(provider));
    }

    internal void WriteTransactionRecoveryCredential(
        AgentProvider provider,
        Guid transactionId,
        byte[] credentialBytes)
    {
        ValidateTransactionId(transactionId);
        ValidateCredential(credentialBytes);

        var entropy = CreateTransactionRecoveryEntropy(provider, transactionId);
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = _protector.Protect(credentialBytes, entropy);
            _atomicWriter.WriteAllBytes(
                GetTransactionRecoveryPath(provider, transactionId),
                protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    internal byte[] LoadTransactionRecoveryCredential(AgentProvider provider, Guid transactionId)
    {
        ValidateTransactionId(transactionId);
        var protectedBytes = ReadBoundedFile(
            GetTransactionRecoveryPath(provider, transactionId),
            MaximumProtectedBlobSizeBytes);
        var entropy = CreateTransactionRecoveryEntropy(provider, transactionId);

        try
        {
            var plaintext = _protector.Unprotect(protectedBytes, entropy);
            try
            {
                ValidateCredential(plaintext);
                return plaintext;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    internal void DeleteTransactionRecoveryCredential(AgentProvider provider, Guid transactionId)
    {
        ValidateTransactionId(transactionId);
        File.Delete(GetTransactionRecoveryPath(provider, transactionId));
    }

    private byte[] ProtectCredential(AuthenticationProfileMetadata metadata, byte[] credentialBytes)
    {
        var entropy = CreateEntropy(metadata);
        try
        {
            return _protector.Protect(credentialBytes, entropy);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    private static byte[] CreateEntropy(AuthenticationProfileMetadata metadata) =>
        Encoding.UTF8.GetBytes(
            $"CodingAgentAccountSwitcher|vault-v1|{metadata.Provider.ToStorageKey()}|{metadata.ProfileId:N}");

    private static byte[] CreateTransactionRecoveryEntropy(AgentProvider provider, Guid transactionId) =>
        Encoding.UTF8.GetBytes(
            $"CodingAgentAccountSwitcher|transaction-recovery-v1|{provider.ToStorageKey()}|{transactionId:N}");

    private string GetProviderDirectory(AgentProvider provider) =>
        Path.Combine(RootDirectory, "profiles", provider.ToStorageKey());

    private string GetBlobPath(AgentProvider provider, Guid profileId) =>
        Path.Combine(GetProviderDirectory(provider), $"{profileId:N}.vault");

    private string GetMetadataPath(AgentProvider provider, Guid profileId) =>
        Path.Combine(GetProviderDirectory(provider), $"{profileId:N}.metadata.json");

    private string GetActiveStatePath(AgentProvider provider) =>
        Path.Combine(RootDirectory, "state", $"active-{provider.ToStorageKey()}.json");

    private string GetJournalPath(AgentProvider provider) =>
        Path.Combine(RootDirectory, "state", $"journal-{provider.ToStorageKey()}.json");

    private string GetTransactionRecoveryPath(AgentProvider provider, Guid transactionId) =>
        Path.Combine(
            RootDirectory,
            "state",
            $"recovery-{provider.ToStorageKey()}-{transactionId:N}.vault");

    private void WriteJson<T>(string path, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        try
        {
            _atomicWriter.WriteAllBytes(path, bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static T ReadJson<T>(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), JsonOptions);
            return value ?? throw new InvalidDataException($"The file '{path}' contains no data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The file '{path}' is not valid switcher metadata.", exception);
        }
    }

    private static byte[] ReadBoundedFile(string path, int maximumSize)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maximumSize || stream.Length > int.MaxValue)
        {
            throw new InvalidDataException($"The file '{path}' has an invalid size.");
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static void ValidateCredential(byte[] credentialBytes)
    {
        ArgumentNullException.ThrowIfNull(credentialBytes);
        if (credentialBytes.Length == 0 || credentialBytes.Length > MaximumCredentialSizeBytes)
        {
            throw new InvalidDataException("The authentication data has an invalid size.");
        }
    }

    private static void ValidateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var trimmed = displayName.Trim();
        if (trimmed.Length > 80 || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException("The profile display name is invalid.", nameof(displayName));
        }
    }

    private static void ValidateMetadata(
        AuthenticationProfileMetadata metadata,
        AgentProvider expectedProvider,
        Guid expectedProfileId)
    {
        if (metadata.SchemaVersion != AuthenticationProfileMetadata.CurrentSchemaVersion ||
            metadata.Provider != expectedProvider || metadata.ProfileId != expectedProfileId ||
            metadata.ProfileId == Guid.Empty || string.IsNullOrWhiteSpace(metadata.DisplayName) ||
            metadata.DisplayName.Length > 80 || metadata.DisplayName.Any(char.IsControl) ||
            metadata.CreatedAtUtc == default || metadata.CapturedAtUtc == default)
        {
            throw new InvalidDataException("The authentication profile metadata is invalid.");
        }
    }

    private static void ValidateJournal(SwitchTransactionJournal journal, AgentProvider expectedProvider)
    {
        var profileRelationshipIsValid = journal.Kind switch
        {
            SwitchTransactionKind.ProfileSwitch => journal.SourceProfileId != journal.TargetProfileId,
            SwitchTransactionKind.SavedSnapshotRestore => journal.SourceProfileId == journal.TargetProfileId,
            _ => false
        };

        if (journal.SchemaVersion != SwitchTransactionJournal.CurrentSchemaVersion ||
            journal.Provider != expectedProvider || journal.TransactionId == Guid.Empty ||
            journal.SourceProfileId == Guid.Empty || journal.TargetProfileId == Guid.Empty ||
            !profileRelationshipIsValid || journal.StartedAtUtc == default ||
            !Enum.IsDefined(journal.Kind) || !Enum.IsDefined(journal.Phase))
        {
            throw new InvalidDataException("The pending switch journal is invalid.");
        }
    }

    private static void ValidateTransactionId(Guid transactionId)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("The transaction identifier cannot be empty.", nameof(transactionId));
        }
    }
}
