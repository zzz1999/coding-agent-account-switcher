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
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse("1.0.42"));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(1, 0, 41));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new Version(1, 0, 41, 0), result.CurrentVersion);
        Assert.Equal(new Version(1, 0, 42, 0), result.LatestVersion);
        Assert.Equal(GitHubUpdateCheckService.LatestReleasePageUri, result.ReleasePage);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(GitHubUpdateCheckService.LatestReleaseApiUri, handler.RequestUri);
        Assert.Contains("application/vnd.github+json", handler.Accept, StringComparison.Ordinal);
        Assert.Equal("CodingAgentAccountSwitcher/1.0.41", handler.UserAgent);
        Assert.Equal("2026-03-10", handler.ApiVersion);
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task CheckAsyncReportsFirstStableReleaseAsNewerThanPreviewSeries()
    {
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse("1.0.11"));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(0, 1, 10));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new Version(1, 0, 11, 0), result.LatestVersion);
    }

    [Theory]
    [InlineData("1.0.42", 42)]
    [InlineData("1.0.41", 42)]
    public async Task CheckAsyncReportsUpToDateWhenReleaseIsNotNewer(
        string releaseVersion,
        int currentBuild)
    {
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse(releaseVersion));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(1, 0, currentBuild, 0));

        Assert.Equal(UpdateAvailability.UpToDate, result.Availability);
    }

    [Fact]
    public async Task CheckAsyncRejectsMismatchedVersionTagAndMarker()
    {
        using var wrongTagHandler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse(
            "1.0.42",
            tagName: "v1.0.41"));
        using var wrongTagClient = new HttpClient(wrongTagHandler);
        var wrongTagService = new GitHubUpdateCheckService(wrongTagClient);
        await Assert.ThrowsAsync<InvalidDataException>(
            () => wrongTagService.CheckAsync(new Version(1, 0, 41)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CheckAsyncAcceptsVersionedTagWhenReleaseNotesAreUnavailable(
        bool includeBody,
        bool useNullBody)
    {
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = includeBody
                ? JsonContent(new { tag_name = "v1.0.42", body = useNullBody ? null : "Automated Windows build." })
                : JsonContent(new { tag_name = "v1.0.42" }),
        });
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(1, 0, 41));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new Version(1, 0, 42, 0), result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsyncAcceptsLegacyLatestTagDuringVersionedReleaseMigration()
    {
        using var handler = new RecordingHttpMessageHandler(_ => CreateReleaseResponse(
            "1.0.42",
            tagName: "latest"));
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        var result = await service.CheckAsync(new Version(1, 0, 41));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new Version(1, 0, 42, 0), result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsyncRequiresVersionMarkerForLegacyLatestTag()
    {
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new { tag_name = "latest", body = "Automated Windows build." }),
        });
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.CheckAsync(new Version(1, 0, 41)));
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
            () => service.CheckAsync(new Version(1, 0, 41)));
    }

    [Fact]
    public async Task CheckAsyncTimesOutWhenResponseBodyStallsAfterHeaders()
    {
        using var body = new StalledResponseStream();
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var service = new GitHubUpdateCheckService(client, TimeSpan.FromMilliseconds(100));

        var check = service.CheckAsync(new Version(1, 0, 41));
        await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(exception.CancellationToken.IsCancellationRequested);
        Assert.True(body.IsDisposed);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsyncPreservesCallerCancellationWhileReadingResponseBody()
    {
        using var body = new StalledResponseStream();
        using var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        });
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var cancellation = new CancellationTokenSource();
        var service = new GitHubUpdateCheckService(client, TimeSpan.FromSeconds(30));

        var check = service.CheckAsync(new Version(1, 0, 41), cancellation.Token);
        await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(body.IsDisposed);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4294967295)]
    public void ConstructorRejectsInvalidRequestTimeout(double timeoutMilliseconds)
    {
        using var client = new HttpClient();

        Assert.Throws<ArgumentOutOfRangeException>(() => new GitHubUpdateCheckService(
            client,
            TimeSpan.FromMilliseconds(timeoutMilliseconds)));
    }

    [Fact]
    public void ParseReleaseVersionRejectsInvalidAndConflictingMarkers()
    {
        Assert.Throws<InvalidDataException>(() => GitHubUpdateCheckService.ParseReleaseVersion(
            "<!-- coding-agent-account-switcher-version: not-a-version -->"));
        Assert.Throws<InvalidDataException>(() => GitHubUpdateCheckService.ParseReleaseVersion(
            "<!-- coding-agent-account-switcher-version: 1.0.41 -->\n" +
            "<!-- coding-agent-account-switcher-version: 1.0.42 -->"));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("1.0.42")]
    [InlineData("v1.0")]
    [InlineData("v1.0.42.0")]
    [InlineData("v1.0.42-beta")]
    public void ParseVersionedTagRejectsUnsupportedOrNoncanonicalTags(string tagName)
    {
        Assert.Throws<InvalidDataException>(() => GitHubUpdateCheckService.ParseVersionedTag(tagName));
    }

    [Theory]
    [InlineData(1, 0, 42, 0, "1.0.42")]
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
        public int RequestCount { get; private set; }

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
            RequestCount++;
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

    private sealed class StalledResponseStream : Stream
    {
        public TaskCompletionSource ReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
