namespace CodingAgentAccountSwitcher.Core.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public void TransactionalReplacementCleansExactTemporaryAndBackupFiles()
    {
        using var temporary = new TemporaryDirectory();
        var destinationPath = System.IO.Path.Combine(temporary.Path, "auth.json");
        var transactionId = Guid.NewGuid();
        File.WriteAllBytes(destinationPath, [1, 2, 3]);

        new AtomicFileWriter().WriteAllBytes(destinationPath, [4, 5, 6], transactionId);

        Assert.Equal([4, 5, 6], File.ReadAllBytes(destinationPath));
        Assert.False(File.Exists(GetOwnedPath(destinationPath, transactionId, "tmp")));
        Assert.False(File.Exists(GetOwnedPath(destinationPath, transactionId, "backup")));
    }

    [Fact]
    public void CleanupDeletesOnlyFilesOwnedByTheSpecifiedTransaction()
    {
        using var temporary = new TemporaryDirectory();
        var destinationPath = System.IO.Path.Combine(temporary.Path, "auth.json");
        var ownedTransactionId = Guid.NewGuid();
        var unrelatedTransactionId = Guid.NewGuid();
        var ownedTemporaryPath = GetOwnedPath(destinationPath, ownedTransactionId, "tmp");
        var ownedBackupPath = GetOwnedPath(destinationPath, ownedTransactionId, "backup");
        var unrelatedTemporaryPath = GetOwnedPath(destinationPath, unrelatedTransactionId, "tmp");
        File.WriteAllBytes(ownedTemporaryPath, [1]);
        File.WriteAllBytes(ownedBackupPath, [2]);
        File.WriteAllBytes(unrelatedTemporaryPath, [3]);

        new AtomicFileWriter().DeleteOwnedTransactionFiles(destinationPath, ownedTransactionId);

        Assert.False(File.Exists(ownedTemporaryPath));
        Assert.False(File.Exists(ownedBackupPath));
        Assert.True(File.Exists(unrelatedTemporaryPath));
    }

    private static string GetOwnedPath(string destinationPath, Guid transactionId, string extension) =>
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(destinationPath)!,
            $".coding-agent-account-switcher.{System.IO.Path.GetFileName(destinationPath)}.{transactionId:N}.{extension}");
}
