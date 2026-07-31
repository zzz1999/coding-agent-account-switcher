namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class AuthenticationAdapterTests
{
    [Fact]
    public void AdaptersResolveOnlyTheirFixedAuthenticationFiles()
    {
        using var temporary = new TemporaryDirectory();

        var codex = new CodexAuthenticationAdapter(temporary.Path);
        var claude = new ClaudeCodeAuthenticationAdapter(temporary.Path);

        Assert.Equal(
            System.IO.Path.Combine(temporary.Path, ".codex", "auth.json"),
            codex.AuthenticationFilePath);
        Assert.Equal(
            System.IO.Path.Combine(temporary.Path, ".claude", ".credentials.json"),
            claude.AuthenticationFilePath);
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("ChatGPT"));
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("codex"));
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("codex-code-mode-host"));
        Assert.Contains(codex.BlockingProcessRules, rule => rule.Matches("codex-command-runner-42"));
        Assert.Contains(claude.BlockingProcessRules, rule => rule.Matches("Claude"));
        Assert.Contains(claude.BlockingProcessRules, rule => rule.Matches("claude-code"));
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
}
