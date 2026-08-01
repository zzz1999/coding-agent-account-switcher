using System.Security.Cryptography;
using System.Text;

namespace CodingAgentAccountSwitcher.Core;

public static class AuthenticationLocationScope
{
    public static string CreateStorageKey(IAuthenticationAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        var providerKey = adapter.Provider.ToStorageKey();
        // Codex and Claude Code retain their original single-path scope material so
        // existing encrypted profile directories remain discoverable. OpenCode is
        // new and must include its credential store plus every global/custom config layer.
        var normalizedAuthenticationPath = adapter.Provider == AgentProvider.OpenCode
            ? string.Join(
                "|",
                adapter.ManagedFilePaths.Select(static path =>
                    Path.GetFullPath(path).ToUpperInvariant()))
            : Path.GetFullPath(adapter.AuthenticationFilePath).ToUpperInvariant();
        var scopeMaterial = Encoding.UTF8.GetBytes(
            $"CodingAgentAccountSwitcher|location-v1|{providerKey}|{normalizedAuthenticationPath}");
        byte[] scopeHash;
        try
        {
            scopeHash = SHA256.HashData(scopeMaterial);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(scopeMaterial);
        }

        try
        {
            return $"{providerKey}-{Convert.ToHexString(scopeHash).ToLowerInvariant()}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(scopeHash);
        }
    }
}
