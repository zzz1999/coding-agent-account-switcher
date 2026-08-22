using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace CodingAgentAccountSwitcher.Core;

internal static class ManagedConfigurationFiles
{
    internal const int MaximumConfigurationSizeBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static byte[]? ReadIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.SequentialScan);
        if (stream.Length > MaximumConfigurationSizeBytes || stream.Length > int.MaxValue)
        {
            throw new InvalidDataException("The managed configuration file is too large.");
        }

        var bytes = new byte[(int)stream.Length];
        try
        {
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    internal static string DecodeUtf8(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)
            ? Encoding.UTF8.Preamble.Length
            : 0;
        return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
    }

    internal static byte[] EncodeUtf8(string value) => StrictUtf8.GetBytes(value);
}

internal static class ClaudeManagedConfiguration
{
    private static readonly string[] ManagedEnvironmentKeys =
    [
        "ANTHROPIC_BASE_URL",
        "ANTHROPIC_API_KEY",
        "ANTHROPIC_AUTH_TOKEN",
        "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC",
        "CLAUDE_CODE_ATTRIBUTION_HEADER"
    ];

    internal static byte[] Capture(string path)
    {
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null || source.Length == 0)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new JsonObject());
        }

        try
        {
            var root = ParseObject(source, "Claude Code settings");
            var captured = new JsonObject();
            var capturedEnvironment = new JsonObject();
            if (root["env"] is JsonObject environment)
            {
                foreach (var key in ManagedEnvironmentKeys)
                {
                    if (environment.TryGetPropertyValue(key, out var value))
                    {
                        capturedEnvironment[key] = value?.DeepClone();
                    }
                }
            }

            if (capturedEnvironment.Count > 0)
            {
                captured["env"] = capturedEnvironment;
            }

            return JsonSerializer.SerializeToUtf8Bytes(captured);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
        }
    }

    internal static byte[] EmptySnapshot() =>
        JsonSerializer.SerializeToUtf8Bytes(new JsonObject());

    internal static byte[]? Merge(string path, byte[] capturedBytes)
    {
        var captured = ParseObject(capturedBytes, "saved Claude Code settings");
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null && captured.Count == 0)
        {
            return null;
        }

        try
        {
            var root = source is null || source.Length == 0
                ? new JsonObject()
                : ParseObject(source, "Claude Code settings");
            var environmentExisted = root["env"] is JsonObject;
            var environment = root["env"] as JsonObject ?? new JsonObject();

            foreach (var key in ManagedEnvironmentKeys)
            {
                environment.Remove(key);
            }

            if (captured["env"] is JsonObject capturedEnvironment)
            {
                foreach (var key in ManagedEnvironmentKeys)
                {
                    if (capturedEnvironment.TryGetPropertyValue(key, out var value))
                    {
                        environment[key] = value?.DeepClone();
                    }
                }
            }

            if (environment.Count > 0 || environmentExisted || captured["env"] is JsonObject)
            {
                root["env"] = environment;
            }

            return SerializeObject(root);
        }
        finally
        {
            if (source is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
            }
        }
    }

    internal static bool HasManagedValues(byte[] capturedBytes) =>
        ParseObject(capturedBytes, "saved Claude Code settings").Count > 0;

    private static JsonObject ParseObject(byte[] bytes, string description) =>
        JsonConfiguration.ParseObject(bytes, description);

    private static byte[] SerializeObject(JsonObject value) => JsonConfiguration.SerializeObject(value);
}

internal sealed record OpenCodeConfigurationLayer(string Identifier, string FilePath);

internal sealed record OpenCodeConfigurationWrite(string FilePath, byte[] Contents);

internal static class OpenCodeManagedConfiguration
{
    private const string SnapshotFormatProperty = "$codingAgentAccountSwitcher";
    private const string SnapshotFormatValue = "opencode-managed-configuration-v2";
    private const string EffectiveProperty = "effective";
    private const string LayersProperty = "layers";

    private static readonly string[] ManagedProperties = ["provider", "model", "small_model"];

    internal static byte[] Capture(IReadOnlyList<OpenCodeConfigurationLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        var effective = new JsonObject();
        var layerStates = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            var state = CaptureLayer(layer.FilePath);
            layerStates.Add(layer.Identifier, state);
            DeepMergeInto(effective, state);
        }

