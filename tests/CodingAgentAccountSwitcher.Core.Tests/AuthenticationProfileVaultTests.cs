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

    [Fact]
    public void SafeListingIsolatesCorruptedMetadataWithoutChangingItOrHidingValidProfiles()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var valid = vault.CreateProfile(AgentProvider.Codex, "Personal", [1, 2, 3]);
        var damaged = vault.CreateProfile(AgentProvider.Codex, "Work", [4, 5, 6]);
        var damagedMetadataPath = Directory.GetFiles(
            temporary.Path,
            $"{damaged.ProfileId:N}.metadata.json",
            SearchOption.AllDirectories).Single();
        byte[] damagedMetadata = "{not-valid-json"u8.ToArray();
        File.WriteAllBytes(damagedMetadataPath, damagedMetadata);

        var result = vault.ListProfilesWithIssues(AgentProvider.Codex);

        Assert.Equal(valid.ProfileId, Assert.Single(result.Profiles).ProfileId);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(Path.GetFileName(damagedMetadataPath), issue.FileName);
        Assert.Equal(damagedMetadata, File.ReadAllBytes(damagedMetadataPath));
        Assert.Throws<InvalidDataException>(() => vault.ListProfiles(AgentProvider.Codex));
    }

    [Fact]
    public void SafeListingRejectsMetadataWhoseProfileIdDoesNotMatchItsFileName()
    {
        using var temporary = new TemporaryDirectory();
        var vault = new AuthenticationProfileVault(temporary.Path);
        var valid = vault.CreateProfile(AgentProvider.ClaudeCode, "Personal", [1, 2, 3]);
        var mismatched = vault.CreateProfile(AgentProvider.ClaudeCode, "Work", [4, 5, 6]);
        var metadataPath = Directory.GetFiles(
            temporary.Path,
            $"{mismatched.ProfileId:N}.metadata.json",
            SearchOption.AllDirectories).Single();
        var originalMetadata = File.ReadAllText(metadataPath);
        var alteredMetadata = originalMetadata.Replace(
            mismatched.ProfileId.ToString(),
            Guid.NewGuid().ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(originalMetadata, alteredMetadata);
        File.WriteAllText(metadataPath, alteredMetadata);

        var result = vault.ListProfilesWithIssues(AgentProvider.ClaudeCode);

        Assert.Equal(valid.ProfileId, Assert.Single(result.Profiles).ProfileId);
        Assert.Equal(Path.GetFileName(metadataPath), Assert.Single(result.Issues).FileName);
        Assert.Equal(alteredMetadata, File.ReadAllText(metadataPath));
    }

    [Fact]
    public void SafeListingDoesNotConvertDirectoryEnumerationFailureIntoAProfileIssue()
    {
        var exception = Assert.Throws<IOException>(() =>
            AuthenticationProfileVault.ListProfilesWithIssues(
                AgentProvider.Codex,
                new ThrowingMetadataPathEnumerable()));

        Assert.Equal("Provider directory enumeration failed.", exception.Message);
    }

    [Fact]
    public void CaptureSnapshotReadFailureDoesNotStartMutationOrRollbackWrites()
    {
        using var temporary = new TemporaryDirectory();
        var writer = new CountingAtomicFileWriter();
        var vault = new AuthenticationProfileVault(temporary.Path, atomicWriter: writer);
        var activeStatePath = Path.Combine(temporary.Path, "state", "active-codex.json");
        Directory.CreateDirectory(Path.GetDirectoryName(activeStatePath)!);
        File.WriteAllBytes(activeStatePath, []);

        Assert.Throws<InvalidDataException>(() =>
            vault.CaptureProfileAndSetActive(
                AgentProvider.Codex,
                "Personal",
                [1, 2, 3],
                profileToReplace: null));

        Assert.Equal(0, writer.WriteCount);
        Assert.Empty(vault.ListProfiles(AgentProvider.Codex));
        Assert.Empty(File.ReadAllBytes(activeStatePath));
    }

    [Fact]
    public void FailedActiveStateRollbackKeepsNewProfileReferencedByCommittedState()
    {
        using var temporary = new TemporaryDirectory();
        var writer = new CommitThenFailActiveStateAndRejectRollbackWriter();
        var vault = new AuthenticationProfileVault(temporary.Path, atomicWriter: writer);
        var original = vault.CaptureProfileAndSetActive(
            AgentProvider.Codex,
            "Personal",
            [1, 2, 3],
            profileToReplace: null);
        writer.Enabled = true;

        Assert.Throws<IOException>(() =>
            vault.CaptureProfileAndSetActive(
                AgentProvider.Codex,
                "Work",
                [4, 5, 6],
                profileToReplace: null));

        var committedActiveState = Assert.IsType<ActiveProfileState>(
            vault.GetActiveProfile(AgentProvider.Codex));
        Assert.NotEqual(original.ProfileId, committedActiveState.ProfileId);
        Assert.Equal("Work", vault.GetProfile(AgentProvider.Codex, committedActiveState.ProfileId).DisplayName);
        var committedCredential = vault.LoadCredential(AgentProvider.Codex, committedActiveState.ProfileId);
        try
        {
            Assert.Equal([4, 5, 6], committedCredential);
            Assert.Equal(2, vault.ListProfiles(AgentProvider.Codex).Count);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(committedCredential);
        }
    }

    private sealed class ThrowingMetadataPathEnumerable : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator() =>
            throw new IOException("Provider directory enumeration failed.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingAtomicFileWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();

        public int WriteCount { get; private set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            WriteCount++;
            _inner.WriteAllBytes(destinationPath, contents, transactionId);
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }

    private sealed class CommitThenFailActiveStateAndRejectRollbackWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private int _activeStateWriteCount;

        public bool Enabled { get; set; }

        public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
        {
            if (Enabled && Path.GetFileName(destinationPath).StartsWith("active-", StringComparison.Ordinal))
            {
                _activeStateWriteCount++;
                if (_activeStateWriteCount == 1)
                {
                    _inner.WriteAllBytes(destinationPath, contents, transactionId);
                }

                throw new IOException("Injected active-state write or rollback failure.");
            }

            _inner.WriteAllBytes(destinationPath, contents, transactionId);
        }

        public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId) =>
            _inner.DeleteOwnedTransactionFiles(destinationPath, transactionId);

        public void DeleteFile(string destinationPath) => _inner.DeleteFile(destinationPath);
    }
}
