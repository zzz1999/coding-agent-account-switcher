using System.Net;
using System.Text;
using System.Text.Json;
using CodingAgentAccountSwitcher.App;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckAsyncReportsNewerReleaseAndSendsRequiredGitHubHeaders()
    {
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse("0.1.42"));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(0, 1, 41));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new Version(0, 1, 41, 0), result.CurrentVersion);
        Assert.Equal(new Version(0, 1, 42, 0), result.LatestVersion);
        Assert.Equal(GitHubUpdateCheckService.LatestReleasePageUri, result.ReleasePage);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(GitHubUpdateCheckService.LatestReleaseApiUri, handler.RequestUri);
        Assert.Contains("application/vnd.github+json", handler.Accept, StringComparison.Ordinal);
        Assert.Equal("CodingAgentAccountSwitcher/0.1.41", handler.UserAgent);
        Assert.Equal("2026-03-10", handler.ApiVersion);
        Assert.Null(handler.Authorization);
    }

    [Theory]
    [InlineData("0.1.42", 42)]
    [InlineData("0.1.41", 42)]
    public async Task CheckAsyncReportsUpToDateWhenReleaseIsNotNewer(
        string releaseVersion,
        int currentBuild)
    {
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse(releaseVersion));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(0, 1, currentBuild, 0));

        Assert.Equal(UpdateAvailability.UpToDate, result.Availability);
    }

    [Fact]
    public async Task CheckAsyncRejectsUnexpectedTagOrMissingVersionMarker()
    {
        using var wrongTagHandler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse(
            "0.1.42",
            tagName: "v0.1.41"));
        using var wrongTagClient = new HttpClient(wrongTagHandler);
        var wrongTagService = new GitHubUpdateCheckService(wrongTagClient);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => wrongTagService.CheckAsync(new Version(0, 1, 41)));

        using var missingMarkerHandler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new { tag_name = "v0.1.42", body = "Automated Windows build." }),
        });
        using var missingMarkerClient = new HttpClient(missingMarkerHandler);
        var missingMarkerService = new GitHubUpdateCheckService(missingMarkerClient);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => missingMarkerService.CheckAsync(new Version(0, 1, 41)));
    }

    [Fact]
    public async Task CheckAsyncAcceptsLegacyLatestTagDuringVersionedReleaseMigration()
    {
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse(
            "0.1.42",
            tagName: "latest"));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(0, 1, 41));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new Version(0, 1, 42, 0), result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsyncRejectsOversizedResponseEvenWithoutReadingIt()
    {
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[GitHubUpdateCheckService.MaximumResponseBytes + 1]),
        });
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(new Version(0, 1, 41)));
    }

    [Fact]
    public void ParseReleaseVersionRejectsInvalidAndConflictingMarkers()
    {
        Assert.Throws<InvalidDataException>(() => GitHubUpdateCheckService.ParseReleaseVersion(
            "<!-- coding-agent-account-switcher-version: not-a-version -->"));
        Assert.Throws<InvalidDataException>(() => GitHubUpdateCheckService.ParseReleaseVersion(
            "<!-- coding-agent-account-switcher-version: 0.1.41 -->\n" +
            "<!-- coding-agent-account-switcher-version: 0.1.42 -->"));
    }

    [Theory]
    [InlineData(0, 1, 42, 0, "0.1.42")]
    [InlineData(1, 2, 3, 4, "1.2.3.4")]
    public void FormatVersionUsesThreeComponentsUnlessRevisionIsNonzero(
        int major,
        int minor,
        int build,
        int revision,
        string expected)
    {
        Assert.Equal(
            expected,
            GitHubUpdateCheckService.FormatVersion(new Version(major, minor, build, revision)));
    }

    private static HttpResponseMessage CreateReleaseResponse(string version, string? tagName = null) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent(new
            {
                tag_name = tagName ?? $"v{version}",
                body = $"<!-- coding-agent-account-switcher-version: {version} -->\nAutomated Windows build.",
            }),
        };

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string Accept { get; private set; } = string.Empty;

        public string UserAgent { get; private set; } = string.Empty;

        public string? ApiVersion { get; private set; }

        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Method = request.Method;
            RequestUri = request.RequestUri;
            Accept = request.Headers.Accept.ToString();
            UserAgent = request.Headers.UserAgent.ToString();
            ApiVersion = request.Headers.TryGetValues("X-GitHub-Api-Version", out var versions)
                ? versions.Single()
                : null;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(responseFactory(request));
        }
    }
}
