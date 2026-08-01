using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    public string? ResponsesWebSocketsV2 { get; init; }

    public string? ModelProviderIdentifier { get; init; }

    public string? ModelProviderSections { get; init; }
}

internal static class CodexManagedConfiguration
{
    private static readonly string[] ManagedTopLevelKeys =
    [
        "model_provider",
        "openai_base_url",
        "model",
        "review_model",
        "model_reasoning_effort",
        "disable_response_storage"
    ];

    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web);

    internal static byte[] Capture(string path)
    {
        var source = ManagedConfigurationFiles.ReadIfExists(path);
        if (source is null || source.Length == 0)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new CodexConfigurationSnapshot(), SnapshotOptions);
        }

        try
        {
            var text = ManagedConfigurationFiles.DecodeUtf8(source);
            var lines = LexLines(text);
            var observedTopLevel = new Dictionary<string, string>(StringComparer.Ordinal);
            string? responsesWebSocketsV2 = null;
            IReadOnlyList<string> tablePath = Array.Empty<string>();

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (TryParseTablePath(line, out var parsedTablePath))
                {
                    tablePath = parsedTablePath;
                    continue;
                }

                if (!TryParseAssignment(line, out var keyPath, out var rawValue))
                {
                    continue;
                }

                if (IsManagedTopLevelAssignment(tablePath, keyPath))
                {
                    var key = keyPath[0];
                    if (observedTopLevel.ContainsKey(key))
                    {
                        throw new InvalidDataException(
                            $"The Codex configuration contains duplicate {key} settings.");
                    }
                    observedTopLevel[key] = CaptureAssignmentValue(lines, index, rawValue);
                }
                else if (IsResponsesWebSocketsAssignment(tablePath, keyPath))
                {
                    if (responsesWebSocketsV2 is not null)
                    {
                        throw new InvalidDataException(
                            "The Codex configuration contains duplicate responses_websockets_v2 settings.");
                    }
                    responsesWebSocketsV2 = CaptureAssignmentValue(lines, index, rawValue);
                }
            }

            var topLevel = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in ManagedTopLevelKeys)
            {
                if (observedTopLevel.TryGetValue(key, out var value))
                {
                    topLevel[key] = value;
                }
            }

            string? providerIdentifier = null;
            if (topLevel.TryGetValue("model_provider", out var rawProvider))
            {
                providerIdentifier = TryParseTomlString(rawProvider) ??
                    throw new InvalidDataException(
                        "The Codex model_provider setting must be a TOML string.");
            }

            var providerSections = providerIdentifier is null
                ? null
                : CaptureProviderSections(lines, providerIdentifier);
            var snapshot = new CodexConfigurationSnapshot
            {
                TopLevel = topLevel,
                ResponsesWebSocketsV2 = responsesWebSocketsV2,
                ModelProviderIdentifier = providerIdentifier,
                ModelProviderSections = providerSections
            };
            return JsonSerializer.SerializeToUtf8Bytes(snapshot, SnapshotOptions);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
        }
    }

    internal static byte[] EmptySnapshot() =>
        JsonSerializer.SerializeToUtf8Bytes(new CodexConfigurationSnapshot(), SnapshotOptions);

    internal static bool HasManagedValues(byte[] capturedBytes)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<CodexConfigurationSnapshot>(
                capturedBytes,
                SnapshotOptions) ?? throw new InvalidDataException(
                    "The saved Codex configuration snapshot is empty.");
            return !IsEmpty(snapshot);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The saved Codex configuration snapshot is invalid.", exception);
        }
    }

    internal static byte[]? Merge(string path, byte[] capturedBytes)
    {
        CodexConfigurationSnapshot captured;
        try
        {
            captured = JsonSerializer.Deserialize<CodexConfigurationSnapshot>(capturedBytes, SnapshotOptions) ??
                throw new InvalidDataException("The saved Codex configuration snapshot is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The saved Codex configuration snapshot is invalid.", exception);
        }

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
            var newLine = DetectNewLine(sourceText);
            var sourceLines = LexLines(sourceText);
            var retained = new List<string>(sourceLines.Count + 16);
            IReadOnlyList<string> tablePath = Array.Empty<string>();
            var skipProviderSection = false;
            var currentProviderIdentifier = FindActiveProviderIdentifier(sourceLines);

            for (var index = 0; index < sourceLines.Count; index++)
            {
                var line = sourceLines[index];
                if (TryParseTablePath(line, out var parsedTablePath))
                {
                    tablePath = parsedTablePath;
                    skipProviderSection =
                        (currentProviderIdentifier is not null &&
                         IsProviderPath(tablePath, currentProviderIdentifier)) ||
                        (captured.ModelProviderIdentifier is not null &&
                         IsProviderPath(tablePath, captured.ModelProviderIdentifier));
                    if (skipProviderSection)
                    {
                        continue;
                    }
                }
                else if (skipProviderSection)
                {
                    continue;
                }

                if (TryParseAssignment(line, out var keyPath, out _) &&
                    (IsManagedTopLevelAssignment(tablePath, keyPath) ||
                     IsResponsesWebSocketsAssignment(tablePath, keyPath)))
                {
                    while (index + 1 < sourceLines.Count &&
                        !sourceLines[index + 1].CanStartStatement)
                    {
                        index++;
                    }
                    continue;
                }

                retained.Add(line.Text);
            }

            var topLevelLines = new List<string>();
            foreach (var key in ManagedTopLevelKeys)
            {
                if (captured.TopLevel.TryGetValue(key, out var value))
                {
                    topLevelLines.Add($"{key} = {value.Replace("\n", newLine, StringComparison.Ordinal)}");
                }
            }

            if (topLevelLines.Count > 0)
            {
                var firstTable = FindFirstTableIndex(retained);
                if (firstTable < 0)
                {
                    firstTable = retained.Count;
                }

                retained.InsertRange(firstTable, topLevelLines);
            }

            if (captured.ResponsesWebSocketsV2 is not null)
            {
                var featuresHeader = FindTableIndex(retained, "features");
                if (featuresHeader >= 0)
                {
                    retained.Insert(featuresHeader + 1,
                        $"responses_websockets_v2 = {captured.ResponsesWebSocketsV2.Replace("\n", newLine, StringComparison.Ordinal)}");
                }
                else
                {
                    var firstTable = FindFirstTableIndex(retained);
                    if (firstTable < 0)
                    {
                        firstTable = retained.Count;
                    }

                    // A root dotted key remains valid even when the source already
                    // defines other feature values with dotted keys. Appending a new
                    // [features] header could otherwise redefine that implicit table.
                    retained.Insert(
                        firstTable,
                        $"features.responses_websockets_v2 = {captured.ResponsesWebSocketsV2.Replace("\n", newLine, StringComparison.Ordinal)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(captured.ModelProviderSections))
            {
                AppendSeparated(retained, captured.ModelProviderSections!.TrimEnd('\r', '\n'));
            }

            var mergedText = string.Join(newLine, retained);
            if (retained.Count > 0 && !mergedText.EndsWith(newLine, StringComparison.Ordinal))
            {
                mergedText += newLine;
            }

            return ManagedConfigurationFiles.EncodeUtf8(mergedText);
        }
        finally
        {
            if (source is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
            }
        }
    }

    private static bool IsEmpty(CodexConfigurationSnapshot snapshot) =>
        snapshot.TopLevel.Count == 0 && snapshot.ResponsesWebSocketsV2 is null &&
        string.IsNullOrWhiteSpace(snapshot.ModelProviderSections);

    private static string? CaptureProviderSections(
        IReadOnlyList<LexedTomlLine> lines,
        string providerIdentifier)
    {
        var captured = new List<string>();
        var inProviderSection = false;
        foreach (var line in lines)
        {
            if (TryParseTablePath(line, out var tablePath))
            {
                inProviderSection = IsProviderPath(tablePath, providerIdentifier);
            }

            if (inProviderSection)
            {
                captured.Add(line.Text);
            }
        }

        return captured.Count == 0
            ? null
            : string.Join("\n", captured).TrimEnd('\r', '\n');
    }

    private static bool IsProviderPath(IReadOnlyList<string> path, string providerIdentifier) =>
        path.Count >= 2 && path[0] == "model_providers" && path[1] == providerIdentifier;

    private static string CaptureAssignmentValue(
        IReadOnlyList<LexedTomlLine> lines,
        int assignmentIndex,
        string firstLineValue)
    {
        var valueLines = new List<string> { firstLineValue };
        for (var index = assignmentIndex + 1;
             index < lines.Count && !lines[index].CanStartStatement;
             index++)
        {
            valueLines.Add(lines[index].Text);
        }

        return string.Join("\n", valueLines).TrimEnd('\r', '\n');
    }

    private static string? FindActiveProviderIdentifier(IReadOnlyList<LexedTomlLine> lines)
    {
        IReadOnlyList<string> tablePath = Array.Empty<string>();
        string? providerIdentifier = null;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (TryParseTablePath(line, out var parsedTablePath))
            {
                tablePath = parsedTablePath;
                continue;
            }

            if (tablePath.Count == 0 &&
                TryParseAssignment(line, out var keyPath, out var rawValue) &&
                keyPath.Count == 1 && keyPath[0] == "model_provider")
            {
                if (providerIdentifier is not null)
                {
                    throw new InvalidDataException(
                        "The Codex configuration contains duplicate model_provider settings.");
                }
                providerIdentifier = TryParseTomlString(
                    CaptureAssignmentValue(lines, index, rawValue)) ??
                    throw new InvalidDataException(
                        "The Codex model_provider setting must be a TOML string.");
            }
        }

        return providerIdentifier;
    }

    private static int FindFirstTableIndex(IReadOnlyList<string> lines)
    {
        var lexed = LexLines(string.Join("\n", lines));
        for (var index = 0; index < lexed.Count; index++)
        {
            if (TryParseTablePath(lexed[index], out _))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindTableIndex(IReadOnlyList<string> lines, string tableName)
    {
        var lexed = LexLines(string.Join("\n", lines));
        for (var index = 0; index < lexed.Count; index++)
        {
            if (TryParseTablePath(lexed[index], out var path) &&
                path.Count == 1 && path[0] == tableName)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseAssignment(
        LexedTomlLine line,
        out IReadOnlyList<string> keyPath,
        out string rawValue)
    {
        keyPath = Array.Empty<string>();
        rawValue = string.Empty;
        if (!line.CanStartStatement)
        {
            return false;
        }

        var trimmed = line.Text.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return false;
        }

        var equalsIndex = FindAssignmentSeparator(trimmed);
        if (equalsIndex <= 0)
        {
            return false;
        }

        var candidate = trimmed[..equalsIndex].Trim();
        if (!TryParseDottedKey(candidate, out keyPath))
        {
            return false;
        }

        rawValue = trimmed[(equalsIndex + 1)..].Trim();
        return rawValue.Length > 0;
    }

    private static bool IsManagedTopLevelAssignment(
        IReadOnlyList<string> tablePath,
        IReadOnlyList<string> keyPath) =>
        tablePath.Count == 0 && keyPath.Count == 1 &&
        ManagedTopLevelKeys.Contains(keyPath[0], StringComparer.Ordinal);

    private static bool IsResponsesWebSocketsAssignment(
        IReadOnlyList<string> tablePath,
        IReadOnlyList<string> keyPath) =>
        (tablePath.Count == 1 && tablePath[0] == "features" &&
         keyPath.Count == 1 && keyPath[0] == "responses_websockets_v2") ||
        (tablePath.Count == 0 && keyPath.Count == 2 &&
         keyPath[0] == "features" && keyPath[1] == "responses_websockets_v2");

    private static int FindAssignmentSeparator(string value)
    {
        var quote = '\0';
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (quote == '"' && character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character is '"' or '\'')
            {
                quote = quote == '\0' ? character : quote == character ? '\0' : quote;
            }
            else if (character == '=' && quote == '\0')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseDottedKey(string value, out IReadOnlyList<string> path)
    {
        path = Array.Empty<string>();
        var rawSegments = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var escaped = false;
        foreach (var character in value)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (quote == '"' && character == '\\')
            {
                current.Append(character);
                escaped = true;
                continue;
            }

            if (character is '"' or '\'')
            {
                quote = quote == '\0' ? character : quote == character ? '\0' : quote;
                current.Append(character);
                continue;
            }

            if (character == '.' && quote == '\0')
            {
                rawSegments.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        if (quote != '\0' || escaped)
        {
            return false;
        }

        rawSegments.Add(current.ToString().Trim());
        var segments = new List<string>(rawSegments.Count);
        foreach (var rawSegment in rawSegments)
        {
            if (!TryParseKeySegment(rawSegment, out var segment))
            {
                return false;
            }

            segments.Add(segment);
        }

        path = segments;
        return segments.Count > 0;
    }

    private static bool TryParseKeySegment(string value, out string segment)
    {
        segment = string.Empty;
        if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
        {
            segment = UnquoteTomlKey(value);
            return true;
        }

        if (value.Length == 0 || value.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
        {
            return false;
        }

        segment = value;
        return true;
    }

    private static bool TryParseTablePath(
        LexedTomlLine line,
        out IReadOnlyList<string> path)
    {
        path = Array.Empty<string>();
        if (!line.CanStartStatement)
        {
            return false;
        }

        var trimmed = line.Text.Trim();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        var arrayTable = trimmed.StartsWith("[[", StringComparison.Ordinal);
        var openingLength = arrayTable ? 2 : 1;
        var closingToken = arrayTable ? "]]" : "]";
        var closingIndex = trimmed.IndexOf(
            closingToken,
            openingLength,
            StringComparison.Ordinal);
        if (closingIndex <= openingLength)
        {
            return false;
        }

        var content = trimmed[openingLength..closingIndex];
        var segments = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var escaped = false;
        foreach (var character in content)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (quote == '"' && character == '\\')
            {
                escaped = true;
                current.Append(character);
                continue;
            }

            if (character is '"' or '\'')
            {
                if (quote == '\0')
                {
                    quote = character;
                }
                else if (quote == character)
                {
                    quote = '\0';
                }
                current.Append(character);
                continue;
            }

            if (character == '.' && quote == '\0')
            {
                segments.Add(UnquoteTomlKey(current.ToString().Trim()));
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        segments.Add(UnquoteTomlKey(current.ToString().Trim()));
        if (segments.Any(static segment => segment.Length == 0))
        {
            return false;
        }

        path = segments;
        return true;
    }

    private static string UnquoteTomlKey(string value)
    {
        if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
        {
            if (value[0] == '\'')
            {
                return value[1..^1];
            }

            try
            {
                return JsonSerializer.Deserialize<string>(value) ?? string.Empty;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The Codex configuration contains an invalid quoted TOML key.", exception);
            }
        }

        return value;
    }

    private static string? TryParseTomlString(string rawValue)
    {
        var value = StripTomlComment(rawValue).Trim();
        if (value.Length < 2 || value[0] != value[^1] || value[0] is not ('"' or '\''))
        {
            return null;
        }

        return UnquoteTomlKey(value);
    }

    private static string StripTomlComment(string value)
    {
        var quote = '\0';
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (quote == '"' && character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character is '"' or '\'')
            {
                quote = quote == '\0' ? character : quote == character ? '\0' : quote;
            }
            else if (character == '#' && quote == '\0')
            {
                return value[..index];
            }
        }

        return value;
    }

    private static List<LexedTomlLine> LexLines(string value)
    {
        var lines = SplitLines(value);
        var result = new List<LexedTomlLine>(lines.Count);
        var state = new TomlLexicalState();
        foreach (var line in lines)
        {
            var canStartStatement = state.CanStartStatement;
            result.Add(new LexedTomlLine(line, canStartStatement));
            AdvanceLexicalState(line, state);
        }

        if (!state.CanStartStatement)
        {
            throw new InvalidDataException(
                "The Codex configuration contains an unterminated TOML value.");
        }

        return result;
    }

    private static void AdvanceLexicalState(string line, TomlLexicalState state)
    {
        for (var index = 0; index < line.Length;)
        {
            if (state.InMultilineBasicString)
            {
                if (StartsWithDelimiter(line, index, "\"\"\"") && !IsEscaped(line, index))
                {
                    state.InMultilineBasicString = false;
                    index += 3;
                }
                else
                {
                    index++;
                }
                continue;
            }

            if (state.InMultilineLiteralString)
            {
                if (StartsWithDelimiter(line, index, "'''"))
                {
                    state.InMultilineLiteralString = false;
                    index += 3;
                }
                else
                {
                    index++;
                }
                continue;
            }

            var character = line[index];
            if (character == '#')
            {
                return;
            }

            if (StartsWithDelimiter(line, index, "\"\"\""))
            {
                state.InMultilineBasicString = true;
                index += 3;
                continue;
            }

            if (StartsWithDelimiter(line, index, "'''"))
            {
                state.InMultilineLiteralString = true;
                index += 3;
                continue;
            }

            if (character == '"')
            {
                index = SkipBasicString(line, index + 1);
                continue;
            }

            if (character == '\'')
            {
                var closingIndex = line.IndexOf('\'', index + 1);
                if (closingIndex < 0)
                {
                    throw new InvalidDataException(
                        "The Codex configuration contains an unterminated literal string.");
                }
                index = closingIndex + 1;
                continue;
            }

            switch (character)
            {
                case '[':
                    state.SquareBracketDepth++;
                    break;
                case ']':
                    if (state.SquareBracketDepth == 0)
                    {
                        throw new InvalidDataException(
                            "The Codex configuration contains an unmatched closing bracket.");
                    }
                    state.SquareBracketDepth--;
                    break;
                case '{':
                    state.CurlyBracketDepth++;
                    break;
                case '}':
                    if (state.CurlyBracketDepth == 0)
                    {
                        throw new InvalidDataException(
                            "The Codex configuration contains an unmatched closing brace.");
                    }
                    state.CurlyBracketDepth--;
                    break;
            }

            index++;
        }
    }

    private static int SkipBasicString(string line, int index)
    {
        var escaped = false;
        for (; index < line.Length; index++)
        {
            var character = line[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
            }
            else if (character == '"')
            {
                return index + 1;
            }
        }

        throw new InvalidDataException(
            "The Codex configuration contains an unterminated basic string.");
    }

    private static bool StartsWithDelimiter(string line, int index, string delimiter) =>
        index + delimiter.Length <= line.Length &&
        line.AsSpan(index, delimiter.Length).SequenceEqual(delimiter);

    private static bool IsEscaped(string line, int index)
    {
        var backslashCount = 0;
        for (var cursor = index - 1; cursor >= 0 && line[cursor] == '\\'; cursor--)
        {
            backslashCount++;
        }

        return backslashCount % 2 != 0;
    }

    private static List<string> SplitLines(string value) => value
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n')
        .ToList();

    private static string DetectNewLine(string value) =>
        value.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static void AppendSeparated(List<string> lines, string content)
    {
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        var contentLines = SplitLines(content);
        if (contentLines.Count > 0 && contentLines[^1].Length == 0)
        {
            contentLines.RemoveAt(contentLines.Count - 1);
        }
        lines.AddRange(contentLines);
    }

    private readonly record struct LexedTomlLine(string Text, bool CanStartStatement);

    private sealed class TomlLexicalState
    {
        public bool InMultilineBasicString { get; set; }

        public bool InMultilineLiteralString { get; set; }

        public int SquareBracketDepth { get; set; }

        public int CurlyBracketDepth { get; set; }

        public bool CanStartStatement => !InMultilineBasicString &&
            !InMultilineLiteralString && SquareBracketDepth == 0 && CurlyBracketDepth == 0;
    }
}
