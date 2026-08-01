using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingAgentAccountSwitcher.Core;

public sealed class AuthenticationProfileVault
{
    public const int MaximumCredentialSizeBytes = 16 * 1024 * 1024;
    private const int MaximumProtectedBlobSizeBytes = MaximumCredentialSizeBytes + (1024 * 1024);
    private const int MaximumMetadataSizeBytes = 1024 * 1024;
    private const string ProfileMetadataSuffix = ".metadata.json";

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
        byte[] credentialBytes) =>
        CreateProfileCore(provider, displayName, credentialBytes, onMutationStarting: null);

    private AuthenticationProfileMetadata CreateProfileCore(
        AgentProvider provider,
        string displayName,
        byte[] credentialBytes,
        Action? onMutationStarting)
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
            try
            {
                onMutationStarting?.Invoke();
                _atomicWriter.WriteAllBytes(blobPath, protectedBytes);
                WriteJson(metadataPath, metadata);
                return metadata;
            }
            catch (Exception createException)
            {
                var rollbackErrors = new List<Exception>();
                var metadataRemoved = TryRollback(() => File.Delete(metadataPath), rollbackErrors);
                if (metadataRemoved)
                {
                    TryRollback(() => File.Delete(blobPath), rollbackErrors);
                }
                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, createException);
                    throw new IOException(
                        "Creating the authentication profile failed and its files could not be removed completely.",
                        new AggregateException(rollbackErrors));
                }

                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public AuthenticationProfileMetadata UpdateCredential(
        AgentProvider provider,
        Guid profileId,
        byte[] credentialBytes) =>
        UpdateCredentialCore(provider, profileId, credentialBytes, onMutationStarting: null);

    private AuthenticationProfileMetadata UpdateCredentialCore(
        AgentProvider provider,
        Guid profileId,
        byte[] credentialBytes,
        Action? onMutationStarting)
    {
        ValidateCredential(credentialBytes);
        var metadata = GetProfile(provider, profileId);
        var blobPath = GetBlobPath(provider, profileId);
        var metadataPath = GetMetadataPath(provider, profileId);
        byte[]? previousBlob = null;
        byte[]? previousMetadata = null;
        byte[]? protectedBytes = null;

        try
        {
            previousBlob = ReadBoundedFile(blobPath, MaximumProtectedBlobSizeBytes);
            previousMetadata = ReadBoundedFile(metadataPath, MaximumMetadataSizeBytes);
            protectedBytes = ProtectCredential(metadata, credentialBytes);
            try
            {
                onMutationStarting?.Invoke();
                _atomicWriter.WriteAllBytes(blobPath, protectedBytes);
                var updated = metadata with { CapturedAtUtc = _timeProvider.GetUtcNow() };
                WriteJson(metadataPath, updated);
                return updated;
            }
            catch (Exception updateException)
            {
                var rollbackErrors = new List<Exception>();
                TryRollback(
                    () => _atomicWriter.WriteAllBytes(blobPath, previousBlob!),
                    rollbackErrors);
                TryRollback(
                    () => _atomicWriter.WriteAllBytes(metadataPath, previousMetadata!),
                    rollbackErrors);
                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, updateException);
                    throw new IOException(
                        "Updating the authentication profile failed and its previous state could not be restored completely.",
                        new AggregateException(rollbackErrors));
                }

                throw;
            }
        }
        finally
        {
            ZeroIfPresent(protectedBytes);
            ZeroIfPresent(previousBlob);
            ZeroIfPresent(previousMetadata);
        }
    }

    internal AuthenticationProfileMetadata CaptureProfileAndSetActive(
        AgentProvider provider,
        string displayName,
        byte[] credentialBytes,
        Guid? profileToReplace)
    {
        ValidateDisplayName(displayName);
        ValidateCredential(credentialBytes);

        var activeStatePath = GetActiveStatePath(provider);
        var activeStateExisted = false;
        byte[]? previousActiveState = null;
        byte[]? previousProfileBlob = null;
        byte[]? previousProfileMetadata = null;
        AuthenticationProfileMetadata? capturedProfile = null;
        var mutationStarted = false;

        try
        {
            activeStateExisted = File.Exists(activeStatePath);
            previousActiveState = activeStateExisted
                ? ReadBoundedFile(activeStatePath, MaximumMetadataSizeBytes)
                : null;

            if (profileToReplace.HasValue)
            {
                // Read and validate the existing profile before taking the exact on-disk
                // snapshot used if committing the active state fails.
                GetProfile(provider, profileToReplace.Value);
                previousProfileBlob = ReadBoundedFile(
                    GetBlobPath(provider, profileToReplace.Value),
                    MaximumProtectedBlobSizeBytes);
                previousProfileMetadata = ReadBoundedFile(
                    GetMetadataPath(provider, profileToReplace.Value),
                    MaximumMetadataSizeBytes);
            }

            void MarkMutationStarting() => mutationStarted = true;
            capturedProfile = profileToReplace.HasValue
                ? UpdateCredentialCore(
                    provider,
                    profileToReplace.Value,
                    credentialBytes,
                    MarkMutationStarting)
                : CreateProfileCore(provider, displayName, credentialBytes, MarkMutationStarting);

            WriteActiveProfile(new ActiveProfileState
            {
                Provider = provider,
                ProfileId = capturedProfile.ProfileId,
                ActivatedAtUtc = _timeProvider.GetUtcNow()
            });

            return capturedProfile;
        }
        catch (Exception captureException)
        {
            if (!mutationStarted)
            {
                throw;
            }

            var rollbackErrors = new List<Exception>();

            if (profileToReplace.HasValue)
            {
                TryRollback(
                    () => _atomicWriter.WriteAllBytes(
                        GetBlobPath(provider, profileToReplace.Value),
                        previousProfileBlob!),
                    rollbackErrors);
                TryRollback(
                    () => _atomicWriter.WriteAllBytes(
                        GetMetadataPath(provider, profileToReplace.Value),
                        previousProfileMetadata!),
                    rollbackErrors);
            }

            var activeStateRestored = TryRollback(
                () => RestoreFile(activeStatePath, activeStateExisted, previousActiveState),
                rollbackErrors);

            if (!profileToReplace.HasValue && capturedProfile is not null && activeStateRestored)
            {
                var metadataRemoved = TryRollback(
                    () => File.Delete(GetMetadataPath(provider, capturedProfile.ProfileId)),
                    rollbackErrors);
                if (metadataRemoved)
                {
                    TryRollback(
                        () => File.Delete(GetBlobPath(provider, capturedProfile.ProfileId)),
                        rollbackErrors);
                }
            }

            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, captureException);
                throw new IOException(
                    "Capturing the login failed and the profile state could not be rolled back completely.",
                    new AggregateException(rollbackErrors));
            }

            throw;
        }
        finally
        {
            ZeroIfPresent(previousActiveState);
            ZeroIfPresent(previousProfileBlob);
            ZeroIfPresent(previousProfileMetadata);
        }
    }

    public AuthenticationProfileMetadata MarkActivated(AgentProvider provider, Guid profileId)
    {
        var metadata = GetProfile(provider, profileId);
        var updated = metadata with { LastActivatedAtUtc = _timeProvider.GetUtcNow() };
        WriteJson(GetMetadataPath(provider, profileId), updated);
        return updated;
    }

    public AuthenticationProfileMetadata RenameProfile(
        AgentProvider provider,
        Guid profileId,
        string displayName)
    {
        ValidateDisplayName(displayName);
        var metadata = GetProfile(provider, profileId);
        var trimmedDisplayName = displayName.Trim();
        if (string.Equals(metadata.DisplayName, trimmedDisplayName, StringComparison.Ordinal))
        {
            return metadata;
        }

        var metadataPath = GetMetadataPath(provider, profileId);
        byte[]? previousMetadata = null;
        var updated = metadata with { DisplayName = trimmedDisplayName };
        try
        {
            previousMetadata = ReadBoundedFile(metadataPath, MaximumMetadataSizeBytes);
            try
            {
                WriteJson(metadataPath, updated);
                return updated;
            }
            catch (Exception renameException)
            {
                var rollbackErrors = new List<Exception>();
                TryRollback(
                    () => _atomicWriter.WriteAllBytes(metadataPath, previousMetadata!),
                    rollbackErrors);
                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, renameException);
                    throw new IOException(
                        "Renaming the authentication profile failed and its previous metadata could not be restored.",
                        new AggregateException(rollbackErrors));
                }

                throw;
            }
        }
        finally
        {
            ZeroIfPresent(previousMetadata);
        }
    }

    public bool DeleteProfile(AgentProvider provider, Guid profileId)
    {
        GetProfile(provider, profileId);
        var blobPath = GetBlobPath(provider, profileId);
        var metadataPath = GetMetadataPath(provider, profileId);
        var activeStatePath = GetActiveStatePath(provider);
        var activeState = GetActiveProfile(provider);
        var removesActiveSelection = activeState?.ProfileId == profileId;

        byte[]? previousBlob = null;
        byte[]? previousMetadata = null;
        byte[]? previousActiveState = null;
        var activeMutationAttempted = false;
        var metadataMutationAttempted = false;
        var blobMutationAttempted = false;
        try
        {
            // Capture exact bytes before the first destructive write so a normal
            // I/O failure can restore the complete encrypted profile.
            previousBlob = ReadBoundedFile(blobPath, MaximumProtectedBlobSizeBytes);
            previousMetadata = ReadBoundedFile(metadataPath, MaximumMetadataSizeBytes);
            previousActiveState = removesActiveSelection
                ? ReadBoundedFile(activeStatePath, MaximumMetadataSizeBytes)
                : null;

            if (removesActiveSelection)
            {
                activeMutationAttempted = true;
                _atomicWriter.DeleteFile(activeStatePath);
            }

            // Hide metadata before removing the encrypted blob. A sudden process
            // termination can then leave only an unreferenced encrypted file,
            // never a visible profile whose credential blob is missing.
            metadataMutationAttempted = true;
            _atomicWriter.DeleteFile(metadataPath);
            blobMutationAttempted = true;
            _atomicWriter.DeleteFile(blobPath);
            return removesActiveSelection;
        }
        catch (Exception deleteException)
        {
            var rollbackErrors = new List<Exception>();
            var blobRestored = !blobMutationAttempted || TryRollback(
                () => _atomicWriter.WriteAllBytes(blobPath, previousBlob!),
                rollbackErrors);
            var metadataRestored = !metadataMutationAttempted;
            if (metadataMutationAttempted && blobRestored)
            {
                metadataRestored = TryRollback(
                    () => _atomicWriter.WriteAllBytes(metadataPath, previousMetadata!),
                    rollbackErrors);
            }

            if (activeMutationAttempted && metadataRestored)
            {
                TryRollback(
                    () => _atomicWriter.WriteAllBytes(activeStatePath, previousActiveState!),
                    rollbackErrors);
            }

            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, deleteException);
                throw new IOException(
                    "Deleting the authentication profile failed and its previous state could not be restored completely.",
                    new AggregateException(rollbackErrors));
            }

            throw;
        }
        finally
        {
            ZeroIfPresent(previousBlob);
            ZeroIfPresent(previousMetadata);
            ZeroIfPresent(previousActiveState);
        }
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
        var result = ListProfilesWithIssues(provider);
        if (result.Issues.Count > 0)
        {
            throw new InvalidDataException(
                $"One or more authentication profile metadata files are invalid. " +
                $"The first affected file is '{result.Issues[0].FileName}'.");
        }

        return result.Profiles;
    }

    public AuthenticationProfileListResult ListProfilesWithIssues(AgentProvider provider)
    {
        var directory = GetProviderDirectory(provider);
        try
        {
            return ListProfilesWithIssues(
                provider,
                Directory.EnumerateFiles(directory, $"*{ProfileMetadataSuffix}", SearchOption.TopDirectoryOnly));
        }
        catch (DirectoryNotFoundException)
        {
            // A provider that has never saved a profile has no directory yet.
            return new AuthenticationProfileListResult();
        }
    }

    internal static AuthenticationProfileListResult ListProfilesWithIssues(
        AgentProvider provider,
        IEnumerable<string> metadataPaths)
    {
        ArgumentNullException.ThrowIfNull(metadataPaths);
        var profiles = new List<AuthenticationProfileMetadata>();
        var issues = new List<AuthenticationProfileLoadIssue>();
        foreach (var path in metadataPaths)
        {
            var fileName = Path.GetFileName(path);
            try
            {
                var expectedProfileId = ParseProfileIdFromMetadataFileName(fileName);
                var metadata = ReadJson<AuthenticationProfileMetadata>(path);
                ValidateMetadata(metadata, provider, expectedProfileId);
                profiles.Add(metadata);
            }
            catch (Exception exception) when (IsIsolatableProfileMetadataException(exception))
            {
                // Keep damaged files untouched so the user can recover or inspect them manually.
                issues.Add(new AuthenticationProfileLoadIssue
                {
                    FileName = fileName,
                    Message = exception.Message
                });
            }
        }

        return new AuthenticationProfileListResult
        {
            Profiles = profiles
                .OrderBy(profile => profile.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(profile => profile.ProfileId)
                .ToArray(),
            Issues = issues.ToArray()
        };
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
        var bytes = ReadBoundedFile(path, MaximumMetadataSizeBytes);
        try
        {
            var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            return value ?? throw new InvalidDataException($"The file '{path}' contains no data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The file '{path}' is not valid switcher metadata.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static Guid ParseProfileIdFromMetadataFileName(string fileName)
    {
        if (!fileName.EndsWith(ProfileMetadataSuffix, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(fileName[..^ProfileMetadataSuffix.Length], "N", out var profileId) ||
            profileId == Guid.Empty)
        {
            throw new InvalidDataException("The authentication profile metadata file name is invalid.");
        }

        return profileId;
    }

    private static bool IsIsolatableProfileMetadataException(Exception exception) =>
        exception is InvalidDataException or IOException or UnauthorizedAccessException or
            System.Security.SecurityException;

    private static byte[] ReadBoundedFile(string path, int maximumSize)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maximumSize || stream.Length > int.MaxValue)
        {
            throw new InvalidDataException($"The file '{path}' has an invalid size.");
        }

        var bytes = new byte[(int)stream.Length];
        try
        {
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private void RestoreFile(string path, bool existed, byte[]? previousContents)
    {
        if (existed)
        {
            _atomicWriter.WriteAllBytes(path, previousContents!);
        }
        else
        {
            try
            {
                File.Delete(path);
            }
            catch (DirectoryNotFoundException)
            {
                // The exact pre-operation state was that neither the file nor its
                // parent directory existed, so there is nothing left to restore.
            }
        }
    }

    private static bool TryRollback(Action action, ICollection<Exception> rollbackErrors)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            rollbackErrors.Add(exception);
            return false;
        }
    }

    private static void ZeroIfPresent(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
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
