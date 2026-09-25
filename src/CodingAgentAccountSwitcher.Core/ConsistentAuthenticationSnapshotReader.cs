using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core;

internal static class ConsistentAuthenticationSnapshotReader
{
    private const int MaximumReadAttempts = 3;

    internal static byte[] Read(
        AgentProvider provider,
        string displayName,
        string authenticationFilePath,
        Func<string, byte[]?> readAuthenticationFile,
        Func<byte[]> captureConfiguration,
        Func<byte[], bool> hasManagedValues,
        bool allowIncomplete)
    {
        for (var attempt = 0; attempt < MaximumReadAttempts; attempt++)
        {
            byte[]? authenticationBefore = null;
            byte[]? authenticationAfter = null;
            byte[]? configurationBefore = null;
            byte[]? configurationAfter = null;
            try
            {
                authenticationBefore = readAuthenticationFile(authenticationFilePath);
                configurationBefore = captureConfiguration();
                authenticationAfter = readAuthenticationFile(authenticationFilePath);
                configurationAfter = captureConfiguration();

                // Compare the complete captured configuration, including OpenCode's
                // individual layers, rather than only its merged effective values.
                // Saving is allowed while providers run, but must not accept a
                // credential/configuration pair observed across a detected change.
                if (!OptionalBytesEqual(authenticationBefore, authenticationAfter) ||
                    !AuthenticationSnapshotFiles.BytesEqual(configurationBefore, configurationAfter))
                {
                    continue;
                }

                if (authenticationAfter is null &&
                    !hasManagedValues(configurationAfter) &&
                    !allowIncomplete)
                {
                    throw new FileNotFoundException(
                        $"Neither {displayName} credentials nor managed provider settings were found.",
                        authenticationFilePath);
                }

                return AuthenticationSnapshotCodec.Encode(new ManagedAuthenticationSnapshot
                {
                    Provider = provider,
                    AuthenticationFileExists = authenticationAfter is not null,
                    AuthenticationFileContents = authenticationAfter,
                    ManagedConfiguration = configurationAfter
                });
            }
            finally
            {
                ZeroIfPresent(authenticationBefore);
                ZeroIfPresent(authenticationAfter);
                ZeroIfPresent(configurationBefore);
                ZeroIfPresent(configurationAfter);
            }
        }

        throw new IOException(
            $"The {displayName} credentials or provider settings changed while the account snapshot was being read. Try saving again.");
    }

    private static bool OptionalBytesEqual(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null &&
            AuthenticationSnapshotFiles.BytesEqual(left, right);

    private static void ZeroIfPresent(byte[]? bytes)
    {
        if (bytes is not null)
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
