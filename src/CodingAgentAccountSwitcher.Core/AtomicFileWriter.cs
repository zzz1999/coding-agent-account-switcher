namespace CodingAgentAccountSwitcher.Core;

public interface IAtomicFileWriter
{
    void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null);

    void DeleteFile(string destinationPath);

    void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId);
}

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    private const string TemporaryFilePrefix = ".coding-agent-account-switcher";

    public void WriteAllBytes(string destinationPath, byte[] contents, Guid? transactionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(contents);
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("The transaction identifier cannot be empty.", nameof(transactionId));
        }

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullDestinationPath) ??
            throw new ArgumentException("The destination must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);

        var effectiveTransactionId = transactionId ?? Guid.NewGuid();
        var temporaryPath = GetTemporaryPath(fullDestinationPath, effectiveTransactionId);
        var backupPath = transactionId.HasValue
            ? GetBackupPath(fullDestinationPath, effectiveTransactionId)
            : null;

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 1,
                Options = FileOptions.WriteThrough
            };

            using (var stream = new FileStream(temporaryPath, options))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullDestinationPath))
            {
                File.Replace(temporaryPath, fullDestinationPath, backupPath,
                    ignoreMetadataErrors: false);
            }
            else
            {
                File.Move(temporaryPath, fullDestinationPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            if (backupPath is not null && File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    public void DeleteOwnedTransactionFiles(string destinationPath, Guid transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("The transaction identifier cannot be empty.", nameof(transactionId));
        }

        var temporaryPath = GetTemporaryPath(Path.GetFullPath(destinationPath), transactionId);
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        var backupPath = GetBackupPath(Path.GetFullPath(destinationPath), transactionId);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    public void DeleteFile(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        if (File.Exists(fullDestinationPath))
        {
            File.Delete(fullDestinationPath);
        }
    }

    private static string GetTemporaryPath(string fullDestinationPath, Guid transactionId)
    {
        var directory = Path.GetDirectoryName(fullDestinationPath) ??
            throw new ArgumentException("The destination must have a parent directory.", nameof(fullDestinationPath));
        return Path.Combine(
            directory,
            $"{TemporaryFilePrefix}.{Path.GetFileName(fullDestinationPath)}.{transactionId:N}.tmp");
    }

    private static string GetBackupPath(string fullDestinationPath, Guid transactionId)
    {
        var directory = Path.GetDirectoryName(fullDestinationPath) ??
            throw new ArgumentException("The destination must have a parent directory.", nameof(fullDestinationPath));
        return Path.Combine(
            directory,
            $"{TemporaryFilePrefix}.{Path.GetFileName(fullDestinationPath)}.{transactionId:N}.backup");
    }
}
