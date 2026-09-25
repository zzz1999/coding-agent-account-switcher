using System.Security.Cryptography;
using System.Text;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class CaptureConsistencyTests
{
    [Theory]
    [InlineData(AgentProvider.ClaudeCode)]
    [InlineData(AgentProvider.OpenCode)]
    public async Task StableCaptureDoesNotInspectProcessesOrChangeProviderFiles(AgentProvider provider)
    {
        using var temporary = new TemporaryDirectory();
        var reads = new List<byte[]>();
        var adapter = CreateAdapter(provider, temporary.Path, path => ReadAndTrack(path, reads));
        WriteAccount(adapter, "stable");
        var before = adapter.ManagedFilePaths.Where(File.Exists)
            .ToDictionary(path => path, File.ReadAllBytes);
        var vault = new AuthenticationProfileVault(Path.Combine(temporary.Path, "vault"));
        var service = CreateService(vault);

        var result = await service.CaptureCurrentLoginAsync(adapter, "Stable");

        Assert.True(result.Status == AccountOperationStatus.Success, result.ErrorMessage);
        Assert.Equal(2, reads.Count);
        AssertZeroed(reads);
        foreach (var (path, contents) in before)
        {
            Assert.Equal(contents, File.ReadAllBytes(path));
        }
        Assert.Equal(before.Count, adapter.ManagedFilePaths.Count(File.Exists));
        var captured = vault.LoadCredential(provider, result.Profile!.ProfileId);
        var expected = CreateAdapter(provider, temporary.Path).ReadSnapshot();
        try
        {
            Assert.True(adapter.SnapshotsEqual(expected, captured));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(captured);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    [Theory]
    [InlineData(AgentProvider.ClaudeCode)]
    [InlineData(AgentProvider.OpenCode)]
    public void AuthenticationChangeBetweenFilesRetriesInsteadOfCapturingMixedAccount(AgentProvider provider)
    {
        using var temporary = new TemporaryDirectory();
        var normalAdapter = CreateAdapter(provider, temporary.Path);
        WriteAccount(normalAdapter, "before");
        var reads = new List<byte[]>();
        var adapter = CreateAdapter(provider, temporary.Path, path =>
        {
            var result = ReadAndTrack(path, reads);
            if (reads.Count == 1)
            {
                WriteAccount(normalAdapter, "after");
            }
            return result;
        });

        var captured = adapter.ReadSnapshot();
        var expected = normalAdapter.ReadSnapshot();
        try
        {
            Assert.Equal(4, reads.Count);
            Assert.True(adapter.SnapshotsEqual(expected, captured));
            AssertZeroed(reads);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(captured);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    [Theory]
    [InlineData(AgentProvider.ClaudeCode)]
    [InlineData(AgentProvider.OpenCode)]
    public void ConfigurationOnlyChangeBetweenPassesRetries(AgentProvider provider)
    {
        using var temporary = new TemporaryDirectory();
        var normalAdapter = CreateAdapter(provider, temporary.Path);
        WriteAccount(normalAdapter, "before");
        var originalAuthentication = File.ReadAllBytes(normalAdapter.AuthenticationFilePath);
        var reads = new List<byte[]>();
        var adapter = CreateAdapter(provider, temporary.Path, path =>
        {
            var result = ReadAndTrack(path, reads);
            if (reads.Count == 2)
            {
                WriteConfiguration(normalAdapter, "after");
            }
            return result;
        });

        var captured = adapter.ReadSnapshot();
        var expected = normalAdapter.ReadSnapshot();
        try
        {
            Assert.Equal(4, reads.Count);
            Assert.True(adapter.SnapshotsEqual(expected, captured));
            Assert.Equal(originalAuthentication, File.ReadAllBytes(adapter.AuthenticationFilePath));
            AssertZeroed(reads);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(captured);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(originalAuthentication);
        }
    }

    [Fact]
    public void OpenCodeRetriesWhenAnOverriddenLayerChangesEvenIfEffectiveValuesStayEqual()
    {
        using var temporary = new TemporaryDirectory();
        var normalAdapter = CreateAdapter(AgentProvider.OpenCode, temporary.Path);
        WriteAccount(normalAdapter, "stable");
        var legacyPath = Path.Combine(Path.GetDirectoryName(ConfigurationPath(normalAdapter))!, "config.json");
        File.WriteAllText(legacyPath, "{\"model\":\"placeholder/before\",\"theme\":\"keep-legacy\"}");
        var reads = new List<byte[]>();
        var adapter = CreateAdapter(AgentProvider.OpenCode, temporary.Path, path =>
        {
            var result = ReadAndTrack(path, reads);
            if (reads.Count == 2)
            {
                File.WriteAllText(legacyPath, "{\"model\":\"placeholder/after\",\"theme\":\"keep-legacy\"}");
            }
            return result;
        });

        var captured = adapter.ReadSnapshot();
        var expected = normalAdapter.ReadSnapshot();
        try
        {
            Assert.Equal(4, reads.Count);
            // Full bytes retain the updated per-layer evidence needed by recovery.
            Assert.Equal(expected, captured);
            AssertZeroed(reads);
            Assert.Contains("keep-legacy", File.ReadAllText(legacyPath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(captured);
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    [Theory]
    [InlineData(AgentProvider.ClaudeCode)]
    [InlineData(AgentProvider.OpenCode)]
    public async Task ContinuousChangesFailAfterThreeAttemptsWithoutSavingToVault(AgentProvider provider)
    {
        using var temporary = new TemporaryDirectory();
        var normalAdapter = CreateAdapter(provider, temporary.Path);
        WriteAccount(normalAdapter, "initial");
        var reads = new List<byte[]>();
        var adapter = CreateAdapter(provider, temporary.Path, path =>
        {
            var result = ReadAndTrack(path, reads);
            WriteAccount(normalAdapter, $"change-{reads.Count}");
            return result;
        });
        var vault = new AuthenticationProfileVault(Path.Combine(temporary.Path, "vault"));

        var result = await CreateService(vault).CaptureCurrentLoginAsync(adapter, "Never stable");

        Assert.Equal(AccountOperationStatus.Failed, result.Status);
        Assert.Contains("changed while", result.ErrorMessage);
        Assert.Equal(6, reads.Count);
        AssertZeroed(reads);
        Assert.Empty(vault.ListProfiles(provider));
        Assert.Null(vault.GetActiveProfile(provider));
        Assert.Null(vault.GetPendingJournal(provider));
        Assert.Equal("dummy-auth-change-6", File.ReadAllText(adapter.AuthenticationFilePath));
        Assert.Contains("keep-unrelated", File.ReadAllText(ConfigurationPath(adapter)));
    }

    [Theory]
    [InlineData(AgentProvider.ClaudeCode)]
    [InlineData(AgentProvider.OpenCode)]
    public void EmptyCaptureRequiresAllowIncompleteAndDoesNotCreateFiles(AgentProvider provider)
    {
        using var temporary = new TemporaryDirectory();
        var reads = 0;
        var adapter = CreateAdapter(provider, temporary.Path, _ =>
        {
            reads++;
            return null;
        });

        Assert.Throws<FileNotFoundException>(() => adapter.ReadSnapshot());
        Assert.Equal(2, reads);
        var captured = adapter.ReadSnapshot(allowIncomplete: true);
        ManagedAuthenticationSnapshot? decoded = null;
        try
        {
            Assert.True(AuthenticationSnapshotCodec.TryDecode(captured, out decoded));
            Assert.False(decoded!.AuthenticationFileExists);
            Assert.Equal(4, reads);
            Assert.All(adapter.ManagedFilePaths, path => Assert.False(File.Exists(path)));
        }
        finally
        {
            AuthenticationSnapshotCodec.Zero(decoded);
            CryptographicOperations.ZeroMemory(captured);
        }
    }

    [Fact]
    public void ConfigurationReadFailureClearsEveryPreviouslyReadBuffer()
    {
        var authentications = new List<byte[]>();
        var configuration = Encoding.UTF8.GetBytes("{\"dummy\":\"configuration\"}");
        var configurationReads = 0;

        Assert.Throws<IOException>(() => ConsistentAuthenticationSnapshotReader.Read(
            AgentProvider.ClaudeCode,
            "Claude Code",
            "synthetic-auth-path",
            _ =>
            {
                var contents = Encoding.UTF8.GetBytes("dummy-auth");
                authentications.Add(contents);
                return contents;
            },
            () => ++configurationReads == 1 ? configuration : throw new IOException("Synthetic read failure"),
            _ => true,
            allowIncomplete: false));

        Assert.Equal(2, authentications.Count);
        AssertZeroed(authentications);
        Assert.All(configuration, value => Assert.Equal((byte)0, value));
    }

    private static IAuthenticationAdapter CreateAdapter(
        AgentProvider provider,
        string userProfileDirectory,
        Func<string, byte[]?>? readAuthenticationFile = null) => provider switch
    {
        AgentProvider.ClaudeCode => new ClaudeCodeAuthenticationAdapter(
            userProfileDirectory,
            claudeConfigDirectory: null,
            manageApiConfiguration: true,
            readAuthenticationFile ?? AuthenticationSnapshotFiles.ReadOptionalAuthenticationFile),
        AgentProvider.OpenCode => new OpenCodeAuthenticationAdapter(
            userProfileDirectory,
            openCodeConfigurationPath: null,
            openCodeAuthenticationPath: null,
            validateKnownEnvironmentOverrides: false,
            readAuthenticationFile),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static byte[]? ReadAndTrack(string path, List<byte[]> reads)
    {
        var contents = AuthenticationSnapshotFiles.ReadOptionalAuthenticationFile(path);
        if (contents is not null)
        {
            reads.Add(contents);
        }
        return contents;
    }

    private static void WriteAccount(IAuthenticationAdapter adapter, string suffix)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, $"dummy-auth-{suffix}");
        WriteConfiguration(adapter, suffix);
    }

    private static void WriteConfiguration(IAuthenticationAdapter adapter, string suffix)
    {
        var path = ConfigurationPath(adapter);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, adapter.Provider == AgentProvider.ClaudeCode
            ? $$"""
              {
                "env": {
                  "ANTHROPIC_BASE_URL": "https://{{suffix}}.invalid",
                  "ANTHROPIC_AUTH_TOKEN": "dummy-token-{{suffix}}",
                  "UNRELATED_ENV": "keep-unrelated"
                },
                "theme": "keep-unrelated"
              }
              """
            : $$"""
              {
                "provider": {
                  "placeholder": {
                    "options": {
                      "baseURL": "https://{{suffix}}.invalid",
                      "apiKey": "dummy-token-{{suffix}}"
                    }
                  }
                },
                "model": "placeholder/{{suffix}}",
                "theme": "keep-unrelated"
              }
              """);
    }

    private static string ConfigurationPath(IAuthenticationAdapter adapter) => adapter switch
    {
        ClaudeCodeAuthenticationAdapter claude => claude.SettingsFilePath,
        OpenCodeAuthenticationAdapter openCode => openCode.ConfigurationFilePath,
        _ => throw new ArgumentOutOfRangeException(nameof(adapter))
    };

    private static void AssertZeroed(IEnumerable<byte[]> buffers) =>
        Assert.All(buffers, buffer => Assert.All(buffer, value => Assert.Equal((byte)0, value)));

    private static AccountSwitchService CreateService(AuthenticationProfileVault vault) => new(
        vault,
        new FailIfInspectedProcessInspector(),
        mutexNamePrefix: $"CaptureConsistencyTests.{Guid.NewGuid():N}");

    private sealed class FailIfInspectedProcessInspector : IProcessInspector
    {
        public ProcessInspectionResult Inspect(IAuthenticationAdapter adapter) =>
            throw new InvalidOperationException("Saving must not inspect provider processes.");
    }
}