        return SerializeSnapshot(effective, layerStates, layers);
    }

    internal static IReadOnlyList<OpenCodeConfigurationWrite> PrepareWrites(
        IReadOnlyList<OpenCodeConfigurationLayer> layers,
        byte[] capturedBytes)
    {
        ArgumentNullException.ThrowIfNull(layers);
        var snapshot = ParseSnapshot(capturedBytes);
        var normalizedStates = CreateNormalizedStates(snapshot.Effective, layers);
        var writes = new List<OpenCodeConfigurationWrite>();
        try
        {
            foreach (var layer in layers)
            {
                var merged = MergeLayer(layer.FilePath, normalizedStates[layer.Identifier]);
                if (merged is not null)
                {
                    writes.Add(new OpenCodeConfigurationWrite(layer.FilePath, merged));
                }
            }

            return writes;
        }
        catch
        {
            ZeroWrites(writes);
            throw;
        }
    }

    internal static bool HasManagedValues(byte[] capturedBytes) =>
        ParseSnapshot(capturedBytes).Effective.Count > 0;

    internal static bool ContainsManagedValues(string content, string description)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        var bytes = ManagedConfigurationFiles.EncodeUtf8(content);
        try
        {
            var root = JsonConfiguration.ParseObject(bytes, description);
            return ExtractManagedProperties(root).Count > 0;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    internal static bool FileContainsManagedValues(string path) =>
        CaptureLayer(path).Count > 0;

    internal static bool SnapshotsEqual(byte[] left, byte[] right) =>
        JsonNode.DeepEquals(ParseSnapshot(left).Effective, ParseSnapshot(right).Effective);

    internal static bool IsLegacySnapshot(byte[] capturedBytes) =>
        !ParseSnapshot(capturedBytes).HasLayerLayout;

    internal static bool IsRecognizedPartial(
        byte[] sourceBytes,
        byte[] targetBytes,
        byte[] currentBytes,
        IReadOnlyList<OpenCodeConfigurationLayer> layers)
    {
        var source = ParseSnapshot(sourceBytes);
        var target = ParseSnapshot(targetBytes);
        var current = ParseSnapshot(currentBytes);
        var sourceNormalized = CreateNormalizedStates(source.Effective, layers);
        var targetNormalized = CreateNormalizedStates(target.Effective, layers);
        var currentStates = GetCurrentLayerStates(current, layers);
        var sourceCaptured = GetCurrentLayerStates(source, layers);

        foreach (var layer in layers)
        {
            var identifier = layer.Identifier;
            var currentState = currentStates[identifier];
            if (!JsonNode.DeepEquals(currentState, sourceCaptured[identifier]) &&
                !JsonNode.DeepEquals(currentState, sourceNormalized[identifier]) &&
                !JsonNode.DeepEquals(currentState, targetNormalized[identifier]))
            {
                return false;
            }
        }

        return true;
    }

    internal static void ZeroWrites(IEnumerable<OpenCodeConfigurationWrite> writes)
    {
        foreach (var write in writes)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(write.Contents);
        }
    }

    private static JsonObject CaptureLayer(string path)
    {
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null || source.Length == 0)
        {
            return new JsonObject();
        }

        try
        {
            return ExtractManagedProperties(
                JsonConfiguration.ParseObject(source, $"OpenCode configuration at {path}"));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
        }
    }

    private static byte[]? MergeLayer(string path, JsonObject targetState)
    {
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null && targetState.Count == 0)
        {
            return null;
        }

        try
        {
            var root = source is null || source.Length == 0
                ? new JsonObject()
                : JsonConfiguration.ParseObject(source, $"OpenCode configuration at {path}");
            var currentState = ExtractManagedProperties(root);
            if (JsonNode.DeepEquals(currentState, targetState))
            {
                return null;
            }

            foreach (var propertyName in ManagedProperties)
            {
                root.Remove(propertyName);
                if (targetState.TryGetPropertyValue(propertyName, out var value))
                {
                    root[propertyName] = value?.DeepClone();
                }
            }

            return JsonConfiguration.SerializeObject(root);
        }
        finally
        {
            if (source is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
            }
        }
    }

    private static OpenCodeConfigurationSnapshot ParseSnapshot(byte[] capturedBytes)
    {
        var root = JsonConfiguration.ParseObject(capturedBytes, "saved OpenCode configuration");
        if (!root.TryGetPropertyValue(SnapshotFormatProperty, out var formatNode))
        {
            ValidateManagedObject(root, "legacy saved OpenCode configuration");
            return new OpenCodeConfigurationSnapshot(
                ExtractManagedProperties(root),
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                HasLayerLayout: false);
        }

        if (formatNode?.GetValue<string>() != SnapshotFormatValue ||
            root[EffectiveProperty] is not JsonObject effectiveNode ||
            root[LayersProperty] is not JsonObject layersNode)
        {
            throw new InvalidDataException("The saved OpenCode configuration snapshot is invalid.");
        }

        ValidateManagedObject(effectiveNode, "saved OpenCode effective configuration");
        var layerStates = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var entry in layersNode)
        {
            if (entry.Value is not JsonObject layerState)
            {
                throw new InvalidDataException("A saved OpenCode configuration layer is invalid.");
            }

            ValidateManagedObject(layerState, $"saved OpenCode layer {entry.Key}");
            layerStates.Add(entry.Key, (JsonObject)layerState.DeepClone());
        }

        return new OpenCodeConfigurationSnapshot(
            (JsonObject)effectiveNode.DeepClone(),
            layerStates,
            HasLayerLayout: true);
    }

    private static byte[] SerializeSnapshot(
        JsonObject effective,
        IReadOnlyDictionary<string, JsonObject> layerStates,
        IReadOnlyList<OpenCodeConfigurationLayer> layers)
    {
        var serializedLayers = new JsonObject();
        foreach (var layer in layers)
        {
            serializedLayers[layer.Identifier] = layerStates[layer.Identifier].DeepClone();
        }

        return JsonConfiguration.SerializeObject(new JsonObject
        {
            [SnapshotFormatProperty] = SnapshotFormatValue,
            [EffectiveProperty] = effective.DeepClone(),
            [LayersProperty] = serializedLayers
        });
    }

    private static Dictionary<string, JsonObject> CreateNormalizedStates(
        JsonObject effective,
        IReadOnlyList<OpenCodeConfigurationLayer> layers)
    {
        if (layers.Count == 0)
        {
            throw new InvalidOperationException("At least one OpenCode configuration layer is required.");
        }

        var states = layers.ToDictionary(
            static layer => layer.Identifier,
            static _ => new JsonObject(),
            StringComparer.Ordinal);
        states[layers[^1].Identifier] = (JsonObject)effective.DeepClone();
        return states;
    }

    private static Dictionary<string, JsonObject> GetCurrentLayerStates(
        OpenCodeConfigurationSnapshot snapshot,
        IReadOnlyList<OpenCodeConfigurationLayer> layers)
    {
        if (!snapshot.HasLayerLayout)
        {
            return CreateNormalizedStates(snapshot.Effective, layers);
        }

        var states = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            states[layer.Identifier] = snapshot.LayerStates.TryGetValue(layer.Identifier, out var state)
                ? (JsonObject)state.DeepClone()
                : new JsonObject();
        }

        return states;
    }

    private static JsonObject ExtractManagedProperties(JsonObject root)
    {
        var captured = new JsonObject();
        foreach (var propertyName in ManagedProperties)
        {
            if (root.TryGetPropertyValue(propertyName, out var value))
            {
                captured[propertyName] = value?.DeepClone();
            }
        }

        return captured;
    }

    private static void ValidateManagedObject(JsonObject value, string description)
    {
        if (value.Any(entry => !ManagedProperties.Contains(entry.Key, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"The {description} contains unsupported properties.");
        }
    }

    private static void DeepMergeInto(JsonObject destination, JsonObject overlay)
    {
        foreach (var entry in overlay)
        {
            if (entry.Value is JsonObject overlayObject &&
                destination[entry.Key] is JsonObject destinationObject)
            {
                DeepMergeInto(destinationObject, overlayObject);
            }
            else
            {
                destination[entry.Key] = entry.Value?.DeepClone();
            }
        }
    }

    private sealed record OpenCodeConfigurationSnapshot(
        JsonObject Effective,
        IReadOnlyDictionary<string, JsonObject> LayerStates,
        bool HasLayerLayout);
}

