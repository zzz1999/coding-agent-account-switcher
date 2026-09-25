using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core;

public interface IAuthenticationAdapter
{
    AgentProvider Provider { get; }

    string DisplayName { get; }

    // Kept as the primary location for storage scoping and compatibility with
    // authentication-only profiles created by earlier app versions.
    string AuthenticationFilePath { get; }

    IReadOnlyList<string> ManagedFilePaths { get; }

    IReadOnlyList<ProcessNameRule> BlockingProcessRules { get; }

    bool HasCurrentSnapshot { get; }

    bool IsLegacySnapshot(byte[] snapshotBytes);

    byte[] ReadSnapshot(bool allowIncomplete = false);

    void WriteSnapshot(IAtomicFileWriter atomicWriter, byte[] snapshotBytes, Guid transactionId);

    bool SnapshotsEqual(byte[] left, byte[] right);

    bool IsRecognizedPartialSnapshot(byte[] source, byte[] target, byte[] current);

    void DeleteOwnedTransactionFiles(IAtomicFileWriter atomicWriter, Guid transactionId);
}

public sealed class CodexAuthenticationAdapter : IAuthenticationAdapter
{
    private const int SnapshotReadAttempts = 3;

    private static readonly IReadOnlyList<ProcessNameRule> Processes = Array.AsReadOnly<ProcessNameRule>(
    [
        ProcessNameRule.Exact("ChatGPT"),
        ProcessNameRule.Exact("codex"),
        ProcessNameRule.Exact("codex-code-mode-host"),
        ProcessNameRule.Prefix("codex-command-runner-")
    ]);

    private readonly bool _manageApiConfiguration;

    public CodexAuthenticationAdapter(
        string? userProfileDirectory = null,
        string? codexHomeDirectory = null,
        bool manageApiConfiguration = true)
    {
        var authenticationRoot = AuthenticationPathResolution.ResolveRoot(
            userProfileDirectory,
            codexHomeDirectory,
            "CODEX_HOME",
            ".codex");
        AuthenticationFilePath = Path.Combine(authenticationRoot, "auth.json");
        ConfigurationFilePath = Path.Combine(authenticationRoot, "config.toml");
        _manageApiConfiguration = manageApiConfiguration;
        ManagedFilePaths = manageApiConfiguration
            ? Array.AsReadOnly([AuthenticationFilePath, ConfigurationFilePath])
            : Array.AsReadOnly([AuthenticationFilePath]);
    }

    public AgentProvider Provider => AgentProvider.Codex;

    public string DisplayName => "Codex";

    public string AuthenticationFilePath { get; }

    public string ConfigurationFilePath { get; }

    public IReadOnlyList<string> ManagedFilePaths { get; }

    public IReadOnlyList<ProcessNameRule> BlockingProcessRules => Processes;

    public bool HasCurrentSnapshot
    {
        get
        {
            if (HasNonEmptyFile(AuthenticationFilePath))
            {
                return true;
            }

            if (!_manageApiConfiguration)
            {
                return false;
            }

            byte[]? configuration = null;
            try
            {
                configuration = CodexManagedConfiguration.Capture(ConfigurationFilePath);
                return CodexManagedConfiguration.HasManagedValues(configuration);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return false;
            }
            finally
            {
                ZeroIfPresent(configuration);
            }
        }
    }

