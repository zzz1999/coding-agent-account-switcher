using System.Security.Cryptography;
using System.Text;

namespace CodingAgentAccountSwitcher.Core;

public static class AuthenticationLocationScope
{
    public static string CreateStorageKey(IAuthenticationAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        var providerKey = adapter.Provider.ToStorageKey();
        var normalizedAuthenticationPath = Path.GetFullPath(adapter.AuthenticationFilePath)
            .ToUpperInvariant();
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
