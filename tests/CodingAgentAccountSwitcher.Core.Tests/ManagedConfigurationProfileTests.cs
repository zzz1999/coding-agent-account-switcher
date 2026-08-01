using System.Text.Json.Nodes;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class ManagedConfigurationProfileTests
{
    [Fact]
    public void ClaudeSettingsOnlySnapshotSwitchesManagedEnvironmentAndPreservesEverythingElse()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new ClaudeCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.SettingsFilePath)!);
        File.WriteAllText(adapter.SettingsFilePath, """
            {
              "$schema": "https://json.schemastore.org/claude-code-settings.json",
              "env": {
                "ANTHROPIC_BASE_URL": "https://api-a.invalid",
                "ANTHROPIC_AUTH_TOKEN": "dummy-claude-token-a",
                "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1",
                "UNRELATED_ENV": "keep-original"
              },
              "permissions": { "allow": ["Read"] }
            }
            """);

        var savedSnapshot = adapter.ReadSnapshot();
        try
        {
            Assert.False(File.Exists(adapter.AuthenticationFilePath));
            File.WriteAllText(adapter.SettingsFilePath, """
                {
                  "env": {
                    "ANTHROPIC_BASE_URL": "https://api-b.invalid",
                    "ANTHROPIC_API_KEY": "dummy-claude-key-b",
                    "UNRELATED_ENV": "keep-current"
                  },
                  "permissions": { "allow": ["Write"] },
                  "theme": "dark"
                }
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), savedSnapshot, Guid.NewGuid());

            var root = ParseObject(adapter.SettingsFilePath);
            var environment = Assert.IsType<JsonObject>(root["env"]);
            Assert.Equal("https://api-a.invalid", environment["ANTHROPIC_BASE_URL"]!.GetValue<string>());
            Assert.Equal("dummy-claude-token-a", environment["ANTHROPIC_AUTH_TOKEN"]!.GetValue<string>());
            Assert.Null(environment["ANTHROPIC_API_KEY"]);
            Assert.Equal("keep-current", environment["UNRELATED_ENV"]!.GetValue<string>());
            Assert.Equal("dark", root["theme"]!.GetValue<string>());
            Assert.Equal("Write", root["permissions"]!["allow"]![0]!.GetValue<string>());
            Assert.False(File.Exists(adapter.AuthenticationFilePath));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(savedSnapshot);
        }
    }

    [Fact]
    public void CodexSnapshotSwitchesApiAndModelFieldsWhilePreservingGlobalConfiguration()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-openai-key-a\"}");
        File.WriteAllText(adapter.ConfigurationFilePath, """
            model_provider = "ProviderA"
            model = "model-a"
            review_model = "review-a"
            model_reasoning_effort = "xhigh"
            disable_response_storage = true
            network_access = "enabled"
            windows_wsl_setup_acknowledged = true

            [model_providers.ProviderA]
            name = "Provider A"
            base_url = "https://api-a.invalid"
            wire_api = "responses"
            requires_openai_auth = false

            [features]
            responses_websockets_v2 = true
            goals = true

            [mcp_servers.keep]
            command = "keep-original"
            """);

        var savedSnapshot = adapter.ReadSnapshot();
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-openai-key-b\"}");
            File.WriteAllText(adapter.ConfigurationFilePath, """
                model_provider = "ProviderB"
                model = "model-b"
                network_access = "restricted"
                windows_wsl_setup_acknowledged = false

                [model_providers.ProviderB]
                name = "Provider B"
                base_url = "https://api-b.invalid"
                http_headers = { Authorization = "dummy-provider-b-header" }

                [model_providers.ProviderA]
                name = "Stale Provider A"
                base_url = "https://stale-api-a.invalid"
                http_headers = { Authorization = "dummy-stale-provider-a-header" }

                [model_providers.Dormant]
                name = "Dormant Provider"
                base_url = "https://dormant.invalid"

                [features]
                responses_websockets_v2 = false
                goals = false

                [mcp_servers.keep]
                command = "keep-current"
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), savedSnapshot, Guid.NewGuid());

            Assert.Contains("dummy-openai-key-a", File.ReadAllText(adapter.AuthenticationFilePath));
            var merged = File.ReadAllText(adapter.ConfigurationFilePath);
            Assert.Contains("model_provider = \"ProviderA\"", merged);
            Assert.Contains("model = \"model-a\"", merged);
            Assert.Contains("review_model = \"review-a\"", merged);
            Assert.Contains("disable_response_storage = true", merged);
            Assert.Contains("[model_providers.ProviderA]", merged);
            Assert.Contains("base_url = \"https://api-a.invalid\"", merged);
            Assert.Equal(
                1,
                CountOccurrences(merged, "[model_providers.ProviderA]"));
            Assert.DoesNotContain("https://api-b.invalid", merged);
            Assert.DoesNotContain("dummy-provider-b-header", merged);
            Assert.DoesNotContain("https://stale-api-a.invalid", merged);
            Assert.DoesNotContain("dummy-stale-provider-a-header", merged);
            Assert.Contains("[model_providers.Dormant]", merged);
            Assert.Contains("https://dormant.invalid", merged);
            Assert.Contains("responses_websockets_v2 = true", merged);
            Assert.Contains("network_access = \"restricted\"", merged);
            Assert.Contains("windows_wsl_setup_acknowledged = false", merged);
            Assert.Contains("goals = false", merged);
            Assert.Contains("command = \"keep-current\"", merged);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(savedSnapshot);
        }
    }

    [Fact]
    public void OpenCodeSnapshotSwitchesProviderAndModelsWhilePreservingOtherOptions()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.ConfigurationFilePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-opencode-auth-a\"}}");
        File.WriteAllText(adapter.ConfigurationFilePath, """
            {
              "$schema": "https://opencode.ai/config.json",
              "provider": {
                "anthropic": {
                  "options": {
                    "baseURL": "https://api-a.invalid/v1",
                    "apiKey": "dummy-opencode-key-a"
                  },
                  "npm": "@ai-sdk/anthropic"
                }
              },
              "model": "anthropic/model-a",
              "small_model": "anthropic/small-a",
              "theme": "system"
            }
            """);

        var savedSnapshot = adapter.ReadSnapshot();
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"openai\":{\"key\":\"dummy-opencode-auth-b\"}}");
            File.WriteAllText(adapter.ConfigurationFilePath, """
                {
                  "$schema": "https://opencode.ai/config.json",
                  "provider": { "openai": { "options": { "apiKey": "dummy-opencode-key-b" } } },
                  "model": "openai/model-b",
                  "small_model": "openai/small-b",
                  "theme": "dark",
                  "autoupdate": false
                }
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), savedSnapshot, Guid.NewGuid());

            var root = ParseObject(adapter.ConfigurationFilePath);
            Assert.Contains("dummy-opencode-auth-a", File.ReadAllText(adapter.AuthenticationFilePath));
            Assert.NotNull(root["provider"]!["anthropic"]);
            Assert.Null(root["provider"]!["openai"]);
            Assert.Equal("anthropic/model-a", root["model"]!.GetValue<string>());
            Assert.Equal("anthropic/small-a", root["small_model"]!.GetValue<string>());
            Assert.Equal("dark", root["theme"]!.GetValue<string>());
            Assert.False(root["autoupdate"]!.GetValue<bool>());
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(savedSnapshot);
        }
    }

    [Fact]
    public void OpenCodeCaptureDeepMergesJsonAndJsoncFilesCreatedAfterAdapterConstruction()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path);
        var configurationDirectory = System.IO.Path.GetDirectoryName(adapter.ConfigurationFilePath)!;
        var jsonPath = System.IO.Path.Combine(configurationDirectory, "opencode.json");
        var jsoncPath = System.IO.Path.Combine(configurationDirectory, "opencode.jsonc");
        Directory.CreateDirectory(configurationDirectory);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-target-auth\"}}");
        File.WriteAllText(jsonPath, """
            {
              "provider": {
                "anthropic": {
                  "options": {
                    "baseURL": "https://lower-target.invalid/v1",
                    "headers": { "X-Lower": "lower-target" }
                  },
                  "npm": "@ai-sdk/anthropic"
                }
              },
              "model": "anthropic/target-model",
              "theme": "target-lower-theme"
            }
            """);
        File.WriteAllText(jsoncPath, """
            {
              // Later files override individual nested values without discarding lower values.
              "provider": {
                "anthropic": {
                  "options": {
                    "apiKey": "dummy-target-config-key",
                    "headers": { "X-Higher": "higher-target" }
                  }
                },
                "openai": { "options": { "baseURL": "https://openai-target.invalid/v1" } }
              },
              "small_model": "anthropic/target-small",
              "autoupdate": true,
            }
            """);

        Assert.True(adapter.HasCurrentSnapshot);
        var target = adapter.ReadSnapshot();
        byte[]? installed = null;
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-source-auth\"}}");
            File.WriteAllText(jsonPath, """
                {
                  "provider": { "anthropic": { "options": { "baseURL": "https://lower-source.invalid/v1" } } },
                  "model": "anthropic/source-model",
                  "theme": "keep-current-lower"
                }
                """);
            File.WriteAllText(jsoncPath, """
                {
                  "provider": { "anthropic": { "options": { "apiKey": "dummy-source-key" } } },
                  "small_model": "anthropic/source-small",
                  "autoupdate": false
                }
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid());

            var lower = ParseObject(jsonPath);
            Assert.Null(lower["provider"]);
            Assert.Null(lower["model"]);
            Assert.Equal("keep-current-lower", lower["theme"]!.GetValue<string>());

            var higher = ParseObject(jsoncPath);
            Assert.Equal(
                "https://lower-target.invalid/v1",
                higher["provider"]!["anthropic"]!["options"]!["baseURL"]!.GetValue<string>());
            Assert.Equal(
                "dummy-target-config-key",
                higher["provider"]!["anthropic"]!["options"]!["apiKey"]!.GetValue<string>());
            Assert.Equal(
                "lower-target",
                higher["provider"]!["anthropic"]!["options"]!["headers"]!["X-Lower"]!.GetValue<string>());
            Assert.Equal(
                "higher-target",
                higher["provider"]!["anthropic"]!["options"]!["headers"]!["X-Higher"]!.GetValue<string>());
            Assert.Equal(
                "https://openai-target.invalid/v1",
                higher["provider"]!["openai"]!["options"]!["baseURL"]!.GetValue<string>());
            Assert.Equal("anthropic/target-model", higher["model"]!.GetValue<string>());
            Assert.Equal("anthropic/target-small", higher["small_model"]!.GetValue<string>());
            Assert.False(higher["autoupdate"]!.GetValue<bool>());

            installed = adapter.ReadSnapshot();
            Assert.True(adapter.SnapshotsEqual(target, installed));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
            if (installed is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(installed);
            }
        }
    }

    [Fact]
    public void OpenCodeLegacyConfigJsonIsCapturedThenNormalizedToJsonc()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path);
        var configurationDirectory = System.IO.Path.GetDirectoryName(adapter.ConfigurationFilePath)!;
        var legacyPath = System.IO.Path.Combine(configurationDirectory, "config.json");
        Directory.CreateDirectory(configurationDirectory);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-target-auth\"}}");
        File.WriteAllText(legacyPath, """
            {
              "provider": { "anthropic": { "options": { "baseURL": "https://legacy-target.invalid/v1" } } },
              "model": "anthropic/legacy-target",
              "theme": "target-theme"
            }
            """);
        var target = adapter.ReadSnapshot();
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-source-auth\"}}");
            File.WriteAllText(legacyPath, """
                {
                  "provider": { "anthropic": { "options": { "baseURL": "https://legacy-source.invalid/v1" } } },
                  "model": "anthropic/legacy-source",
                  "theme": "keep-current-theme"
                }
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid());

            var legacy = ParseObject(legacyPath);
            Assert.Null(legacy["provider"]);
            Assert.Null(legacy["model"]);
            Assert.Equal("keep-current-theme", legacy["theme"]!.GetValue<string>());
            var jsonc = ParseObject(adapter.ConfigurationFilePath);
            Assert.Equal(
                "https://legacy-target.invalid/v1",
                jsonc["provider"]!["anthropic"]!["options"]!["baseURL"]!.GetValue<string>());
            Assert.Equal("anthropic/legacy-target", jsonc["model"]!.GetValue<string>());
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    [Fact]
    public void OpenCodeEmptyTargetClearsEveryGlobalLayerWithoutCreatingAbsentCustomFile()
    {
        using var temporary = new TemporaryDirectory();
        var customPath = System.IO.Path.Combine(temporary.Path, "custom", "accounts.jsonc");
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path, customPath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-target-auth\"}}");
        var target = adapter.ReadSnapshot();
        try
        {
            var globalPaths = adapter.ManagedFilePaths
                .Where(path => path != adapter.AuthenticationFilePath && path != customPath)
                .ToArray();
            foreach (var path in globalPaths)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllText(path, $$"""
                    {
                      "provider": { "anthropic": { "options": { "baseURL": "https://source-{{System.IO.Path.GetFileName(path)}}.invalid" } } },
                      "model": "anthropic/source",
                      "small_model": "anthropic/source-small",
                      "unrelated": "keep-{{System.IO.Path.GetFileName(path)}}"
                    }
                    """);
            }
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-source-auth\"}}");

            adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid());

            foreach (var path in globalPaths)
            {
                var root = ParseObject(path);
                Assert.Null(root["provider"]);
                Assert.Null(root["model"]);
                Assert.Null(root["small_model"]);
                Assert.Equal(
                    $"keep-{System.IO.Path.GetFileName(path)}",
                    root["unrelated"]!.GetValue<string>());
            }
            Assert.False(File.Exists(customPath));
            Assert.Contains("dummy-target-auth", File.ReadAllText(adapter.AuthenticationFilePath));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    [Fact]
    public void OpenCodeCustomConfigurationOverridesGlobalsWithoutTouchingProjectConfiguration()
    {
        using var temporary = new TemporaryDirectory();
        var customPath = System.IO.Path.Combine(temporary.Path, "custom", "accounts.jsonc");
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path, customPath);
        var globalJsonPath = adapter.ManagedFilePaths.Single(path =>
            System.IO.Path.GetFileName(path) == "opencode.json");
        var projectPath = System.IO.Path.Combine(temporary.Path, "project", "opencode.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(globalJsonPath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(customPath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-target-auth\"}}");
        File.WriteAllText(globalJsonPath, """
            {
              "provider": { "anthropic": { "options": { "baseURL": "https://global-target.invalid/v1" } } },
              "model": "anthropic/global-target",
              "theme": "target-global-theme"
            }
            """);
        File.WriteAllText(customPath, """
            {
              "provider": { "anthropic": { "options": { "apiKey": "dummy-custom-target-key" } } },
              "model": "anthropic/custom-target",
              "custom_unrelated": "target-custom"
            }
            """);
        const string projectContents = "{\"provider\":{\"anthropic\":{\"options\":{\"apiKey\":\"dummy-project-key\"}}},\"sentinel\":\"untouched\"}";
        File.WriteAllText(projectPath, projectContents);
        var target = adapter.ReadSnapshot();
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-source-auth\"}}");
            File.WriteAllText(globalJsonPath, """
                {
                  "provider": { "anthropic": { "options": { "baseURL": "https://global-source.invalid/v1" } } },
                  "model": "anthropic/global-source",
                  "theme": "keep-current-global"
                }
                """);
            File.WriteAllText(customPath, """
                {
                  "provider": { "anthropic": { "options": { "apiKey": "dummy-custom-source-key" } } },
                  "model": "anthropic/custom-source",
                  "custom_unrelated": "keep-current-custom"
                }
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid());

            var global = ParseObject(globalJsonPath);
            Assert.Null(global["provider"]);
            Assert.Null(global["model"]);
            Assert.Equal("keep-current-global", global["theme"]!.GetValue<string>());
            var custom = ParseObject(customPath);
            Assert.Equal(
                "https://global-target.invalid/v1",
                custom["provider"]!["anthropic"]!["options"]!["baseURL"]!.GetValue<string>());
            Assert.Equal(
                "dummy-custom-target-key",
                custom["provider"]!["anthropic"]!["options"]!["apiKey"]!.GetValue<string>());
            Assert.Equal("anthropic/custom-target", custom["model"]!.GetValue<string>());
            Assert.Equal("keep-current-custom", custom["custom_unrelated"]!.GetValue<string>());
            Assert.Equal(projectContents, File.ReadAllText(projectPath));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    [Fact]
    public async Task AccountSwitchServiceSwitchesCompositeCodexSnapshotsEndToEnd()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        WriteCodexAccount(adapter, "a", "https://api-a.invalid", "keep-a", "\r\n");

        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = new AccountSwitchService(
            vault,
            new FixedProcessInspector(ProcessInspectionResult.Clear),
            mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");
        var first = await service.CaptureCurrentLoginAsync(
            adapter,
            "First",
            cancellationToken: CancellationToken.None);
        Assert.Equal(AccountOperationStatus.Success, first.Status);

        WriteCodexAccount(adapter, "b", "https://api-b.invalid", "keep-b", "\n");
        var second = await service.CaptureCurrentLoginAsync(
            adapter,
            "Second",
            cancellationToken: CancellationToken.None);
        Assert.Equal(AccountOperationStatus.Success, second.Status);

        var result = await service.SwitchAsync(
            adapter,
            first.Profile!.ProfileId,
            cancellationToken: CancellationToken.None);

        Assert.Equal(AccountOperationStatus.Success, result.Status);
        Assert.Contains("dummy-openai-key-a", File.ReadAllText(adapter.AuthenticationFilePath));
        var merged = File.ReadAllText(adapter.ConfigurationFilePath);
        Assert.Contains("model = \"model-a\"", merged);
        Assert.Contains("base_url = \"https://api-a.invalid\"", merged);
        // This field is deliberately outside the managed API/model boundary.
        Assert.Contains("command = \"keep-b\"", merged);

        var alreadyActive = await service.SwitchAsync(
            adapter,
            first.Profile.ProfileId,
            cancellationToken: CancellationToken.None);
        Assert.Equal(AccountOperationStatus.AlreadyActive, alreadyActive.Status);
    }

    [Fact]
    public async Task PreparedCompositeSwitchRecoversFailClosedIntermediateToTheSourceSnapshot()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        var writer = new ThrowBeforePathCommitWriter(adapter.AuthenticationFilePath);
        var vaultPath = System.IO.Path.Combine(temporary.Path, "vault");
        var vault = new AuthenticationProfileVault(vaultPath, atomicWriter: writer);
        var captureService = new AccountSwitchService(
            vault,
            new FixedProcessInspector(ProcessInspectionResult.Clear),
            mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

        WriteCodexAccount(adapter, "a", "https://api-a.invalid", "keep-a");
        var first = await captureService.CaptureCurrentLoginAsync(
            adapter,
            "First",
            cancellationToken: CancellationToken.None);
        WriteCodexAccount(adapter, "b", "https://api-b.invalid", "keep-b");
        var second = await captureService.CaptureCurrentLoginAsync(
            adapter,
            "Second",
            cancellationToken: CancellationToken.None);
        Assert.Equal(AccountOperationStatus.Success, first.Status);
        Assert.Equal(AccountOperationStatus.Success, second.Status);

        writer.Enabled = true;
        var interruptedService = new AccountSwitchService(
            vault,
            new SequenceInspector(
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                ProcessInspectionResult.Clear,
                new ProcessInspectionResult
                {
                    Status = ProcessInspectionStatus.Running,
                    Processes = [new DetectedProcess(42, "codex")]
                }),
            mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

        var interrupted = await interruptedService.SwitchAsync(
            adapter,
            first.Profile!.ProfileId,
            cancellationToken: CancellationToken.None);
        Assert.Equal(AccountOperationStatus.RecoveryRequired, interrupted.Status);
        Assert.NotNull(vault.GetPendingJournal(AgentProvider.Codex));
        Assert.False(File.Exists(adapter.AuthenticationFilePath));
        Assert.Contains("model = \"model-a\"", File.ReadAllText(adapter.ConfigurationFilePath));

        var restartedVault = new AuthenticationProfileVault(vaultPath);
        var recoveryService = new AccountSwitchService(
            restartedVault,
            new FixedProcessInspector(ProcessInspectionResult.Clear),
            mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");
        var recovery = await recoveryService.RecoverAsync(
            adapter,
            CancellationToken.None);

        Assert.Equal(RecoveryStatus.RecoveredToSource, recovery.Status);
        Assert.Equal(second.Profile!.ProfileId, recovery.ActiveProfileId);
        Assert.Contains("dummy-openai-key-b", File.ReadAllText(adapter.AuthenticationFilePath));
        Assert.Contains("model = \"model-b\"", File.ReadAllText(adapter.ConfigurationFilePath));
        Assert.Null(restartedVault.GetPendingJournal(AgentProvider.Codex));
    }

    [Fact]
    public async Task AccountSwitchServiceCapturesSettingsOnlyClaudeAndConfigurationOnlyOpenCodeProfiles()
    {
        using var temporary = new TemporaryDirectory();
        var claude = new ClaudeCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(claude.SettingsFilePath)!);
        File.WriteAllText(claude.SettingsFilePath, """
            { "env": { "ANTHROPIC_AUTH_TOKEN": "dummy-claude-token" } }
            """);
        var openCode = new OpenCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(openCode.ConfigurationFilePath)!);
        File.WriteAllText(openCode.ConfigurationFilePath, """
            { "provider": { "anthropic": { "options": { "apiKey": "dummy-opencode-token" } } } }
            """);

        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var service = new AccountSwitchService(
            vault,
            new FixedProcessInspector(ProcessInspectionResult.Clear),
            mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

        var claudeResult = await service.CaptureCurrentLoginAsync(
            claude,
            "Claude API",
            cancellationToken: CancellationToken.None);
        var openCodeResult = await service.CaptureCurrentLoginAsync(
            openCode,
            "OpenCode API",
            cancellationToken: CancellationToken.None);

        Assert.Equal(AccountOperationStatus.Success, claudeResult.Status);
        Assert.Equal(AccountOperationStatus.Success, openCodeResult.Status);
        Assert.Single(vault.ListProfiles(AgentProvider.ClaudeCode));
        Assert.Single(vault.ListProfiles(AgentProvider.OpenCode));
    }

    [Fact]
    public async Task UnrelatedSettingsAreNotDetectedOrCapturedAsAccountProfiles()
    {
        using var temporary = new TemporaryDirectory();
        var claude = new ClaudeCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(claude.SettingsFilePath)!);
        File.WriteAllText(claude.SettingsFilePath, "{\"theme\":\"dark\",\"env\":{\"UNRELATED\":\"keep\"}}");
        var openCode = new OpenCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(openCode.ConfigurationFilePath)!);
        File.WriteAllText(openCode.ConfigurationFilePath, "{\"theme\":\"dark\",\"autoupdate\":false}");
        var service = new AccountSwitchService(
            new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault")),
            new FixedProcessInspector(ProcessInspectionResult.Clear),
            mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

        var claudeCapture = await service.CaptureCurrentLoginAsync(
            claude,
            "Not an account",
            cancellationToken: CancellationToken.None);
        var openCodeCapture = await service.CaptureCurrentLoginAsync(
            openCode,
            "Not an account",
            cancellationToken: CancellationToken.None);

        Assert.False(claude.HasCurrentSnapshot);
        Assert.False(openCode.HasCurrentSnapshot);
        Assert.Equal(AccountOperationStatus.AuthenticationFileMissing, claudeCapture.Status);
        Assert.Equal(AccountOperationStatus.AuthenticationFileMissing, openCodeCapture.Status);
    }

    [Fact]
    public void LegacyRawCodexProfileClearsManagedApiConfiguration()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-current\"}");
        File.WriteAllText(adapter.ConfigurationFilePath, """
            model_provider = "ApiSite"
            model = "api-model"
            network_access = "enabled"

            [model_providers.ApiSite]
            base_url = "https://old-api.invalid"
            http_headers = { Authorization = "dummy-old-header" }
            """);
        var legacy = System.Text.Encoding.UTF8.GetBytes("{\"OPENAI_API_KEY\":\"dummy-legacy\"}");
        var apiSiteSnapshot = adapter.ReadSnapshot();
        byte[]? clearedSnapshot = null;
        try
        {
            Assert.True(adapter.IsLegacySnapshot(legacy));
            Assert.False(adapter.SnapshotsEqual(legacy, apiSiteSnapshot));

            adapter.WriteSnapshot(new AtomicFileWriter(), legacy, Guid.NewGuid());

            Assert.Equal("{\"OPENAI_API_KEY\":\"dummy-legacy\"}", File.ReadAllText(adapter.AuthenticationFilePath));
            var configuration = File.ReadAllText(adapter.ConfigurationFilePath);
            Assert.DoesNotContain("model_provider", configuration);
            Assert.DoesNotContain("api-model", configuration);
            Assert.DoesNotContain("old-api.invalid", configuration);
            Assert.DoesNotContain("dummy-old-header", configuration);
            Assert.Contains("network_access = \"enabled\"", configuration);

            clearedSnapshot = adapter.ReadSnapshot();
            Assert.False(adapter.IsLegacySnapshot(clearedSnapshot));
            Assert.True(adapter.SnapshotsEqual(legacy, clearedSnapshot));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(legacy);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(apiSiteSnapshot);
            if (clearedSnapshot is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(clearedSnapshot);
            }
        }
    }

    [Fact]
    public void LegacyRawClaudeProfileClearsManagedApiEnvironmentAndPreservesUnrelatedSettings()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new ClaudeCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"sessionKey\":\"dummy-current\"}");
        File.WriteAllText(adapter.SettingsFilePath, """
            {
              "env": {
                "ANTHROPIC_BASE_URL": "https://old-api.invalid",
                "ANTHROPIC_API_KEY": "dummy-old-api-key",
                "ANTHROPIC_AUTH_TOKEN": "dummy-old-auth-token",
                "CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC": "1",
                "CLAUDE_CODE_ATTRIBUTION_HEADER": "0",
                "UNRELATED_ENV": "keep"
              },
              "permissions": { "allow": ["Read"] }
            }
            """);
        var legacy = System.Text.Encoding.UTF8.GetBytes("{\"sessionKey\":\"dummy-legacy\"}");

        try
        {
            adapter.WriteSnapshot(new AtomicFileWriter(), legacy, Guid.NewGuid());

            Assert.Equal("{\"sessionKey\":\"dummy-legacy\"}", File.ReadAllText(adapter.AuthenticationFilePath));
            var root = ParseObject(adapter.SettingsFilePath);
            var environment = Assert.IsType<JsonObject>(root["env"]);
            Assert.Null(environment["ANTHROPIC_BASE_URL"]);
            Assert.Null(environment["ANTHROPIC_API_KEY"]);
            Assert.Null(environment["ANTHROPIC_AUTH_TOKEN"]);
            Assert.Null(environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"]);
            Assert.Null(environment["CLAUDE_CODE_ATTRIBUTION_HEADER"]);
            Assert.Equal("keep", environment["UNRELATED_ENV"]!.GetValue<string>());
            Assert.Equal("Read", root["permissions"]!["allow"]![0]!.GetValue<string>());
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(legacy);
        }
    }

    [Fact]
    public async Task SwitchingAwayFromLegacyActiveCodexProfileUpgradesItsSavedSnapshot()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        var legacy = System.Text.Encoding.UTF8.GetBytes("{\"OPENAI_API_KEY\":\"dummy-openai-key-a\"}");
        File.WriteAllBytes(adapter.AuthenticationFilePath, legacy);
        File.WriteAllText(adapter.ConfigurationFilePath, "network_access = \"enabled\"\n");
        var sourceComposite = adapter.ReadSnapshot();
        var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
        var source = vault.CaptureProfileAndSetActive(AgentProvider.Codex, "Legacy source", legacy, null);

        WriteCodexAccount(adapter, "b", "https://api-b.invalid", "keep-b");
        var targetSnapshot = adapter.ReadSnapshot();
        var target = vault.CreateProfile(AgentProvider.Codex, "API target", targetSnapshot);
        File.WriteAllBytes(adapter.AuthenticationFilePath, legacy);
        File.WriteAllText(adapter.ConfigurationFilePath, "network_access = \"enabled\"\n");

        byte[]? upgraded = null;
        try
        {
            var service = new AccountSwitchService(
                vault,
                new FixedProcessInspector(ProcessInspectionResult.Clear),
                mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");
            var result = await service.SwitchAsync(
                adapter,
                target.ProfileId,
                cancellationToken: CancellationToken.None);

            Assert.Equal(AccountOperationStatus.Success, result.Status);
            upgraded = vault.LoadCredential(AgentProvider.Codex, source.ProfileId);
            Assert.False(adapter.IsLegacySnapshot(upgraded));
            Assert.True(adapter.SnapshotsEqual(sourceComposite, upgraded));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(legacy);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(sourceComposite);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(targetSnapshot);
            if (upgraded is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(upgraded);
            }
        }
    }

    [Fact]
    public void CodexSnapshotCommitIsFailClosedAtEveryFileOperation()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        WriteCodexAccount(adapter, "a", "https://api-a.invalid", "keep-a");
        var target = adapter.ReadSnapshot();
        try
        {
            AssertFailClosedCommitOrdering(
                adapter,
                target,
                () => WriteCodexAccount(adapter, "b", "https://api-b.invalid", "keep-b"),
                adapter.ConfigurationFilePath,
                "dummy-openai-key-b",
                "https://api-b.invalid",
                "https://api-a.invalid");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    [Fact]
    public void ClaudeSnapshotCommitIsFailClosedAtEveryFileOperation()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new ClaudeCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);

        void WriteAccount(string suffix)
        {
            File.WriteAllText(adapter.AuthenticationFilePath, $"{{\"sessionKey\":\"dummy-claude-{suffix}\"}}");
            File.WriteAllText(adapter.SettingsFilePath, $$"""
                {
                  "env": {
                    "ANTHROPIC_BASE_URL": "https://api-{{suffix}}.invalid",
                    "ANTHROPIC_AUTH_TOKEN": "dummy-claude-token-{{suffix}}"
                  },
                  "theme": "system"
                }
                """);
        }

        WriteAccount("a");
        var target = adapter.ReadSnapshot();
        try
        {
            AssertFailClosedCommitOrdering(
                adapter,
                target,
                () => WriteAccount("b"),
                adapter.SettingsFilePath,
                "dummy-claude-b",
                "https://api-b.invalid",
                "https://api-a.invalid");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    [Fact]
    public void OpenCodeSnapshotCommitIsFailClosedAtEveryFileOperation()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.ConfigurationFilePath)!);

        void WriteAccount(string suffix)
        {
            File.WriteAllText(adapter.AuthenticationFilePath, $"{{\"anthropic\":{{\"key\":\"dummy-opencode-{suffix}\"}}}}");
            File.WriteAllText(adapter.ConfigurationFilePath, $$"""
                {
                  "provider": {
                    "anthropic": {
                      "options": {
                        "baseURL": "https://api-{{suffix}}.invalid/v1",
                        "apiKey": "dummy-opencode-config-{{suffix}}"
                      }
                    }
                  },
                  "theme": "system"
                }
                """);
        }

        WriteAccount("a");
        var target = adapter.ReadSnapshot();
        try
        {
            AssertFailClosedCommitOrdering(
                adapter,
                target,
                () => WriteAccount("b"),
                adapter.ConfigurationFilePath,
                "dummy-opencode-b",
                "https://api-b.invalid/v1",
                "https://api-a.invalid/v1");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    [Fact]
    public void OpenCodeMultiLayerCommitAndRecoveryAreFailClosedAtEveryFileOperation()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path);
        var configurationPaths = adapter.ManagedFilePaths
            .Where(path => path != adapter.AuthenticationFilePath)
            .ToArray();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.ConfigurationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-target-auth\"}}");
        File.WriteAllText(adapter.ConfigurationFilePath, """
            {
              "provider": { "anthropic": { "options": { "baseURL": "https://target.invalid/v1" } } },
              "model": "anthropic/target"
            }
            """);
        var target = adapter.ReadSnapshot();

        void RestoreSource()
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-source-auth\"}}");
            for (var index = 0; index < configurationPaths.Length; index++)
            {
                var path = configurationPaths[index];
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllText(path, $$"""
                    {
                      "provider": { "anthropic": { "options": { "baseURL": "https://source-{{index}}.invalid/v1" } } },
                      "model": "anthropic/source-{{index}}",
                      "unrelated": "keep-{{index}}"
                    }
                    """);
            }
        }

        RestoreSource();
        var source = adapter.ReadSnapshot();
        byte[]? current = null;
        try
        {
            // delete auth + three ordered configuration writes + install target auth
            for (var failBeforeOperation = 1; failBeforeOperation <= 5; failBeforeOperation++)
            {
                RestoreSource();
                var writer = new FailBeforeOperationWriter(failBeforeOperation);
                Assert.Throws<IOException>(
                    () => adapter.WriteSnapshot(writer, target, Guid.NewGuid()));

                if (failBeforeOperation == 1)
                {
                    Assert.Contains("dummy-source-auth", File.ReadAllText(adapter.AuthenticationFilePath));
                }
                else
                {
                    Assert.False(File.Exists(adapter.AuthenticationFilePath));
                    current = adapter.ReadSnapshot(allowIncomplete: true);
                    Assert.True(adapter.IsRecognizedPartialSnapshot(source, target, current));
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(current);
                    current = null;
                }

                var completedConfigurationWrites = Math.Clamp(failBeforeOperation - 2, 0, 3);
                for (var index = 0; index < configurationPaths.Length; index++)
                {
                    var root = ParseObject(configurationPaths[index]);
                    Assert.Equal($"keep-{index}", root["unrelated"]!.GetValue<string>());
                    if (index < completedConfigurationWrites)
                    {
                        if (index == configurationPaths.Length - 1)
                        {
                            Assert.Equal(
                                "https://target.invalid/v1",
                                root["provider"]!["anthropic"]!["options"]!["baseURL"]!.GetValue<string>());
                            Assert.Equal("anthropic/target", root["model"]!.GetValue<string>());
                        }
                        else
                        {
                            Assert.Null(root["provider"]);
                            Assert.Null(root["model"]);
                        }
                    }
                    else
                    {
                        Assert.Equal(
                            $"https://source-{index}.invalid/v1",
                            root["provider"]!["anthropic"]!["options"]!["baseURL"]!.GetValue<string>());
                        Assert.Equal($"anthropic/source-{index}", root["model"]!.GetValue<string>());
                    }
                }
            }
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(source);
            if (current is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(current);
            }
        }
    }

    [Fact]
    public void OpenCodeAuthenticationOnlySnapshotDoesNotCreateAnEmptyConfigurationFile()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new OpenCodeAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"key\":\"dummy-connect-key\"}}");
        Assert.False(File.Exists(adapter.ConfigurationFilePath));
        Assert.True(adapter.HasCurrentSnapshot);

        var snapshot = adapter.ReadSnapshot();
        byte[]? restored = null;
        try
        {
            File.Delete(adapter.AuthenticationFilePath);
            adapter.WriteSnapshot(new AtomicFileWriter(), snapshot, Guid.NewGuid());

            Assert.Contains("dummy-connect-key", File.ReadAllText(adapter.AuthenticationFilePath));
            Assert.False(File.Exists(adapter.ConfigurationFilePath));
            restored = adapter.ReadSnapshot();
            Assert.True(adapter.SnapshotsEqual(snapshot, restored));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(snapshot);
            if (restored is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(restored);
            }
        }
    }

    [Fact]
    public void CodexSnapshotsAreStableAcrossCrLfAndLfConfigurationFiles()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        WriteCodexAccount(adapter, "a", "https://api-a.invalid", "keep", "\r\n");
        var crLf = adapter.ReadSnapshot();
        WriteCodexAccount(adapter, "a", "https://api-a.invalid", "keep", "\n");
        var lf = adapter.ReadSnapshot();
        try
        {
            Assert.True(adapter.SnapshotsEqual(crLf, lf));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(crLf);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(lf);
        }
    }

    [Fact]
    public void CodexTomlLexerIgnoresPseudoSettingsInsideMultilineValues()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-target\"}");
        File.WriteAllText(adapter.ConfigurationFilePath, """"
            model_provider = "ProviderA"
            model = "target-model"

            [model_providers.ProviderA]
            base_url = "https://target.invalid"
            http_headers = { Authorization = "dummy-target-header" }
            provider_note = """
            target-provider-note
            [features]
            responses_websockets_v2 = false
            [model_providers.FakeTarget]
            base_url = "https://fake-target.invalid"
            """

            [features]
            responses_websockets_v2 = true
            """");
        var target = adapter.ReadSnapshot();
        byte[]? installed = null;
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-source\"}");
            File.WriteAllText(adapter.ConfigurationFilePath, """"
                description = """
                current-description-sentinel
                model_provider = "FakeCurrent"
                [model_providers.FakeCurrent]
                base_url = "https://fake-current.invalid"
                [features]
                responses_websockets_v2 = false
                """
                literal_description = '''
                current-literal-sentinel
                model = 'fake-model'
                [model_providers.FakeLiteral]
                '''
                ignored_array = [
                  "model_provider = \"FakeArray\"",
                  { value = "[features]", other = "model = 'fake-array-model'" },
                ]
                model_provider = "ProviderB"
                model = "source-model"

                [model_providers.ProviderB]
                base_url = "https://source.invalid"
                http_headers = { Authorization = "dummy-source-header" }

                [model_providers.ProviderB.headers]
                X-Source = "dummy-nested-source-header"

                [model_providers.ProviderA]
                base_url = "https://stale-target.invalid"
                http_headers = { Authorization = "dummy-stale-target-header" }

                [model_providers.Dormant]
                base_url = "https://dormant.invalid"

                [features]
                responses_websockets_v2 = false

                [mcp_servers.keep]
                command = "keep-current"
                """");

            adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid());

            var merged = File.ReadAllText(adapter.ConfigurationFilePath);
            Assert.Contains("model_provider = \"ProviderA\"", merged);
            Assert.Contains("model = \"target-model\"", merged);
            Assert.Contains("base_url = \"https://target.invalid\"", merged);
            Assert.Contains("dummy-target-header", merged);
            Assert.DoesNotContain("https://source.invalid", merged);
            Assert.DoesNotContain("dummy-source-header", merged);
            Assert.DoesNotContain("dummy-nested-source-header", merged);
            Assert.DoesNotContain("https://stale-target.invalid", merged);
            Assert.DoesNotContain("dummy-stale-target-header", merged);
            Assert.Contains("https://dormant.invalid", merged);
            Assert.Contains("current-description-sentinel", merged);
            Assert.Contains("https://fake-current.invalid", merged);
            Assert.Contains("current-literal-sentinel", merged);
            Assert.Contains("FakeArray", merged);
            Assert.Contains("target-provider-note", merged);
            Assert.Contains("https://fake-target.invalid", merged);
            Assert.Contains("command = \"keep-current\"", merged);

            installed = adapter.ReadSnapshot();
            Assert.True(adapter.SnapshotsEqual(target, installed));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
            if (installed is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(installed);
            }
        }
    }

    [Fact]
    public void CodexQuotedAndDottedManagedKeysAreReplacedWithoutSemanticDuplicates()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-target\"}");
        File.WriteAllText(adapter.ConfigurationFilePath, """
            'model_provider' = "ProviderA"
            "model" = "target-model"
            "features".'responses_websockets_v2' = true

            [model_providers.ProviderA]
            base_url = "https://target.invalid"
            """);
        var target = adapter.ReadSnapshot();
        byte[]? installed = null;
        try
        {
            File.WriteAllText(adapter.AuthenticationFilePath, "{\"OPENAI_API_KEY\":\"dummy-source\"}");
            File.WriteAllText(adapter.ConfigurationFilePath, """
                "model_provider" = "ProviderB"
                'model' = "source-model"
                features."responses_websockets_v2" = false

                [model_providers.ProviderB]
                base_url = "https://source.invalid"

                [mcp_servers.keep]
                command = "keep-current"
                """);

            adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid());

            var merged = File.ReadAllText(adapter.ConfigurationFilePath);
            Assert.Equal(1, CountOccurrences(merged, "model_provider ="));
            Assert.Equal(1, CountOccurrences(merged, "model ="));
            Assert.Equal(1, CountOccurrences(merged, "features.responses_websockets_v2 ="));
            Assert.Contains("model_provider = \"ProviderA\"", merged);
            Assert.Contains("model = \"target-model\"", merged);
            Assert.Contains("features.responses_websockets_v2 = true", merged);
            Assert.DoesNotContain("ProviderB", merged);
            Assert.DoesNotContain("source-model", merged);
            Assert.DoesNotContain("https://source.invalid", merged);
            Assert.Contains("[model_providers.ProviderA]", merged);
            Assert.Contains("https://target.invalid", merged);
            Assert.Contains("command = \"keep-current\"", merged);

            installed = adapter.ReadSnapshot();
            Assert.True(adapter.SnapshotsEqual(target, installed));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
            if (installed is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(installed);
            }
        }
    }

    [Fact]
    public void InvalidCodexTomlIsRejectedBeforeAuthenticationIsRemoved()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = new CodexAuthenticationAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        WriteCodexAccount(adapter, "a", "https://api-a.invalid", "keep-a");
        var target = adapter.ReadSnapshot();
        const string sourceAuthentication = "{\"OPENAI_API_KEY\":\"dummy-source\"}";
        const string malformedConfiguration = "model_provider = \"ProviderB\"\nnotes = \"\"\"\nunterminated";
        File.WriteAllText(adapter.AuthenticationFilePath, sourceAuthentication);
        File.WriteAllText(adapter.ConfigurationFilePath, malformedConfiguration);
        try
        {
            Assert.Throws<InvalidDataException>(
                () => adapter.WriteSnapshot(new AtomicFileWriter(), target, Guid.NewGuid()));
            Assert.Equal(sourceAuthentication, File.ReadAllText(adapter.AuthenticationFilePath));
            Assert.Equal(malformedConfiguration, File.ReadAllText(adapter.ConfigurationFilePath));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(target);
        }
    }

    private static JsonObject ParseObject(string path) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(path)));

    private static int CountOccurrences(string value, string searchValue)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(searchValue, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += searchValue.Length;
        }

        return count;
    }

    private static void AssertFailClosedCommitOrdering(
        IAuthenticationAdapter adapter,
        byte[] targetSnapshot,
        Action restoreSource,
        string configurationPath,
        string sourceAuthenticationMarker,
        string sourceConfigurationMarker,
        string targetConfigurationMarker)
    {
        for (var failBeforeOperation = 1; failBeforeOperation <= 3; failBeforeOperation++)
        {
            restoreSource();
            var writer = new FailBeforeOperationWriter(failBeforeOperation);

            Assert.Throws<IOException>(
                () => adapter.WriteSnapshot(writer, targetSnapshot, Guid.NewGuid()));

            if (failBeforeOperation == 1)
            {
                Assert.Contains(
                    sourceAuthenticationMarker,
                    File.ReadAllText(adapter.AuthenticationFilePath));
            }
            else
            {
                Assert.False(File.Exists(adapter.AuthenticationFilePath));
            }

            var configuration = File.ReadAllText(configurationPath);
            var configurationShouldBeTarget = failBeforeOperation == 3;
            Assert.Contains(
                configurationShouldBeTarget ? targetConfigurationMarker : sourceConfigurationMarker,
                configuration);
            Assert.DoesNotContain(
                configurationShouldBeTarget ? sourceConfigurationMarker : targetConfigurationMarker,
                configuration);
        }
    }

    private static void WriteCodexAccount(
        CodexAuthenticationAdapter adapter,
        string suffix,
        string baseUrl,
        string unrelatedCommand,
        string newLine = "\n")
    {
        File.WriteAllText(
            adapter.AuthenticationFilePath,
            $"{{\"OPENAI_API_KEY\":\"dummy-openai-key-{suffix}\"}}");
        var configuration = $$"""
            model_provider = "Provider{{suffix.ToUpperInvariant()}}"
            model = "model-{{suffix}}"
            network_access = "enabled"

            [model_providers.Provider{{suffix.ToUpperInvariant()}}]
            name = "Provider {{suffix.ToUpperInvariant()}}"
            base_url = "{{baseUrl}}"
            wire_api = "responses"
            requires_openai_auth = false

            [mcp_servers.keep]
            command = "{{unrelatedCommand}}"
            """;
        File.WriteAllText(
            adapter.ConfigurationFilePath,
            configuration.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", newLine, StringComparison.Ordinal));
    }

    private sealed class ThrowBeforePathCommitWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private readonly string _path;
        private bool _hasThrown;

        public ThrowBeforePathCommitWriter(string path)
        {
            _path = System.IO.Path.GetFullPath(path);
        }

        public bool Enabled { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            if (Enabled && !_hasThrown && System.IO.Path.GetFullPath(destinationPath) == _path)
            {
                _hasThrown = true;
                throw new IOException("Injected interruption before the final authentication commit.");
            }
            _inner.WriteAllBytes(destinationPath, contents, transactionId);
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }

    private sealed class FailBeforeOperationWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private readonly int _failureOperation;
        private int _operationCount;

        public FailBeforeOperationWriter(int failureOperation)
        {
            _failureOperation = failureOperation;
        }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            FailIfSelectedOperation();
            _inner.WriteAllBytes(destinationPath, contents, transactionId);
        }

        public void DeleteFile(string destinationPath)
        {
            FailIfSelectedOperation();
            _inner.DeleteFile(destinationPath);
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        private void FailIfSelectedOperation()
        {
            _operationCount++;
            if (_operationCount == _failureOperation)
            {
                throw new IOException($"Injected failure before file operation {_failureOperation}.");
            }
        }
    }

    private sealed class SequenceInspector : IProcessInspector
    {
        private readonly Queue<ProcessInspectionResult> _results;
        private ProcessInspectionResult _last;

        public SequenceInspector(params ProcessInspectionResult[] results)
        {
            _results = new Queue<ProcessInspectionResult>(results);
            _last = results[^1];
        }

        public ProcessInspectionResult Inspect(IAuthenticationAdapter adapter)
        {
            if (_results.TryDequeue(out var result))
            {
                _last = result;
            }

            return _last;
        }
    }
}
