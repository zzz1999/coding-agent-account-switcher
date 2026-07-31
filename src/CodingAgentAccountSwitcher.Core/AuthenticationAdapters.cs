namespace CodingAgentAccountSwitcher.Core;

public interface IAuthenticationAdapter
{
    AgentProvider Provider { get; }

    string DisplayName { get; }

    string AuthenticationFilePath { get; }

    IReadOnlyList<ProcessNameRule> BlockingProcessRules { get; }
}

public sealed class CodexAuthenticationAdapter : IAuthenticationAdapter
{
    private static readonly IReadOnlyList<ProcessNameRule> Processes = Array.AsReadOnly<ProcessNameRule>(
    [
        ProcessNameRule.Exact("ChatGPT"),
        ProcessNameRule.Exact("codex"),
        ProcessNameRule.Exact("codex-code-mode-host"),
        ProcessNameRule.Prefix("codex-command-runner-")
    ]);

    public CodexAuthenticationAdapter(
        string? userProfileDirectory = null,
        string? codexHomeDirectory = null)
    {
        var authenticationRoot = ResolveAuthenticationRoot(
            userProfileDirectory,
            codexHomeDirectory,
            "CODEX_HOME",
            ".codex");
        AuthenticationFilePath = Path.Combine(authenticationRoot, "auth.json");
    }

    public AgentProvider Provider => AgentProvider.Codex;

    public string DisplayName => "Codex";

    public string AuthenticationFilePath { get; }

    public IReadOnlyList<ProcessNameRule> BlockingProcessRules => Processes;

    private static string ResolveAuthenticationRoot(
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

public sealed class ClaudeCodeAuthenticationAdapter : IAuthenticationAdapter
{
    private static readonly IReadOnlyList<ProcessNameRule> Processes = Array.AsReadOnly<ProcessNameRule>(
    [
        ProcessNameRule.Exact("claude"),
        ProcessNameRule.Exact("claude-code")
    ]);

    public ClaudeCodeAuthenticationAdapter(
        string? userProfileDirectory = null,
        string? claudeConfigDirectory = null)
    {
        var authenticationRoot = ResolveAuthenticationRoot(
            userProfileDirectory,
            claudeConfigDirectory,
            "CLAUDE_CONFIG_DIR",
            ".claude");
        AuthenticationFilePath = Path.Combine(authenticationRoot, ".credentials.json");
    }

    public AgentProvider Provider => AgentProvider.ClaudeCode;

    public string DisplayName => "Claude Code";

    public string AuthenticationFilePath { get; }

    public IReadOnlyList<ProcessNameRule> BlockingProcessRules => Processes;

    private static string ResolveAuthenticationRoot(
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
