namespace CodingAgentAccountSwitcher.Core;

public enum AgentProvider
{
    Codex,
    ClaudeCode
}

internal static class AgentProviderExtensions
{
    public static string ToStorageKey(this AgentProvider provider) => provider switch
    {
        AgentProvider.Codex => "codex",
        AgentProvider.ClaudeCode => "claude-code",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };
}