internal static class JsonConfiguration
{
    private static readonly JsonNodeOptions NodeOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    internal static JsonObject ParseObject(byte[] bytes, string description)
    {
        try
        {
            return JsonNode.Parse(bytes, NodeOptions, DocumentOptions) as JsonObject ??
                throw new InvalidDataException($"The {description} root must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The {description} file contains invalid JSON.", exception);
        }
    }

    internal static byte[] SerializeObject(JsonObject value)
    {
        var text = value.ToJsonString(WriteOptions) + Environment.NewLine;
        return ManagedConfigurationFiles.EncodeUtf8(text);
    }
}

internal sealed record CodexConfigurationSnapshot
{
    public Dictionary<string, string> TopLevel { get; init; } = new(StringComparer.Ordinal);

    // Kept only so profiles created before the provider-only snapshot format
    // can still be read.
    // New snapshots never capture or restore this user preference.
    public string? ResponsesWebSocketsV2 { get; init; }

    public string? ModelProviderIdentifier { get; init; }

    public string? ModelProviderSections { get; init; }
}

internal static class CodexManagedConfiguration
{
    private const string ResponsesWebSocketsV2Key = "responses_websockets_v2";

    private static readonly string[] ManagedTopLevelKeys =
    [
        "model_provider"
    ];

