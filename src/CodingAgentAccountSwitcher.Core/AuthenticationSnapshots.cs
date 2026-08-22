using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingAgentAccountSwitcher.Core;

internal sealed record ManagedAuthenticationSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required AgentProvider Provider { get; init; }

    public required bool AuthenticationFileExists { get; init; }

    public byte[]? AuthenticationFileContents { get; init; }

    public required byte[] ManagedConfiguration { get; init; }
}

internal static class AuthenticationSnapshotCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes(
        "CodingAgentAccountSwitcher:managed-snapshot:v1\0");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static byte[] Encode(ManagedAuthenticationSnapshot snapshot)
    {
        Validate(snapshot);
        var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        try
        {
            if (json.Length > AuthenticationProfileVault.MaximumCredentialSizeBytes - Magic.Length)
            {
                throw new InvalidDataException("The account snapshot is too large.");
            }

            var result = new byte[Magic.Length + json.Length];
            Magic.CopyTo(result, 0);
            json.CopyTo(result, Magic.Length);
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    internal static bool TryDecode(byte[] bytes, out ManagedAuthenticationSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        snapshot = null;
        if (bytes.Length <= Magic.Length || !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            return false;
        }

        try
        {
            snapshot = JsonSerializer.Deserialize<ManagedAuthenticationSnapshot>(
                bytes.AsSpan(Magic.Length),
                JsonOptions);
            if (snapshot is null)
            {
                throw new InvalidDataException("The managed account snapshot is empty.");
            }

            Validate(snapshot);
            return true;
        }
        catch (JsonException exception)
        {
            Zero(snapshot);
            snapshot = null;
            throw new InvalidDataException("The managed account snapshot is invalid.", exception);
        }
        catch
        {
            Zero(snapshot);
            snapshot = null;
            throw;
        }
    }

    internal static void Zero(ManagedAuthenticationSnapshot? snapshot)
    {
        if (snapshot?.AuthenticationFileContents is not null)
        {
            CryptographicOperations.ZeroMemory(snapshot.AuthenticationFileContents);
        }

        if (snapshot?.ManagedConfiguration is not null)
        {
            CryptographicOperations.ZeroMemory(snapshot.ManagedConfiguration);
        }
    }

    private static void Validate(ManagedAuthenticationSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != ManagedAuthenticationSnapshot.CurrentSchemaVersion ||
            !Enum.IsDefined(snapshot.Provider))
        {
            throw new InvalidDataException("The managed account snapshot schema is unsupported.");
        }

        if (snapshot.AuthenticationFileExists != (snapshot.AuthenticationFileContents is not null))
        {
            throw new InvalidDataException("The managed account snapshot authentication state is inconsistent.");
        }

        if (snapshot.AuthenticationFileContents is { Length: 0 } ||
            snapshot.AuthenticationFileContents is { Length: > AuthenticationProfileVault.MaximumCredentialSizeBytes })
        {
            throw new InvalidDataException("The managed account snapshot authentication file has an invalid size.");
        }

        if (snapshot.ManagedConfiguration is null ||
            snapshot.ManagedConfiguration.Length > ManagedConfigurationFiles.MaximumConfigurationSizeBytes)
        {
            throw new InvalidDataException("The managed account snapshot configuration has an invalid size.");
        }
    }
}

internal static class AuthenticationSnapshotFiles
{
    internal static byte[]? ReadOptionalAuthenticationFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None,
            bufferSize: 1,
            options: FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > AuthenticationProfileVault.MaximumCredentialSizeBytes ||
            stream.Length > int.MaxValue)
        {
            throw new InvalidDataException("The authentication file has an invalid size.");
        }

        var bytes = new byte[(int)stream.Length];
        try
        {
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    internal static byte[] ReadRequiredAuthenticationFile(string path) =>
        ReadOptionalAuthenticationFile(path) ??
        throw new FileNotFoundException("The authentication file does not exist.", path);

    internal static void ReplaceAuthenticationFile(
        IAtomicFileWriter atomicWriter,
        string path,
        bool exists,
        byte[]? contents,
        Guid transactionId)
    {
        if (exists)
        {
            // Authentication files are opaque account credentials. Always replace the
            // complete file; never parse, merge, or preserve fields from the live account.
            atomicWriter.WriteAllBytes(path, contents!, transactionId);
        }
        else if (File.Exists(path))
        {
            // A journaled copy of the full source snapshot already exists. If power is
            // lost after this deletion, normal transaction recovery recreates it.
            atomicWriter.DeleteFile(path);
        }
    }

    internal static bool BytesEqual(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}