    public byte[] ReadSnapshot(bool allowIncomplete = false)
    {
        if (!_manageApiConfiguration)
        {
            return AuthenticationSnapshotFiles.ReadRequiredAuthenticationFile(AuthenticationFilePath);
        }

        for (var attempt = 0; attempt < SnapshotReadAttempts; attempt++)
        {
            byte[]? authenticationBefore = null;
            byte[]? authenticationAfter = null;
            byte[]? configurationBefore = null;
            byte[]? configurationAfter = null;
            try
            {
                authenticationBefore = AuthenticationSnapshotFiles.ReadOptionalAuthenticationFile(
                    AuthenticationFilePath);
                configurationBefore = CodexManagedConfiguration.Capture(ConfigurationFilePath);
                authenticationAfter = AuthenticationSnapshotFiles.ReadOptionalAuthenticationFile(
                    AuthenticationFilePath);
                configurationAfter = CodexManagedConfiguration.Capture(ConfigurationFilePath);

                if (!OptionalBytesEqual(authenticationBefore, authenticationAfter) ||
                    !AuthenticationSnapshotFiles.BytesEqual(configurationBefore, configurationAfter))
                {
                    continue;
                }

                if (authenticationAfter is null &&
                    !CodexManagedConfiguration.HasManagedValues(configurationAfter) &&
                    !allowIncomplete)
                {
                    throw new FileNotFoundException(
                        "Neither Codex credentials nor managed provider settings were found.",
                        AuthenticationFilePath);
                }

                return AuthenticationSnapshotCodec.Encode(new ManagedAuthenticationSnapshot
                {
                    Provider = Provider,
                    AuthenticationFileExists = authenticationAfter is not null,
                    AuthenticationFileContents = authenticationAfter,
                    ManagedConfiguration = configurationAfter
                });
            }
            finally
            {
                ZeroIfPresent(authenticationBefore);
                ZeroIfPresent(authenticationAfter);
                ZeroIfPresent(configurationBefore);
                ZeroIfPresent(configurationAfter);
            }
        }

        throw new IOException(
            "The Codex credentials or provider settings changed while the account snapshot was being read. Try saving again.");
    }

    public void WriteSnapshot(IAtomicFileWriter atomicWriter, byte[] snapshotBytes, Guid transactionId)
    {
        ArgumentNullException.ThrowIfNull(atomicWriter);
        ArgumentNullException.ThrowIfNull(snapshotBytes);
        if (!_manageApiConfiguration)
        {
            atomicWriter.WriteAllBytes(AuthenticationFilePath, snapshotBytes, transactionId);
            return;
        }

        if (!AuthenticationSnapshotCodec.TryDecode(snapshotBytes, out var snapshot))
        {
            snapshot = CreateLegacySnapshot(
                Provider,
                snapshotBytes,
                CodexManagedConfiguration.EmptySnapshot());
        }

        try
        {
            EnsureProvider(snapshot!, Provider);
            var merged = CodexManagedConfiguration.Merge(
                ConfigurationFilePath,
                snapshot!.ManagedConfiguration);
            try
            {
                // Validate and prepare the merged configuration before beginning the
                // fail-closed commit. Once the commit starts, no credential is ever
                // paired with the endpoint from the opposite side of the switch.
                atomicWriter.DeleteFile(AuthenticationFilePath);
                if (merged is not null)
                {
                    atomicWriter.WriteAllBytes(ConfigurationFilePath, merged, transactionId);
                }
            }
            finally
            {
                ZeroIfPresent(merged);
            }

            AuthenticationSnapshotFiles.ReplaceAuthenticationFile(
                atomicWriter,
                AuthenticationFilePath,
                snapshot.AuthenticationFileExists,
                snapshot.AuthenticationFileContents,
                transactionId);
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(snapshot);
        }
    }

    public bool SnapshotsEqual(byte[] left, byte[] right) =>
        AuthenticationSnapshotComparison.AreEqual(
            Provider,
            left,
            right,
            CodexManagedConfiguration.SnapshotsEqual);

    public bool IsLegacySnapshot(byte[] snapshotBytes) =>
        !AuthenticationSnapshotComparison.IsManagedSnapshot(Provider, snapshotBytes);

    public bool IsRecognizedPartialSnapshot(byte[] source, byte[] target, byte[] current) =>
        AuthenticationSnapshotComparison.IsRecognizedPartial(
            Provider,
            source,
            target,
            current,
            CodexManagedConfiguration.IsRecognizedPartial);

    public void DeleteOwnedTransactionFiles(IAtomicFileWriter atomicWriter, Guid transactionId) =>
        AuthenticationSnapshotComparison.DeleteOwnedTransactionFiles(
            atomicWriter,
            ManagedFilePaths,
            transactionId);

    private static bool OptionalBytesEqual(byte[]? left, byte[]? right) =>
        left is null
            ? right is null
            : right is not null && AuthenticationSnapshotFiles.BytesEqual(left, right);

    private static void EnsureProvider(ManagedAuthenticationSnapshot snapshot, AgentProvider provider)
    {
        if (snapshot.Provider != provider)
        {
            throw new InvalidDataException("The saved account snapshot belongs to a different provider.");
        }
    }