    // Older releases stored these fields in the managed snapshot. Accept and
    // validate them during migration, but deliberately ignore them when restoring.
    private static readonly string[] RecognizedSnapshotTopLevelKeys =
    [
        "model_provider",
        "openai_base_url",
        "model",
        "review_model",
        "model_reasoning_effort",
        "disable_response_storage"
    ];

    private static readonly HashSet<string> ManagedTopLevelKeySet =
        new(ManagedTopLevelKeys, StringComparer.Ordinal);

    private static readonly HashSet<string> RecognizedSnapshotTopLevelKeySet =
        new(RecognizedSnapshotTopLevelKeys, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SnapshotOptions =
        new(JsonSerializerDefaults.Web);

    internal static byte[] Capture(string path)
    {
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null || source.Length == 0)
        {
            return EmptySnapshot();
        }

        try
        {
            var sourceText = ManagedConfigurationFiles.DecodeUtf8(source);
            var root = ParseToml(sourceText, "Codex configuration");
            var topLevel = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in ManagedTopLevelKeys)
            {
                if (!root.TryGetValue(key, out var value))
                {
                    continue;
                }

                ValidateManagedTopLevelValue(key, value, "Codex configuration");
                topLevel[key] = SerializeValue(value);
            }

            var providerIdentifier = GetProviderIdentifier(root, "Codex configuration");
            var providerSections = CaptureProviderSections(root, providerIdentifier);

            return SerializeSnapshot(new CodexConfigurationSnapshot
            {
                TopLevel = topLevel,
                ModelProviderIdentifier = providerIdentifier,
                ModelProviderSections = providerSections
            });
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
        }
    }

    internal static byte[] EmptySnapshot() =>
        SerializeSnapshot(new CodexConfigurationSnapshot());

    internal static bool HasManagedValues(byte[] capturedBytes) =>
        !IsEmpty(DeserializeAndNormalizeSnapshot(capturedBytes));

    internal static bool SnapshotsEqual(byte[] left, byte[] right)
    {
        try
        {
            var normalizedLeft = DeserializeAndNormalizeSnapshot(left);
            var normalizedRight = DeserializeAndNormalizeSnapshot(right);
            return SnapshotValuesEqual(normalizedLeft, normalizedRight);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    internal static bool IsRecognizedPartial(
        byte[] source,
        byte[] target,
        byte[] current) =>
        SnapshotsEqual(source, current) || SnapshotsEqual(target, current);

    internal static byte[]? Merge(string path, byte[] capturedBytes)
    {
        var captured = DeserializeAndNormalizeSnapshot(capturedBytes);
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null && IsEmpty(captured))
        {
            return null;
        }

        try
        {
            var sourceText = source is null || source.Length == 0
                ? string.Empty
                : ManagedConfigurationFiles.DecodeUtf8(source);
            var sourceDocument = ParseSyntax(sourceText, "Codex configuration");
            var sourceRoot = ParseToml(sourceText, "Codex configuration");
            var currentProviderIdentifier =
                GetProviderIdentifier(sourceRoot, "Codex configuration");
            var fragment = BuildManagedFragment(
                sourceRoot,
                currentProviderIdentifier,
                captured);

            RemoveManagedNodes(sourceDocument);
            var newLine = sourceText.Contains("\r\n", StringComparison.Ordinal)
                ? "\r\n"
                : "\n";
            var fragmentText = SerializeTable(fragment, newLine);
            var fragmentDocument = ParseSyntax(fragmentText, "merged Codex configuration");
            MoveDocumentContents(fragmentDocument, sourceDocument);

            var mergedText = sourceDocument.ToString();
            _ = ParseToml(mergedText, "merged Codex configuration");
            var merged = ManagedConfigurationFiles.EncodeUtf8(mergedText);
            if (merged.Length > ManagedConfigurationFiles.MaximumConfigurationSizeBytes)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(merged);
                throw new InvalidDataException(
                    "The merged Codex configuration is too large.");
            }

            return merged;
        }
        finally
        {
            if (source is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
            }
        }
    }

    private static TomlTable BuildManagedFragment(
        TomlTable sourceRoot,
        string? currentProviderIdentifier,
        CodexConfigurationSnapshot captured)
    {
        var fragment = new TomlTable();
        foreach (var key in ManagedTopLevelKeys)
        {
            if (!captured.TopLevel.TryGetValue(key, out var rawValue))
            {
                continue;
            }

            fragment[key] = ParseManagedTopLevelValue(key, rawValue);
        }

        var providers = CloneOptionalTable(
            sourceRoot,
            "model_providers",
            "Codex configuration") ?? new TomlTable();
        if (currentProviderIdentifier is not null)
        {
            providers.Remove(currentProviderIdentifier);
        }
        if (captured.ModelProviderIdentifier is not null)
        {
            providers.Remove(captured.ModelProviderIdentifier);
            var targetProvider = ReadCapturedProvider(captured);
            if (targetProvider is not null)
            {
                providers[captured.ModelProviderIdentifier] = targetProvider;
            }
        }
        if (providers.Count > 0)
        {
            fragment["model_providers"] = providers;
        }

        return fragment;
    }

    private static CodexConfigurationSnapshot DeserializeAndNormalizeSnapshot(
        byte[] capturedBytes)
    {
        ArgumentNullException.ThrowIfNull(capturedBytes);
        CodexConfigurationSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<CodexConfigurationSnapshot>(
                capturedBytes,
                SnapshotOptions) ?? throw new InvalidDataException(
                "The saved Codex configuration snapshot is empty.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException(
                "The saved Codex configuration snapshot is invalid.");
        }

        if (snapshot.TopLevel is null)
        {
            throw new InvalidDataException(
                "The saved Codex configuration snapshot is invalid.");
        }

        var normalizedTopLevel = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in snapshot.TopLevel)
        {
            if (!RecognizedSnapshotTopLevelKeySet.Contains(entry.Key) ||
                string.IsNullOrWhiteSpace(entry.Value))
            {
                throw new InvalidDataException(
                    "The saved Codex configuration snapshot contains an unsupported setting.");
            }

            var value = ParseManagedTopLevelValue(entry.Key, entry.Value);
            if (ManagedTopLevelKeySet.Contains(entry.Key))
            {
                normalizedTopLevel[entry.Key] = SerializeValue(value);
            }
        }

        var providerIdentifier = normalizedTopLevel.TryGetValue(
            "model_provider",
            out var rawProvider)
            ? ParseStringValue("model_provider", rawProvider)
            : null;
        if (!string.Equals(
                providerIdentifier,
                snapshot.ModelProviderIdentifier,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The saved Codex configuration snapshot has inconsistent provider routing.");
        }

        // Validate the legacy field before dropping it so malformed saved data is
        // still rejected instead of silently accepted.
        if (snapshot.ResponsesWebSocketsV2 is not null)
        {
            _ = ParseBooleanValue(snapshot.ResponsesWebSocketsV2);
        }

        var providerSections = NormalizeProviderSections(
            snapshot.ModelProviderSections,
            providerIdentifier);
        return new CodexConfigurationSnapshot
        {
            TopLevel = normalizedTopLevel,
            ModelProviderIdentifier = providerIdentifier,
            ModelProviderSections = providerSections
        };
    }

    private static string? NormalizeProviderSections(
        string? providerSections,
        string? providerIdentifier)
    {
        if (string.IsNullOrWhiteSpace(providerSections))
        {
            return null;
        }
        if (providerIdentifier is null)
        {
            throw new InvalidDataException(
                "The saved Codex configuration snapshot contains provider settings without a provider.");
        }

        var root = ParseToml(providerSections, "saved Codex provider configuration");
        if (root.Count != 1 ||
            !root.TryGetValue("model_providers", out var providersValue) ||
            providersValue is not TomlTable providers ||
            providers.Count != 1 ||
            !providers.TryGetValue(providerIdentifier, out var providerValue) ||
            providerValue is not TomlTable provider)
        {
            throw new InvalidDataException(
                "The saved Codex provider configuration has an unexpected structure.");
        }

        return SerializeProvider(providerIdentifier, provider);
    }

    private static string? CaptureProviderSections(
        TomlTable root,
        string? providerIdentifier)
    {
        if (providerIdentifier is null)
        {
            return null;
        }

        var providers = GetOptionalTable(
            root,
            "model_providers",
            "Codex configuration");
        if (providers is null ||
            !providers.TryGetValue(providerIdentifier, out var providerValue))
        {
            return null;
        }
        if (providerValue is not TomlTable provider)
        {
            throw new InvalidDataException(
                "The selected Codex model provider must be a TOML table.");
        }

        return SerializeProvider(providerIdentifier, provider);
    }

    private static TomlTable? ReadCapturedProvider(
        CodexConfigurationSnapshot captured)
    {
        if (captured.ModelProviderIdentifier is null ||
            string.IsNullOrWhiteSpace(captured.ModelProviderSections))
        {
            return null;
        }

        var root = ParseToml(
            captured.ModelProviderSections,
            "saved Codex provider configuration");
        var providers = GetRequiredTable(
            root,
            "model_providers",
            "saved Codex provider configuration");
        if (!providers.TryGetValue(
                captured.ModelProviderIdentifier,
                out var providerValue) ||
            providerValue is not TomlTable provider)
        {
            throw new InvalidDataException(
                "The saved Codex provider configuration is missing the selected provider.");
        }

        return CloneTable(provider);
    }

    private static string SerializeProvider(
        string providerIdentifier,
        TomlTable provider)
    {
        var providers = new TomlTable
        {
            [providerIdentifier] = CloneTable(provider)
        };
        var root = new TomlTable
        {
            ["model_providers"] = providers
        };
        return SerializeTable(root, "\n").TrimEnd('\r', '\n');
    }

    private static object ParseManagedTopLevelValue(string key, string rawValue)
    {
        var value = ParseSingleValue(rawValue, "saved Codex configuration snapshot");
        ValidateManagedTopLevelValue(key, value, "saved Codex configuration snapshot");
        return value;
    }

    private static void ValidateManagedTopLevelValue(
        string key,
        object value,
        string description)
    {
        var expected = key == "disable_response_storage"
            ? value is bool
            : value is string;
        if (!expected)
        {
            var expectedType = key == "disable_response_storage"
                ? "boolean"
                : "string";
            throw new InvalidDataException(
                $"The {description} {key} setting must be a TOML {expectedType}.");
        }
    }

    private static string? GetProviderIdentifier(
        TomlTable root,
        string description)
    {
        if (!root.TryGetValue("model_provider", out var providerValue))
        {
            return null;
        }
        if (providerValue is not string providerIdentifier ||
            string.IsNullOrWhiteSpace(providerIdentifier))
        {
            throw new InvalidDataException(
                $"The {description} model_provider setting must be a non-empty TOML string.");
        }

        return providerIdentifier;
    }

    private static string ParseStringValue(string key, string rawValue)
    {
        var value = ParseSingleValue(rawValue, "saved Codex configuration snapshot");
        if (value is not string parsed || string.IsNullOrWhiteSpace(parsed))
        {
            throw new InvalidDataException(
                $"The saved Codex configuration snapshot {key} setting must be a non-empty TOML string.");
        }

        return parsed;
    }

    private static bool ParseBooleanValue(string rawValue)
    {
        var value = ParseSingleValue(rawValue, "saved Codex configuration snapshot");
        if (value is not bool parsed)
        {
            throw new InvalidDataException(
                "The saved Codex responses_websockets_v2 setting must be a TOML boolean.");
        }

        return parsed;
    }

    private static object ParseSingleValue(string rawValue, string description)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidDataException($"The {description} contains an empty value.");
        }

        var root = ParseToml(
            $"snapshot_value = {rawValue}\n",
            description);
        if (root.Count != 1 ||
            !root.TryGetValue("snapshot_value", out var value))
        {
            throw new InvalidDataException(
                $"The {description} contains more than one setting in a saved value.");
        }

        return CloneTomlValue(value);
    }

    private static string SerializeValue(object value)
    {
        var root = new TomlTable
        {
            ["snapshot_value"] = CloneTomlValue(value)
        };
        var document = ParseSyntax(
            SerializeTable(root, "\n"),
            "normalized Codex configuration value");
        if (document.KeyValues.ChildrenCount != 1)
        {
            throw new InvalidDataException(
                "A Codex configuration value could not be normalized.");
        }

        var keyValue = document.KeyValues.GetChild(0);
        return keyValue?.Value?.ToString() ??
            throw new InvalidDataException(
                "A Codex configuration value could not be normalized.");
    }

    private static TomlTable? GetOptionalTable(
        TomlTable root,
        string key,
        string description)
    {
        if (!root.TryGetValue(key, out var value))
        {
            return null;
        }
        if (value is not TomlTable table)
        {
            throw new InvalidDataException(
                $"The {description} {key} setting must be a TOML table.");
        }

        return table;
    }

    private static TomlTable GetRequiredTable(
        TomlTable root,
        string key,
        string description) =>
        GetOptionalTable(root, key, description) ??
        throw new InvalidDataException(
            $"The {description} is missing the {key} table.");

    private static TomlTable? CloneOptionalTable(
        TomlTable root,
        string key,
        string description)
    {
        var table = GetOptionalTable(root, key, description);
        return table is null ? null : CloneTable(table);
    }

    private static TomlTable CloneTable(TomlTable source)
    {
        var clone = new TomlTable();
        foreach (var entry in source.OrderBy(
                     static entry => entry.Key,
                     StringComparer.Ordinal))
        {
            clone[entry.Key] = CloneTomlValue(entry.Value);
        }

        return clone;
    }

    private static object CloneTomlValue(object value) =>
        value switch
        {
            TomlTable table => CloneTable(table),
            TomlArray array => CloneArray(array),
            TomlTableArray tableArray => CloneTableArray(tableArray),
            _ => value
        };

    private static TomlArray CloneArray(TomlArray source)
    {
        var clone = new TomlArray(source.Count);
        foreach (var item in source)
        {
            if (item is null)
            {
                throw new InvalidDataException(
                    "The Codex configuration contains an unsupported null TOML value.");
            }
            clone.Add(CloneTomlValue(item));
        }

        return clone;
    }

    private static TomlTableArray CloneTableArray(TomlTableArray source)
    {
        var clone = new TomlTableArray();
        foreach (var table in source)
        {
            clone.Add(CloneTable(table));
        }

        return clone;
    }

    private static DocumentSyntax ParseSyntax(string text, string description)
    {
        try
        {
            return SyntaxParser.ParseStrict(text, sourceName: "config.toml");
        }
        catch (TomlException)
        {
            throw new InvalidDataException(
                $"The {description} contains invalid TOML.");
        }
    }

    private static TomlTable ParseToml(string text, string description)
    {
        _ = ParseSyntax(text, description);
        try
        {
            return TomlSerializer.Deserialize<TomlTable>(text) ??
                throw new InvalidDataException(
                    $"The {description} root must be a TOML table.");
        }
        catch (TomlException)
        {
            throw new InvalidDataException(
                $"The {description} contains invalid TOML.");
        }
    }

    private static string SerializeTable(TomlTable table, string newLine)
    {
        try
        {
            var options = TomlSerializerOptions.Default with
            {
                NewLine = newLine == "\r\n"
                    ? TomlNewLineKind.CrLf
                    : TomlNewLineKind.Lf
            };
            return TomlSerializer.Serialize(table, options);
        }
        catch (TomlException)
        {
            throw new InvalidDataException(
                "The Codex configuration could not be serialized as TOML.");
        }
    }

    private static void RemoveManagedNodes(DocumentSyntax document)
    {
        for (var index = document.KeyValues.ChildrenCount - 1; index >= 0; index--)
        {
            var keyValue = document.KeyValues.GetChild(index);
            var path = GetKeyPath(keyValue?.Key);
            var removeExactManagedKey =
                path.Count == 1 && ManagedTopLevelKeySet.Contains(path[0]);
            var removeManagedTableRoot =
                path.Count > 0 &&
                path[0] == "model_providers";
            if (removeExactManagedKey || removeManagedTableRoot)
            {
                document.KeyValues.RemoveChildAt(index);
            }
        }

        for (var index = document.Tables.ChildrenCount - 1; index >= 0; index--)
        {
            var table = document.Tables.GetChild(index);
            var path = GetKeyPath(table?.Name);
            if (path.Count > 0 &&
                path[0] == "model_providers")
            {
                document.Tables.RemoveChildAt(index);
            }
        }
    }

    private static IReadOnlyList<string> GetKeyPath(KeySyntax? key)
    {
        if (key?.Key is null)
        {
            return Array.Empty<string>();
        }

        var path = new List<string>(1 + key.DotKeys.ChildrenCount)
        {
            GetKeySegment(key.Key)
        };
        foreach (var dotted in key.DotKeys)
        {
            if (dotted.Key is null)
            {
                return Array.Empty<string>();
            }
            path.Add(GetKeySegment(dotted.Key));
        }

        return path;
    }

    private static string GetKeySegment(BareKeyOrStringValueSyntax key) =>
        key switch
        {
            BareKeySyntax bare => bare.Key?.Text ??
                throw new InvalidDataException("The Codex configuration contains an invalid TOML key."),
            StringValueSyntax text => text.Value ??
                throw new InvalidDataException("The Codex configuration contains an invalid TOML key."),
            _ => throw new InvalidDataException(
                "The Codex configuration contains an unsupported TOML key.")
        };

    private static void MoveDocumentContents(
        DocumentSyntax source,
        DocumentSyntax destination)
    {
        if (source.KeyValues.ChildrenCount > 0 &&
            destination.KeyValues.ChildrenCount > 0)
        {
            // A valid TOML file may end immediately after its last root value.
            // Add a separator before appending the managed root values.
            EnsureEndsWithNewLine(
                destination.KeyValues.GetChild(destination.KeyValues.ChildrenCount - 1));
        }

        while (source.KeyValues.ChildrenCount > 0)
        {
            var child = source.KeyValues.GetChild(0) ??
                throw new InvalidDataException(
                    "The merged Codex configuration contains an invalid root setting.");
            source.KeyValues.RemoveChildAt(0);
            destination.KeyValues.Add(child);
        }

        if (source.Tables.ChildrenCount > 0)
        {
            EnsureDocumentEndsWithNewLine(destination);
        }

        while (source.Tables.ChildrenCount > 0)
        {
            var child = source.Tables.GetChild(0) ??
                throw new InvalidDataException(
                    "The merged Codex configuration contains an invalid table.");
            source.Tables.RemoveChildAt(0);
            destination.Tables.Add(child);
        }
    }

    private static void EnsureDocumentEndsWithNewLine(DocumentSyntax document)
    {
        if (document.Tables.ChildrenCount > 0)
        {
            var table = document.Tables.GetChild(document.Tables.ChildrenCount - 1) ??
                throw new InvalidDataException(
                    "The Codex configuration contains an invalid table.");
            if (table.Items.ChildrenCount > 0)
            {
                EnsureEndsWithNewLine(
                    table.Items.GetChild(table.Items.ChildrenCount - 1));
            }
            else
            {
                table.EndOfLineToken = SyntaxFactory.NewLine();
            }
            return;
        }

        if (document.KeyValues.ChildrenCount > 0)
        {
            EnsureEndsWithNewLine(
                document.KeyValues.GetChild(document.KeyValues.ChildrenCount - 1));
        }
    }

    private static void EnsureEndsWithNewLine(KeyValueSyntax? keyValue)
    {
        if (keyValue is null)
        {
            throw new InvalidDataException(
                "The Codex configuration contains an invalid setting.");
        }
        keyValue.EndOfLineToken = SyntaxFactory.NewLine();
    }

    private static bool SnapshotValuesEqual(
        CodexConfigurationSnapshot left,
        CodexConfigurationSnapshot right)
    {
        if (left.TopLevel.Count != right.TopLevel.Count)
        {
            return false;
        }
        foreach (var entry in left.TopLevel)
        {
            if (!right.TopLevel.TryGetValue(entry.Key, out var rightValue) ||
                !string.Equals(entry.Value, rightValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(
                left.ResponsesWebSocketsV2,
                right.ResponsesWebSocketsV2,
                StringComparison.Ordinal) &&
            string.Equals(
                left.ModelProviderIdentifier,
                right.ModelProviderIdentifier,
                StringComparison.Ordinal) &&
            string.Equals(
                left.ModelProviderSections,
                right.ModelProviderSections,
                StringComparison.Ordinal);
    }

    private static bool IsEmpty(CodexConfigurationSnapshot snapshot) =>
        snapshot.TopLevel.Count == 0 &&
        snapshot.ResponsesWebSocketsV2 is null &&
        string.IsNullOrWhiteSpace(snapshot.ModelProviderSections);

    private static byte[] SerializeSnapshot(CodexConfigurationSnapshot snapshot) =>
        JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotOptions);
}
