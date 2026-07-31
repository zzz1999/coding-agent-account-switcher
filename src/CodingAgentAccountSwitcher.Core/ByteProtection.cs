using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CodingAgentAccountSwitcher.Core;

public interface IByteProtector
{
    byte[] Protect(byte[] plaintext, byte[] additionalEntropy);

    byte[] Unprotect(byte[] protectedData, byte[] additionalEntropy);
}

public sealed class DpapiCurrentUserProtector : IByteProtector
{
    private const int CryptprotectUiForbidden = 0x1;

    public byte[] Protect(byte[] plaintext, byte[] additionalEntropy)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(additionalEntropy);
        EnsureWindows();

        return Transform(plaintext, additionalEntropy, protect: true);
    }

    public byte[] Unprotect(byte[] protectedData, byte[] additionalEntropy)
    {
        ArgumentNullException.ThrowIfNull(protectedData);
        ArgumentNullException.ThrowIfNull(additionalEntropy);
        EnsureWindows();

        return Transform(protectedData, additionalEntropy, protect: false);
    }

    private static byte[] Transform(byte[] inputBytes, byte[] entropyBytes, bool protect)
    {
        var input = AllocateBlob(inputBytes);
        var entropy = AllocateBlob(entropyBytes);
        var output = default(DataBlob);
        var description = IntPtr.Zero;

        try
        {
            var succeeded = protect
                ? CryptProtectData(ref input, null, ref entropy, IntPtr.Zero, IntPtr.Zero,
                    CryptprotectUiForbidden, out output)
                : CryptUnprotectData(ref input, out description, ref entropy, IntPtr.Zero, IntPtr.Zero,
                    CryptprotectUiForbidden, out output);

            if (!succeeded)
            {
                var error = Marshal.GetLastWin32Error();
                throw new CryptographicException(
                    $"Windows DPAPI failed with error {error}: {new Win32Exception(error).Message}");
            }

            if (output.Size < 0 || (output.Size > 0 && output.Data == IntPtr.Zero))
            {
                throw new CryptographicException("Windows DPAPI returned an invalid data buffer.");
            }

            var result = new byte[output.Size];
            if (output.Size > 0)
            {
                Marshal.Copy(output.Data, result, 0, output.Size);
            }

            return result;
        }
        finally
        {
            FreeAllocatedBlob(ref input, clear: true);
            FreeAllocatedBlob(ref entropy, clear: true);
            FreeLocalBlob(ref output, clear: !protect);

            if (description != IntPtr.Zero)
            {
                LocalFree(description);
            }
        }
    }

    private static DataBlob AllocateBlob(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return default;
        }

        var pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return new DataBlob { Size = bytes.Length, Data = pointer };
    }

    private static void FreeAllocatedBlob(ref DataBlob blob, bool clear)
    {
        if (blob.Data == IntPtr.Zero)
        {
            return;
        }

        if (clear)
        {
            ClearUnmanagedMemory(blob.Data, blob.Size);
        }

        Marshal.FreeHGlobal(blob.Data);
        blob = default;
    }

    private static void FreeLocalBlob(ref DataBlob blob, bool clear)
    {
        if (blob.Data == IntPtr.Zero)
        {
            return;
        }

        if (clear)
        {
            ClearUnmanagedMemory(blob.Data, blob.Size);
        }

        LocalFree(blob.Data);
        blob = default;
    }

    private static void ClearUnmanagedMemory(IntPtr pointer, int length)
    {
        for (var index = 0; index < length; index++)
        {
            Marshal.WriteByte(pointer, index, 0);
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI is only available on Windows.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStructure,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out IntPtr dataDescription,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStructure,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