    private static ManagedAuthenticationSnapshot CreateLegacySnapshot(
        AgentProvider provider,
        byte[] authentication,
        byte[] emptyConfiguration) => new()
        {
            Provider = provider,
            AuthenticationFileExists = true,
            AuthenticationFileContents = authentication.ToArray(),
            ManagedConfiguration = emptyConfiguration
        };

    private static void ZeroIfPresent(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool HasNonEmptyFile(string path)
    {
        try
        {
            return new FileInfo(path) is { Exists: true, Length: > 0 };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public sealed class ClaudeCodeAuthenticationAdapter : IAuthenticationAdapter
{
    private static readonly IReadOnlyList<ProcessNameRule> Processes = Array.AsReadOnly<ProcessNameRule>(
    [
        ProcessNameRule.Exact("claude"),
        ProcessNameRule.Exact("claude-code")
    ]);

    private readonly bool _manageApiConfiguration;
    private readonly Func<string, byte[]?> _readAuthenticationFile;

    public ClaudeCodeAuthenticationAdapter(
        string? userProfileDirectory = null,
        string? claudeConfigDirectory = null,
        bool manageApiConfiguration = true)
        : this(
            userProfileDirectory,
            claudeConfigDirectory,
            manageApiConfiguration,
            AuthenticationSnapshotFiles.ReadOptionalAuthenticationFile)
    {
    }

    internal ClaudeCodeAuthenticationAdapter(
        string? userProfileDirectory,
        string? claudeConfigDirectory,
        bool manageApiConfiguration,
        Func<string, byte[]?> readAuthenticationFile)
    {
        ArgumentNullException.ThrowIfNull(readAuthenticationFile);
        _readAuthenticationFile = readAuthenticationFile;
        var authenticationRoot = AuthenticationPathResolution.ResolveRoot(
            userProfileDirectory,
            claudeConfigDirectory,
            "CLAUDE_CONFIG_DIR",
            ".claude");
        AuthenticationFilePath = Path.Combine(authenticationRoot, ".credentials.json");
        SettingsFilePath = Path.Combine(authenticationRoot, "settings.json");
        _manageApiConfiguration = manageApiConfiguration;
        ManagedFilePaths = manageApiConfiguration
            ? Array.AsReadOnly([AuthenticationFilePath, SettingsFilePath])
            : Array.AsReadOnly([AuthenticationFilePath]);
    }

    public AgentProvider Provider => AgentProvider.ClaudeCode;

    public string DisplayName => "Claude Code";

    public string AuthenticationFilePath { get; }

    public string SettingsFilePath { get; }

    public IReadOnlyList<string> ManagedFilePaths { get; }

    public IReadOnlyList<ProcessNameRule> BlockingProcessRules => Processes;

    public bool HasCurrentSnapshot
    {
        get
        {
            if (HasNonEmptyFile(AuthenticationFilePath))
            {
                return true;
            }

            if (!_manageApiConfiguration)
            {
                return false;
            }

            byte[]? configuration = null;
            try
            {
                configuration = ClaudeManagedConfiguration.Capture(SettingsFilePath);
                return ClaudeManagedConfiguration.HasManagedValues(configuration);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return false;
            }
            finally
            {
                ZeroIfPresent(configuration);
            }
        }
    }

    public byte[] ReadSnapshot(bool allowIncomplete = false)
    {
        if (!_manageApiConfiguration)
        {
            return AuthenticationSnapshotFiles.ReadRequiredAuthenticationFile(AuthenticationFilePath);
        }

        return ConsistentAuthenticationSnapshotReader.Read(
            Provider,
            DisplayName,
            AuthenticationFilePath,
            _readAuthenticationFile,
            () => ClaudeManagedConfiguration.Capture(SettingsFilePath),
            ClaudeManagedConfiguration.HasManagedValues,
            allowIncomplete);
    }

    public void WriteSnapshot(IAtomicFileWriter atomicWriter, byte[] snapshotBytes, Guid transactionId)
    {
        ArgumentNullException.ThrowIfNull(atomicWriter);
        ArgumentNullException.ThrowIfNull(snapshotBytes);
        if (!_manageApiConfiguration)
        {
            atomicWriter.WriteAllBytes(AuthenticationFilePath, snapshotBytes, transactionId);
            return;
        }

        if (!AuthenticationSnapshotCodec.TryDecode(snapshotBytes, out var snapshot))
        {
            snapshot = CreateLegacySnapshot(
                Provider,
                snapshotBytes,
                ClaudeManagedConfiguration.EmptySnapshot());
        }

        try
        {
            EnsureProvider(snapshot!, Provider);
            var merged = ClaudeManagedConfiguration.Merge(SettingsFilePath, snapshot!.ManagedConfiguration);
            try
            {
                atomicWriter.DeleteFile(AuthenticationFilePath);
                if (merged is not null)
                {
                    atomicWriter.WriteAllBytes(SettingsFilePath, merged, transactionId);
                }
            }
            finally
            {
                ZeroIfPresent(merged);
            }

            AuthenticationSnapshotFiles.ReplaceAuthenticationFile(
                atomicWriter,
                AuthenticationFilePath,
                snapshot.AuthenticationFileExists,
                snapshot.AuthenticationFileContents,
                transactionId);
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(snapshot);
        }
    }

    public bool SnapshotsEqual(byte[] left, byte[] right) =>
        AuthenticationSnapshotComparison.AreEqual(Provider, left, right);

    public bool IsLegacySnapshot(byte[] snapshotBytes) =>
        !AuthenticationSnapshotComparison.IsManagedSnapshot(Provider, snapshotBytes);

    public bool IsRecognizedPartialSnapshot(byte[] source, byte[] target, byte[] current) =>
        AuthenticationSnapshotComparison.IsRecognizedPartial(Provider, source, target, current);

    public void DeleteOwnedTransactionFiles(IAtomicFileWriter atomicWriter, Guid transactionId) =>
        AuthenticationSnapshotComparison.DeleteOwnedTransactionFiles(
            atomicWriter,
            ManagedFilePaths,
            transactionId);

    private static void EnsureProvider(ManagedAuthenticationSnapshot snapshot, AgentProvider provider)
    {
        if (snapshot.Provider != provider)
        {
            throw new InvalidDataException("The saved account snapshot belongs to a different provider.");
        }
    }

    private static ManagedAuthenticationSnapshot CreateLegacySnapshot(
        AgentProvider provider,
        byte[] authentication,
        byte[] emptyConfiguration) => new()
        {
            Provider = provider,
            AuthenticationFileExists = true,
            AuthenticationFileContents = authentication.ToArray(),
            ManagedConfiguration = emptyConfiguration
        };

    private static void ZeroIfPresent(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool HasNonEmptyFile(string path)
    {
        try
        {
            return new FileInfo(path) is { Exists: true, Length: > 0 };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public sealed class OpenCodeAuthenticationAdapter : IAuthenticationAdapter
{
    private static readonly IReadOnlyList<ProcessNameRule> Processes = Array.AsReadOnly<ProcessNameRule>(
    [
        ProcessNameRule.Exact("opencode"),
        ProcessNameRule.Exact("opencode-cli"),
        ProcessNameRule.Exact("opencode-desktop")
    ]);

    private readonly IReadOnlyList<OpenCodeConfigurationLayer> _configurationLayers;
    private readonly IReadOnlyList<string> _invalidRelativePathEnvironmentVariables;
    private readonly bool _validateKnownEnvironmentOverrides;
    private readonly Func<string, byte[]?> _readAuthenticationFile;

    public OpenCodeAuthenticationAdapter(
        string? userProfileDirectory = null,
        string? openCodeConfigurationPath = null,
        string? openCodeAuthenticationPath = null)
        : this(
            userProfileDirectory,
            openCodeConfigurationPath,
            openCodeAuthenticationPath,
            validateKnownEnvironmentOverrides: userProfileDirectory is null)
    {
    }

    internal OpenCodeAuthenticationAdapter(
        string? userProfileDirectory,
        string? openCodeConfigurationPath,
        string? openCodeAuthenticationPath,
        bool validateKnownEnvironmentOverrides,
        Func<string, byte[]?>? readAuthenticationFile = null)
    {
        _readAuthenticationFile = readAuthenticationFile ??
            AuthenticationSnapshotFiles.ReadOptionalAuthenticationFile;
        var profileDirectory = ResolveUserProfile(userProfileDirectory);
        var allowPathEnvironmentOverrides = userProfileDirectory is null;
        var invalidRelativePathEnvironmentVariables = new List<string>();
        var configurationRoot = ResolveXdgRoot(
            profileDirectory,
            "XDG_CONFIG_HOME",
            ".config",
            allowPathEnvironmentOverrides,
            invalidRelativePathEnvironmentVariables);
        var configurationDirectory = Path.GetFullPath(Path.Combine(
            configurationRoot,
            "opencode"));
        var layers = new List<OpenCodeConfigurationLayer>
        {
            new("legacy-config-json", Path.Combine(configurationDirectory, "config.json")),
            new("opencode-json", Path.Combine(configurationDirectory, "opencode.json")),
            new("opencode-jsonc", Path.Combine(configurationDirectory, "opencode.jsonc"))
        };
        var customConfigurationPath = ResolveCustomConfigurationPath(
            allowPathEnvironmentOverrides,
            openCodeConfigurationPath,
            invalidRelativePathEnvironmentVariables);
        if (customConfigurationPath is not null)
        {
            var matchingLayer = layers.FindIndex(layer =>
                string.Equals(
                    Path.GetFullPath(layer.FilePath),
                    customConfigurationPath,
                    StringComparison.OrdinalIgnoreCase));
            if (matchingLayer >= 0)
            {
                var configuredLayer = layers[matchingLayer];
                layers.RemoveAt(matchingLayer);
                layers.Add(configuredLayer);
            }
            else
            {
                layers.Add(new OpenCodeConfigurationLayer("custom", customConfigurationPath));
            }
        }

        _configurationLayers = layers.AsReadOnly();
        _validateKnownEnvironmentOverrides = validateKnownEnvironmentOverrides;
        ConfigurationFilePath = _configurationLayers[^1].FilePath;
        var dataRoot = ResolveXdgRoot(
            profileDirectory,
            "XDG_DATA_HOME",
            Path.Combine(".local", "share"),
            allowPathEnvironmentOverrides,
            invalidRelativePathEnvironmentVariables);
        AuthenticationFilePath = !string.IsNullOrWhiteSpace(openCodeAuthenticationPath)
            ? Path.GetFullPath(openCodeAuthenticationPath)
            : Path.GetFullPath(Path.Combine(
                dataRoot,
                "opencode",
                "auth.json"));
        _invalidRelativePathEnvironmentVariables = Array.AsReadOnly(
            invalidRelativePathEnvironmentVariables
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        ManagedFilePaths = Array.AsReadOnly(
            new[] { AuthenticationFilePath }
                .Concat(_configurationLayers.Select(static layer => layer.FilePath))
                .ToArray());
    }

    public AgentProvider Provider => AgentProvider.OpenCode;

    public string DisplayName => "OpenCode";

    public string AuthenticationFilePath { get; }

    public string ConfigurationFilePath { get; }

    public IReadOnlyList<string> ManagedFilePaths { get; }

    public IReadOnlyList<ProcessNameRule> BlockingProcessRules => Processes;

    public bool HasCurrentSnapshot
    {
        get
        {
            EnsureNoKnownUnmanagedEnvironmentOverrides();
            if (HasNonEmptyFile(AuthenticationFilePath))
            {
                return true;
            }

            byte[]? configuration = null;
            try
            {
                configuration = OpenCodeManagedConfiguration.Capture(_configurationLayers);
                return OpenCodeManagedConfiguration.HasManagedValues(configuration);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return false;
            }
            finally
            {
                if (configuration is not null)
                {
                    CryptographicOperations.ZeroMemory(configuration);
                }
            }
        }
    }

    public byte[] ReadSnapshot(bool allowIncomplete = false)
    {
        EnsureNoKnownUnmanagedEnvironmentOverrides();
        return ConsistentAuthenticationSnapshotReader.Read(
            Provider,
            DisplayName,
            AuthenticationFilePath,
            _readAuthenticationFile,
            () => OpenCodeManagedConfiguration.Capture(_configurationLayers),
            OpenCodeManagedConfiguration.HasManagedValues,
            allowIncomplete);
    }

    public void WriteSnapshot(IAtomicFileWriter atomicWriter, byte[] snapshotBytes, Guid transactionId)
    {
        ArgumentNullException.ThrowIfNull(atomicWriter);
        ArgumentNullException.ThrowIfNull(snapshotBytes);
        EnsureNoKnownUnmanagedEnvironmentOverrides();
        if (!AuthenticationSnapshotCodec.TryDecode(snapshotBytes, out var snapshot))
        {
            throw new InvalidDataException("OpenCode profiles require a managed configuration snapshot.");
        }

        try
        {
            if (snapshot!.Provider != Provider ||
                (!snapshot.AuthenticationFileExists &&
                 !OpenCodeManagedConfiguration.HasManagedValues(snapshot.ManagedConfiguration)))
            {
                throw new InvalidDataException("The saved account snapshot is not a valid OpenCode profile.");
            }

            var writes = OpenCodeManagedConfiguration.PrepareWrites(
                _configurationLayers,
                snapshot.ManagedConfiguration);
            try
            {
                atomicWriter.DeleteFile(AuthenticationFilePath);
                foreach (var write in writes)
                {
                    atomicWriter.WriteAllBytes(write.FilePath, write.Contents, transactionId);
                }
            }
            finally
            {
                OpenCodeManagedConfiguration.ZeroWrites(writes);
            }

            AuthenticationSnapshotFiles.ReplaceAuthenticationFile(
                atomicWriter,
                AuthenticationFilePath,
                snapshot.AuthenticationFileExists,
                snapshot.AuthenticationFileContents,
                transactionId);
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(snapshot);
        }
    }

    public bool SnapshotsEqual(byte[] left, byte[] right) =>
        AuthenticationSnapshotComparison.AreEqual(
            Provider,
            left,
            right,
            OpenCodeManagedConfiguration.SnapshotsEqual);

    public bool IsLegacySnapshot(byte[] snapshotBytes)
    {
        ManagedAuthenticationSnapshot? snapshot = null;
        try
        {
            return AuthenticationSnapshotCodec.TryDecode(snapshotBytes, out snapshot) &&
                snapshot!.Provider == Provider &&
                OpenCodeManagedConfiguration.IsLegacySnapshot(snapshot.ManagedConfiguration);
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(snapshot);
        }
    }

    public bool IsRecognizedPartialSnapshot(byte[] source, byte[] target, byte[] current) =>
        AuthenticationSnapshotComparison.IsRecognizedPartial(
            Provider,
            source,
            target,
            current,
            (sourceConfiguration, targetConfiguration, currentConfiguration) =>
                OpenCodeManagedConfiguration.IsRecognizedPartial(
                    sourceConfiguration,
                    targetConfiguration,
                    currentConfiguration,
                    _configurationLayers));

    public void DeleteOwnedTransactionFiles(IAtomicFileWriter atomicWriter, Guid transactionId) =>
        AuthenticationSnapshotComparison.DeleteOwnedTransactionFiles(
            atomicWriter,
            ManagedFilePaths,
            transactionId);

    private void EnsureNoKnownUnmanagedEnvironmentOverrides()
    {
        if (!_validateKnownEnvironmentOverrides)
        {
            return;
        }

        if (_invalidRelativePathEnvironmentVariables.Count > 0)
        {
            throw new InvalidOperationException(
                $"OpenCode account switching is blocked because {string.Join(", ", _invalidRelativePathEnvironmentVariables)} " +
                "uses a relative path. Remove the override or set an absolute path and try again.");
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENCODE_AUTH_CONTENT")))
        {
            throw new InvalidOperationException(
                "OpenCode account switching is blocked while OPENCODE_AUTH_CONTENT is set. " +
                "Remove that inline credential override and try again.");
        }

        var inlineConfiguration = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_CONTENT");
        if (!string.IsNullOrEmpty(inlineConfiguration))
        {
            bool hasManagedValues;
            try
            {
                hasManagedValues = OpenCodeManagedConfiguration.ContainsManagedValues(
                    inlineConfiguration,
                    "OPENCODE_CONFIG_CONTENT");
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidOperationException(
                    "OpenCode account switching is blocked because OPENCODE_CONFIG_CONTENT " +
                    "is not valid JSON or JSONC. Remove or correct that inline override and try again.",
                    exception);
            }

            if (hasManagedValues)
            {
                throw new InvalidOperationException(
                    "OpenCode account switching is blocked because OPENCODE_CONFIG_CONTENT defines " +
                    "provider, model, or small_model. Remove those managed keys and try again.");
            }
        }

        var configuredDirectory = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return;
        }

        if (!Path.IsPathFullyQualified(configuredDirectory))
        {
            throw new InvalidOperationException(
                "OpenCode account switching is blocked because OPENCODE_CONFIG_DIR uses a relative path. " +
                "Remove the override or set an absolute path and try again.");
        }

        string fullDirectory;
        try
        {
            fullDirectory = Path.GetFullPath(configuredDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new InvalidOperationException(
                "OpenCode account switching is blocked because OPENCODE_CONFIG_DIR is not a valid path.",
                exception);
        }

        foreach (var fileName in new[] { "opencode.json", "opencode.jsonc" })
        {
            var candidatePath = Path.Combine(fullDirectory, fileName);
            if (_configurationLayers.Any(layer =>
                    string.Equals(
                        Path.GetFullPath(layer.FilePath),
                        candidatePath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            bool hasManagedValues;
            try
            {
                hasManagedValues = OpenCodeManagedConfiguration.FileContainsManagedValues(candidatePath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                throw new InvalidOperationException(
                    $"OpenCode account switching is blocked because the OPENCODE_CONFIG_DIR file " +
                    $"'{candidatePath}' could not be validated. Correct or remove that override and try again.",
                    exception);
            }

            if (hasManagedValues)
            {
                throw new InvalidOperationException(
                    $"OpenCode account switching is blocked because the OPENCODE_CONFIG_DIR file " +
                    $"'{candidatePath}' defines provider, model, or small_model. " +
                    "Remove those managed keys and try again.");
            }
        }
    }

    private static bool HasNonEmptyFile(string path)
    {
        try
        {
            return new FileInfo(path) is { Exists: true, Length: > 0 };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? ResolveCustomConfigurationPath(
        bool allowEnvironmentOverride,
        string? explicitConfigurationPath,
        ICollection<string> invalidRelativePathEnvironmentVariables)
    {
        var configuredPath = explicitConfigurationPath;
        if (configuredPath is null && allowEnvironmentOverride)
        {
            configuredPath = Environment.GetEnvironmentVariable("OPENCODE_CONFIG");
            if (!string.IsNullOrWhiteSpace(configuredPath) &&
                !Path.IsPathFullyQualified(configuredPath))
            {
                invalidRelativePathEnvironmentVariables.Add("OPENCODE_CONFIG");
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return null;
    }

    private static string ResolveXdgRoot(
        string profileDirectory,
        string environmentVariable,
        string defaultRelativePath,
        bool allowEnvironmentOverride,
        ICollection<string> invalidRelativePathEnvironmentVariables)
    {
        if (allowEnvironmentOverride)
        {
            var configuredRoot = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredRoot))
            {
                if (Path.IsPathFullyQualified(configuredRoot))
                {
                    return Path.GetFullPath(configuredRoot);
                }

                invalidRelativePathEnvironmentVariables.Add(environmentVariable);
            }
        }

        return Path.GetFullPath(Path.Combine(profileDirectory, defaultRelativePath));
    }

    private static string ResolveUserProfile(string? userProfileDirectory)
    {
        var profileDirectory = userProfileDirectory ??
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profileDirectory))
        {
            throw new InvalidOperationException("The Windows user profile directory could not be resolved.");
        }

        return Path.GetFullPath(profileDirectory);
    }
}

internal static class AuthenticationSnapshotComparison
{
    internal static bool AreEqual(
        AgentProvider provider,
        byte[] left,
        byte[] right,
        Func<byte[], byte[], bool>? managedConfigurationEquals = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (AuthenticationSnapshotFiles.BytesEqual(left, right))
        {
            return true;
        }

        ManagedAuthenticationSnapshot? leftSnapshot = null;
        ManagedAuthenticationSnapshot? rightSnapshot = null;
        try
        {
            if (!TryGetComparableSnapshot(provider, left, out leftSnapshot) ||
                !TryGetComparableSnapshot(provider, right, out rightSnapshot))
            {
                return false;
            }

            return leftSnapshot!.AuthenticationFileExists == rightSnapshot!.AuthenticationFileExists &&
                OptionalBytesEqual(
                    leftSnapshot.AuthenticationFileContents,
                    rightSnapshot.AuthenticationFileContents) &&
                (managedConfigurationEquals?.Invoke(
                    leftSnapshot.ManagedConfiguration,
                    rightSnapshot.ManagedConfiguration) ??
                 AuthenticationSnapshotFiles.BytesEqual(
                     leftSnapshot.ManagedConfiguration,
                     rightSnapshot.ManagedConfiguration));
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(leftSnapshot);
            AuthenticationSnapshotCodec.Zero(rightSnapshot);
        }
    }

    internal static void DeleteOwnedTransactionFiles(
        IAtomicFileWriter atomicWriter,
        IReadOnlyList<string> managedFilePaths,
        Guid transactionId)
    {
        foreach (var path in managedFilePaths)
        {
            atomicWriter.DeleteOwnedTransactionFiles(path, transactionId);
        }
    }

    internal static bool IsManagedSnapshot(AgentProvider provider, byte[] snapshotBytes)
    {
        ManagedAuthenticationSnapshot? snapshot = null;
        try
        {
            return AuthenticationSnapshotCodec.TryDecode(snapshotBytes, out snapshot) &&
                snapshot!.Provider == provider;
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(snapshot);
        }
    }

    internal static bool IsRecognizedPartial(
        AgentProvider provider,
        byte[] source,
        byte[] target,
        byte[] current,
        Func<byte[], byte[], byte[], bool>? managedConfigurationIsPartial = null)
    {
        ManagedAuthenticationSnapshot? sourceSnapshot = null;
        ManagedAuthenticationSnapshot? targetSnapshot = null;
        ManagedAuthenticationSnapshot? currentSnapshot = null;
        try
        {
            if (!TryGetComparableSnapshot(provider, source, out sourceSnapshot) ||
                !TryGetComparableSnapshot(provider, target, out targetSnapshot) ||
                !AuthenticationSnapshotCodec.TryDecode(current, out currentSnapshot) ||
                currentSnapshot!.Provider != provider)
            {
                return false;
            }

            var sourceConfiguration = sourceSnapshot!.ManagedConfiguration;
            var targetConfiguration = targetSnapshot!.ManagedConfiguration;
            var currentConfiguration = currentSnapshot.ManagedConfiguration;
            var configurationIsRecognized = managedConfigurationIsPartial?.Invoke(
                sourceConfiguration,
                targetConfiguration,
                currentConfiguration) ??
                (AuthenticationSnapshotFiles.BytesEqual(
                     sourceConfiguration,
                     currentConfiguration) ||
                 AuthenticationSnapshotFiles.BytesEqual(
                     targetConfiguration,
                     currentConfiguration));

            // Fail-closed writes deliberately remove authentication before touching
            // configuration. No other mixed credential/config state is recognized.
            return !currentSnapshot.AuthenticationFileExists &&
                configurationIsRecognized;
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(sourceSnapshot);
            AuthenticationSnapshotCodec.Zero(targetSnapshot);
            AuthenticationSnapshotCodec.Zero(currentSnapshot);
        }
    }

    private static bool OptionalBytesEqual(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null &&
            AuthenticationSnapshotFiles.BytesEqual(left, right);

    private static bool TryGetComparableSnapshot(
        AgentProvider provider,
        byte[] snapshotBytes,
        out ManagedAuthenticationSnapshot? snapshot)
    {
        if (AuthenticationSnapshotCodec.TryDecode(snapshotBytes, out snapshot))
        {
            return snapshot!.Provider == provider;
        }

        byte[] emptyConfiguration;
        switch (provider)
        {
            case AgentProvider.Codex:
                emptyConfiguration = CodexManagedConfiguration.EmptySnapshot();
                break;
            case AgentProvider.ClaudeCode:
                emptyConfiguration = ClaudeManagedConfiguration.EmptySnapshot();
                break;
            default:
                snapshot = null;
                return false;
        }

        snapshot = new ManagedAuthenticationSnapshot
        {
            Provider = provider,
            AuthenticationFileExists = true,
            AuthenticationFileContents = snapshotBytes.ToArray(),
            ManagedConfiguration = emptyConfiguration
        };
        return true;
    }
}

internal static class AuthenticationPathResolution
{
    internal static string ResolveRoot(
        string? userProfileDirectory,
        string? explicitRootDirectory,
        string environmentVariable,
        string defaultDirectoryName)
    {
        var configuredRoot = explicitRootDirectory;
        if (configuredRoot is null && userProfileDirectory is null)
        {
            configuredRoot = Environment.GetEnvironmentVariable(environmentVariable);
        }

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var profileDirectory = userProfileDirectory ??
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profileDirectory))
        {
            throw new InvalidOperationException("The Windows user profile directory could not be resolved.");
        }

        return Path.GetFullPath(Path.Combine(profileDirectory, defaultDirectoryName));
    }
}
