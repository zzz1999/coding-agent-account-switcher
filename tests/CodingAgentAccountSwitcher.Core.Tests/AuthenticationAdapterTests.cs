namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class AuthenticationAdapterTests
{
    [Fact]
    public void AdaptersResolveTheirManagedAccountFiles()
    {
        using var temporary = new TemporaryDirectory();

        var codex = new CodexAuthenticationAdapter(temporary.Path);
        var claude = new ClaudeCodeAuthenticationAdapter(temporary.Path);
        var openCode = new OpenCodeAuthenticationAdapter(temporary.Path);

        Assert.Equal(
            System.IO.Path.Combine(temporary.Path, ".codex", "auth.json"),
            codex.AuthenticationFilePath);
        Assert.Equal(
            System.IO.Path.Combine(temporary.Path, ".claude", ".credentials.json"),
            claude.AuthenticationFilePath);
        Assert.Equal(
            System.IO.Path.Combine(temporary.Path, ".local", "share", "opencode", "auth.json"),
            openCode.AuthenticationFilePath);
        Assert.Equal(2, codex.ManagedFilePaths.Count);
        Assert.Contains(System.IO.Path.Combine(temporary.Path, ".codex", "config.toml"), codex.ManagedFilePaths);
        Assert.Equal(2, claude.ManagedFilePaths.Count);
        Assert.Contains(System.IO.Path.Combine(temporary.Path, ".claude", "settings.json"), claude.ManagedFilePaths);
        Assert.Equal(4, openCode.ManagedFilePaths.Count);
        Assert.Contains(
            System.IO.Path.Combine(temporary.Path, ".config", "opencode", "config.json"),
            openCode.ManagedFilePaths);
        Assert.Contains(
            System.IO.Path.Combine(temporary.Path, ".config", "opencode", "opencode.json"),
            openCode.ManagedFilePaths);
        Assert.Contains(
            System.IO.Path.Combine(temporary.Path, ".config", "opencode", "opencode.jsonc"),
            openCode.ManagedFilePaths);
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("ChatGPT"));
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("codex"));
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("codex-code-mode-host"));
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("codex-command-runner-42"));
        Assert.Contains(claude.BlockingProcessRules, rule => rule.Matches("Claude"));
        Assert.Contains(claude.BlockingProcessRules, rule => rule.Matches("claude-code"));
        Assert.Contains(openCode.BlockingProcessRules, rule => rule.Matches("opencode"));
        Assert.Contains(openCode.BlockingProcessRules, rule => rule.Matches("opencode-cli"));
    }

    [Fact]
    public void ExplicitAuthenticationRootsModelEnvironmentOverridesWithoutReadingCredentials()
    {
        using var temporary = new TemporaryDirectory();
        var userProfile = System.IO.Path.Combine(temporary.Path, "user");
        var codexHome = System.IO.Path.Combine(temporary.Path, "custom-codex-home");
        var claudeConfig = System.IO.Path.Combine(temporary.Path, "custom-claude-config");

        var codex = new CodexAuthenticationAdapter(userProfile, codexHome);
        var claude = new ClaudeCodeAuthenticationAdapter(userProfile, claudeConfig);

        Assert.Equal(System.IO.Path.Combine(codexHome, "auth.json"), codex.AuthenticationFilePath);
        Assert.Equal(System.IO.Path.Combine(claudeConfig, ".credentials.json"), claude.AuthenticationFilePath);
        Assert.False(File.Exists(codex.AuthenticationFilePath));
        Assert.False(File.Exists(claude.AuthenticationFilePath));
    }

    [Fact]
    public void DefaultAdaptersPreferEnvironmentConfiguredRoots()
    {
        using var temporary = new TemporaryDirectory();
        var codexHome = System.IO.Path.Combine(temporary.Path, "environment-codex-home");
        var claudeConfig = System.IO.Path.Combine(temporary.Path, "environment-claude-config");
        var originalCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var originalClaudeConfig = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");

        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", claudeConfig);

            Assert.Equal(
                System.IO.Path.Combine(codexHome, "auth.json"),
                new CodexAuthenticationAdapter().AuthenticationFilePath);
            Assert.Equal(
                System.IO.Path.Combine(claudeConfig, ".credentials.json"),
                new ClaudeCodeAuthenticationAdapter().AuthenticationFilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", originalCodexHome);
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", originalClaudeConfig);
        }
    }

    [Fact]
    public void OpenCodeDefaultAdapterHonorsConfigurationPathEnvironmentOverride()
    {
        using var temporary = new TemporaryDirectory();
        var customPath = System.IO.Path.Combine(temporary.Path, "custom", "accounts.json");
        var originalPath = Environment.GetEnvironmentVariable("OPENCODE_CONFIG");

        try
        {
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG", customPath);

            Assert.Equal(customPath, new OpenCodeAuthenticationAdapter().ConfigurationFilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG", originalPath);
        }
    }

    [Fact]
    public void OpenCodeDefaultAdapterHonorsXdgConfigurationAndDataRoots()
    {
        using var temporary = new TemporaryDirectory();
        var configurationRoot = System.IO.Path.Combine(temporary.Path, "xdg-config");
        var dataRoot = System.IO.Path.Combine(temporary.Path, "xdg-data");
        var originalConfigurationRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var originalDataRoot = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var originalCustomConfiguration = Environment.GetEnvironmentVariable("OPENCODE_CONFIG");

        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configurationRoot);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", dataRoot);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG", null);

            var adapter = new OpenCodeAuthenticationAdapter();

            Assert.Equal(
                System.IO.Path.Combine(configurationRoot, "opencode", "opencode.jsonc"),
                adapter.ConfigurationFilePath);
            Assert.Equal(
                System.IO.Path.Combine(dataRoot, "opencode", "auth.json"),
                adapter.AuthenticationFilePath);
            Assert.Contains(
                System.IO.Path.Combine(configurationRoot, "opencode", "config.json"),
                adapter.ManagedFilePaths);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalConfigurationRoot);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", originalDataRoot);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG", originalCustomConfiguration);
        }
    }

    [Fact]
    public void OpenCodeDefaultAdapterBlocksRelativePathEnvironmentOverrides()
    {
        var pathVariables = new[]
        {
            "XDG_CONFIG_HOME",
            "XDG_DATA_HOME",
            "OPENCODE_CONFIG",
            "OPENCODE_CONFIG_DIR"
        };
        var variablesToRestore = pathVariables
            .Concat(new[] { "OPENCODE_AUTH_CONTENT", "OPENCODE_CONFIG_CONTENT" })
            .ToDictionary(
                static variable => variable,
                static variable => Environment.GetEnvironmentVariable(variable),
                StringComparer.Ordinal);

        try
        {
            foreach (var variable in variablesToRestore.Keys)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }

            foreach (var variable in pathVariables)
            {
                Environment.SetEnvironmentVariable(variable, Path.Combine("relative", "opencode"));

                var adapter = new OpenCodeAuthenticationAdapter();
                var exception = Assert.Throws<InvalidOperationException>(() => _ = adapter.HasCurrentSnapshot);

                Assert.Contains(variable, exception.Message);
                Assert.Contains("relative path", exception.Message);
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
        finally
        {
            foreach (var (variable, value) in variablesToRestore)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }
        }
    }

    [Fact]
    public void OpenCodeKnownInlineOverridesFailClosedWithActionableErrors()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateEnvironmentValidatingOpenCodeAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"type\":\"api\",\"key\":\"dummy-auth\"}}");
        var originalAuthenticationContent = Environment.GetEnvironmentVariable("OPENCODE_AUTH_CONTENT");
        var originalConfigurationContent = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_CONTENT");
        var originalConfigurationDirectory = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_DIR");

        try
        {
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_DIR", null);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_CONTENT", null);
            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", "{\"anthropic\":{\"type\":\"api\",\"key\":\"inline\"}}");
            var authenticationException = Assert.Throws<InvalidOperationException>(() => adapter.ReadSnapshot());
            Assert.Contains("OPENCODE_AUTH_CONTENT", authenticationException.Message);

            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", null);
            Environment.SetEnvironmentVariable(
                "OPENCODE_CONFIG_CONTENT",
                "{ \"provider\": { \"anthropic\": { \"options\": { \"apiKey\": \"dummy-inline\" } } } }");
            var configurationException = Assert.Throws<InvalidOperationException>(() => adapter.ReadSnapshot());
            Assert.Contains("OPENCODE_CONFIG_CONTENT", configurationException.Message);
            Assert.Contains("provider", configurationException.Message);

            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_CONTENT", "{");
            var invalidException = Assert.Throws<InvalidOperationException>(() => adapter.ReadSnapshot());
            Assert.Contains("not valid JSON or JSONC", invalidException.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", originalAuthenticationContent);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_CONTENT", originalConfigurationContent);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_DIR", originalConfigurationDirectory);
        }
    }

    [Fact]
    public void OpenCodeConfigurationDirectoryAllowsUnrelatedSettingsButBlocksManagedValues()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateEnvironmentValidatingOpenCodeAdapter(temporary.Path);
        var overrideDirectory = System.IO.Path.Combine(temporary.Path, "external-config-directory");
        var overridePath = System.IO.Path.Combine(overrideDirectory, "opencode.jsonc");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        Directory.CreateDirectory(overrideDirectory);
        File.WriteAllText(adapter.AuthenticationFilePath, "{\"anthropic\":{\"type\":\"api\",\"key\":\"dummy-auth\"}}");
        File.WriteAllText(overridePath, "{ // unrelated\n  \"theme\": \"dark\",\n}");
        var originalAuthenticationContent = Environment.GetEnvironmentVariable("OPENCODE_AUTH_CONTENT");
        var originalConfigurationContent = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_CONTENT");
        var originalConfigurationDirectory = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_DIR");
        byte[]? allowedSnapshot = null;

        try
        {
            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", null);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_CONTENT", "{ \"autoupdate\": false }");
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_DIR", overrideDirectory);
            allowedSnapshot = adapter.ReadSnapshot();
            Assert.NotEmpty(allowedSnapshot);

            File.WriteAllText(
                overridePath,
                "{ \"model\": \"anthropic/external-model\", \"theme\": \"dark\" }");
            var exception = Assert.Throws<InvalidOperationException>(() => adapter.ReadSnapshot());
            Assert.Contains("OPENCODE_CONFIG_DIR", exception.Message);
            Assert.Contains(overridePath, exception.Message);

            File.WriteAllText(overridePath, "{");
            var invalidException = Assert.Throws<InvalidOperationException>(() => adapter.ReadSnapshot());
            Assert.Contains("could not be validated", invalidException.Message);
        }
        finally
        {
            if (allowedSnapshot is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(allowedSnapshot);
            }

            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", originalAuthenticationContent);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_CONTENT", originalConfigurationContent);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_DIR", originalConfigurationDirectory);
        }
    }

    [Fact]
    public async Task OpenCodeKnownOverridesFailBeforeVaultOrManagedFilesAreMutated()
    {
        using var temporary = new TemporaryDirectory();
        var adapter = CreateEnvironmentValidatingOpenCodeAdapter(temporary.Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.AuthenticationFilePath)!);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(adapter.ConfigurationFilePath)!);
        const string authenticationContents = "{\"anthropic\":{\"type\":\"api\",\"key\":\"dummy-live-auth\"}}";
        const string configurationContents = "{\"provider\":{\"anthropic\":{\"options\":{\"baseURL\":\"https://live.invalid/v1\"}}},\"theme\":\"keep\"}";
        File.WriteAllText(adapter.AuthenticationFilePath, authenticationContents);
        File.WriteAllText(adapter.ConfigurationFilePath, configurationContents);
        var originalAuthenticationContent = Environment.GetEnvironmentVariable("OPENCODE_AUTH_CONTENT");
        var originalConfigurationContent = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_CONTENT");
        var originalConfigurationDirectory = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_DIR");

        try
        {
            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", null);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_DIR", null);
            Environment.SetEnvironmentVariable(
                "OPENCODE_CONFIG_CONTENT",
                "{ \"model\": \"anthropic/external-model\" }");
            var vault = new AuthenticationProfileVault(System.IO.Path.Combine(temporary.Path, "vault"));
            var service = new AccountSwitchService(
                vault,
                new FixedProcessInspector(ProcessInspectionResult.Clear),
                mutexNamePrefix: $"CodingAgentAccountSwitcherTests.{Guid.NewGuid():N}");

            var result = await service.CaptureCurrentLoginAsync(
                adapter,
                "Blocked",
                cancellationToken: CancellationToken.None);

            Assert.Equal(AccountOperationStatus.Failed, result.Status);
            Assert.Contains("OPENCODE_CONFIG_CONTENT", result.ErrorMessage);
            Assert.Empty(vault.ListProfiles(AgentProvider.OpenCode));
            Assert.Null(vault.GetPendingJournal(AgentProvider.OpenCode));
            Assert.Equal(authenticationContents, File.ReadAllText(adapter.AuthenticationFilePath));
            Assert.Equal(configurationContents, File.ReadAllText(adapter.ConfigurationFilePath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCODE_AUTH_CONTENT", originalAuthenticationContent);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_CONTENT", originalConfigurationContent);
            Environment.SetEnvironmentVariable("OPENCODE_CONFIG_DIR", originalConfigurationDirectory);
        }
    }

    [Fact]
    public void OpenCodeDefaultWriteTargetIsJsoncRegardlessOfWhichLayersAlreadyExist()
    {
        using var temporary = new TemporaryDirectory();
        var configurationDirectory = System.IO.Path.Combine(
            temporary.Path,
            ".config",
            "opencode");
        Directory.CreateDirectory(configurationDirectory);
        var jsoncPath = System.IO.Path.Combine(configurationDirectory, "opencode.jsonc");
        var jsonPath = System.IO.Path.Combine(configurationDirectory, "opencode.json");

        Assert.Equal(jsoncPath, new OpenCodeAuthenticationAdapter(temporary.Path).ConfigurationFilePath);

        File.WriteAllText(jsonPath, "{}");
        Assert.Equal(jsoncPath, new OpenCodeAuthenticationAdapter(temporary.Path).ConfigurationFilePath);

        File.WriteAllText(jsoncPath, "{}");
        Assert.Equal(jsoncPath, new OpenCodeAuthenticationAdapter(temporary.Path).ConfigurationFilePath);
    }

    [Fact]
    public void OpenCodeAllowsExplicitConfigurationAndAuthenticationPaths()
    {
        using var temporary = new TemporaryDirectory();
        var configurationPath = System.IO.Path.Combine(temporary.Path, "custom", "opencode.json");
        var authenticationPath = System.IO.Path.Combine(temporary.Path, "custom", "auth.json");

        var adapter = new OpenCodeAuthenticationAdapter(
            temporary.Path,
            configurationPath,
            authenticationPath);

        Assert.Equal(configurationPath, adapter.ConfigurationFilePath);
        Assert.Equal(authenticationPath, adapter.AuthenticationFilePath);
    }

    [Fact]
    public void AuthenticationLocationScopeSeparatesDifferentCredentialPathsWithoutEmbeddingThePath()
    {
        using var temporary = new TemporaryDirectory();
        var first = new CodexAuthenticationAdapter(
            temporary.Path,
            System.IO.Path.Combine(temporary.Path, "first-codex-home"));
        var second = new CodexAuthenticationAdapter(
            temporary.Path,
            System.IO.Path.Combine(temporary.Path, "second-codex-home"));

        var firstKey = AuthenticationLocationScope.CreateStorageKey(first);
        var secondKey = AuthenticationLocationScope.CreateStorageKey(second);

        Assert.NotEqual(firstKey, secondKey);
        Assert.StartsWith("codex-", firstKey);
        Assert.DoesNotContain(temporary.Path, firstKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(70, firstKey.Length);
    }

    [Fact]
    public void OpenCodeLocationScopeIncludesTheOverridableConfigurationPath()
    {
        using var temporary = new TemporaryDirectory();
        var authenticationPath = System.IO.Path.Combine(temporary.Path, "shared", "auth.json");
        var first = new OpenCodeAuthenticationAdapter(
            temporary.Path,
            System.IO.Path.Combine(temporary.Path, "first", "opencode.json"),
            authenticationPath);
        var second = new OpenCodeAuthenticationAdapter(
            temporary.Path,
            System.IO.Path.Combine(temporary.Path, "second", "opencode.json"),
            authenticationPath);

        Assert.NotEqual(
            AuthenticationLocationScope.CreateStorageKey(first),
            AuthenticationLocationScope.CreateStorageKey(second));
    }

    private static OpenCodeAuthenticationAdapter CreateEnvironmentValidatingOpenCodeAdapter(
        string userProfileDirectory) =>
        new(
            userProfileDirectory,
            openCodeConfigurationPath: null,
            openCodeAuthenticationPath: null,
            validateKnownEnvironmentOverrides: true);
}
