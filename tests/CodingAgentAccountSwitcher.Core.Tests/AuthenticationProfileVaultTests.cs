using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class AuthenticationProfileVaultTests
{
    [Fact]
    public void VaultRoundTripsOpaqueBytesAndKeepsNonSensitiveMetadataSeparate()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        byte[] credential = [0x00, 0xFF, 0xC3, 0x28, 0x10, 0x00];

        var profile = vault.CreateProfile(AgentProvider.Codex, "Personal", credential);
        var loaded = vault.LoadCredential(AgentProvider.Codex, profile.ProfileId);

        try
        {
            Assert.Equal(credential, loaded);
            Assert.Equal("Personal", vault.GetProfile(AgentProvider.Codex, profile.ProfileId).DisplayName);
            Assert.Single(vault.ListProfiles(AgentProvider.Codex));
            Assert.Empty(vault.ListProfiles(AgentProvider.ClaudeCode));

            var metadataPath = Directory.GetFiles(temporary.Path, "*.metadata.json", SearchOption.AllDirectories).Single();
            var metadataText = File.ReadAllText(metadataPath);
            Assert.Contains("Personal", metadataText);
            Assert.DoesNotContain(Convert.ToBase64String(credential), metadataText);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credential);
            CryptographicOperations.ZeroMemory(loaded);
        }
    }

    [Fact]
    public void CorruptedProtectedBlobCannotBeLoaded()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var profile = vault.CreateProfile(AgentProvider.ClaudeCode, "Work", [1, 2, 3]);
        var blobPath = Directory.GetFiles(temporary.Path, $"{profile.ProfileId:N}.vault", SearchOption.AllDirectories).Single();
        File.WriteAllBytes(blobPath, [9, 8, 7]);

        Assert.Throws<CryptographicException>(() =>
            vault.LoadCredential(AgentProvider.ClaudeCode, profile.ProfileId));
    }
}
