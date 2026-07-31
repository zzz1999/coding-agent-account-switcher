using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class DpapiCurrentUserProtectorTests
{
    [Fact]
    public void ProtectAndUnprotectRoundTripsOpaqueBytesAndBindsEntropy()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = new DpapiCurrentUserProtector();
        byte[] plaintext = [0x00, 0xFF, 0xC3, 0x28, 0x00, 0x7F];
        byte[] entropy = [1, 2, 3, 4];
        byte[] wrongEntropy = [4, 3, 2, 1];
        var protectedBytes = protector.Protect(plaintext, entropy);

        try
        {
            Assert.NotEqual(plaintext, protectedBytes);
            Assert.Equal(plaintext, protector.Unprotect(protectedBytes, entropy));
            Assert.Throws<CryptographicException>(() => protector.Unprotect(protectedBytes, wrongEntropy));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(entropy);
            CryptographicOperations.ZeroMemory(wrongEntropy);
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }
}
