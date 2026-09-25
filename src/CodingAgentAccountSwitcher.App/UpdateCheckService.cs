using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CodingAgentAccountSwitcher.App;

internal enum UpdateAvailability
{
    UpToDate,
    UpdateAvailable,
}

internal sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    UpdateAvailability Availability,
    Uri ReleasePage);

internal interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
}

internal sealed class GitHubUpdateCheckService : IUpdateCheckService
{
    internal const string ReleaseVersionMarkerPrefix =
        "<!-- coding-agent-account-switcher-version: ";
    internal const string ReleaseVersionMarkerSuffix = " -->";
    internal const int MaximumResponseBytes = 128 * 1024;

    internal static readonly Uri LatestReleaseApiUri = new(
        "https://api.github.com/repos/zzz1999/coding-agent-account-switcher/releases/latest",
        UriKind.Absolute);
    internal static readonly Uri LatestReleasePageUri = new(
        "https://github.com/zzz1999/coding-agent-account-switcher/releases/latest",
        UriKind.Absolute);

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;

    public GitHubUpdateCheckService()
        : this(new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
    {
    }

    internal GitHubUpdateCheckService(HttpClient httpClient, TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var effectiveTimeout = requestTimeout ?? TimeSpan.FromSeconds(12);
        if (effectiveTimeout <= TimeSpan.Zero ||
            effectiveTimeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }

        _httpClient = httpClient;
        _requestTimeout = effectiveTimeout;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        // HttpClient.Timeout stops at the headers with ResponseHeadersRead.
        // Keep one deadline alive until the bounded body has also been read.
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCancellation.CancelAfter(_requestTimeout);
        try
        {
            var result = await CheckCoreAsync(currentVersion, requestCancellation.Token);
            requestCancellation.Token.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Preserve the caller's cancellation token, rather than exposing
            // the linked deadline token as if the caller had timed out.
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<UpdateCheckResult> CheckCoreAsync(
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        var normalizedCurrentVersion = NormalizeVersion(currentVersion);

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd(
            $"CodingAgentAccountSwitcher/{FormatVersion(normalizedCurrentVersion)}");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new InvalidDataException("The GitHub release response is unexpectedly large.");
        }

        var responseBytes = await ReadBoundedResponseAsync(response.Content, cancellationToken);
        using var document = JsonDocument.Parse(responseBytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("tag_name", out var tagElement) ||
            tagElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("The GitHub release metadata is incomplete or unexpected.");
        }

        var tagName = tagElement.GetString()!;
        var releaseBody = TryGetReleaseBody(root);
        Version latestVersion;
        if (string.Equals(tagName, "latest", StringComparison.Ordinal))
        {
            // Legacy rolling Releases have no version in their tag, so their
            // machine-readable note marker remains required during migration.
            latestVersion = ParseReleaseVersion(releaseBody ?? string.Empty);
        }
        else
        {
            latestVersion = ParseVersionedTag(tagName);

            // GitHub can briefly expose a newly published versioned Release
            // before its notes are available. The immutable vX.Y.Z tag is
            // sufficient in that state; when a marker is present, verify it.
            if (releaseBody is not null &&
                TryParseReleaseVersion(releaseBody, out var markedVersion) &&
                markedVersion != latestVersion)
            {
                throw new InvalidDataException(
                    "The GitHub release tag does not match its version marker.");
            }
        }

        return new UpdateCheckResult(
            normalizedCurrentVersion,
            latestVersion,
            latestVersion > normalizedCurrentVersion
                ? UpdateAvailability.UpdateAvailable
                : UpdateAvailability.UpToDate,
            LatestReleasePageUri);
    }

    internal static Version ParseReleaseVersion(string releaseBody)
    {
        ArgumentNullException.ThrowIfNull(releaseBody);
        return TryParseReleaseVersion(releaseBody, out var parsedVersion)
            ? parsedVersion
            : throw new InvalidDataException(
                "The GitHub release does not contain an application version marker.");
    }

    internal static Version ParseVersionedTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!tagName.StartsWith('v') ||
            !Version.TryParse(tagName.AsSpan(1), out var parsedVersion) ||
            parsedVersion.Build < 0)
        {
            throw new InvalidDataException("The GitHub release tag is not a supported version tag.");
        }

        var normalizedVersion = NormalizeVersion(parsedVersion);
        if (!string.Equals(
                tagName,
                $"v{FormatVersion(normalizedVersion)}",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The GitHub release tag is not in canonical version format.");
        }

        return normalizedVersion;
    }

    internal static bool TryParseReleaseVersion(string releaseBody, out Version parsedVersion)
    {
        ArgumentNullException.ThrowIfNull(releaseBody);
        Version? discoveredVersion = null;
        foreach (var rawLine in releaseBody.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(ReleaseVersionMarkerPrefix, StringComparison.Ordinal) ||
                !line.EndsWith(ReleaseVersionMarkerSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var versionText = line[
                ReleaseVersionMarkerPrefix.Length..^ReleaseVersionMarkerSuffix.Length].Trim();
            if (!Version.TryParse(versionText, out var candidate) || candidate.Build < 0)
            {
                throw new InvalidDataException("The GitHub release version marker is invalid.");
            }

            var normalizedCandidate = NormalizeVersion(candidate);
            if (discoveredVersion is not null && discoveredVersion != normalizedCandidate)
            {
                throw new InvalidDataException("The GitHub release contains conflicting version markers.");
            }

            discoveredVersion = normalizedCandidate;
        }

        if (discoveredVersion is null)
        {
            parsedVersion = default!;
            return false;
        }

        parsedVersion = discoveredVersion;
        return true;
    }

    internal static Version NormalizeVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new Version(
            version.Major,
            version.Minor,
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }

    internal static string FormatVersion(Version version)
    {
        var normalized = NormalizeVersion(version);
        return normalized.Revision == 0
            ? normalized.ToString(3)
            : normalized.ToString(4);
    }

    private static async Task<byte[]> ReadBoundedResponseAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaximumResponseBytes)
            {
                throw new InvalidDataException("The GitHub release response is unexpectedly large.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }

    private static string? TryGetReleaseBody(JsonElement root)
    {
        if (!root.TryGetProperty("body", out var bodyElement) ||
            bodyElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return bodyElement.ValueKind == JsonValueKind.String
            ? bodyElement.GetString()
            : throw new InvalidDataException("The GitHub release body has an unexpected format.");
    }
}
