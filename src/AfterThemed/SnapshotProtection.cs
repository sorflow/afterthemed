using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DvauiThemeEditor;

/// <summary>
/// DPAPI authenticates the capture metadata for this Windows user. Read-only attributes protect
/// against accidental edits; neither mechanism claims protection against the user or an administrator.
/// The second copy recovers a damaged primary, not loss of the disk or Windows profile.
/// </summary>
internal static class SnapshotProtection
{
    internal static void Seal(string original, string metadata)
    {
        var proof = Path.Combine(Path.GetDirectoryName(metadata)!, "snapshot.proof");
        var replica = Path.Combine(Path.GetDirectoryName(metadata)!, "original.recovery");
        var bytes = File.ReadAllBytes(metadata);
        if (!File.Exists(replica)) File.Copy(original, replica, false);
        if (OriginalDllStore.Sha256(original) != OriginalDllStore.Sha256(replica))
            throw new InvalidDataException("The recovery copy failed SHA-256 verification.");
        foreach (var path in new[] { original, replica })
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }
        using (var stream = new FileStream(proof, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(Transform(bytes, protect: true));
            stream.Flush(flushToDisk: true);
        }
        foreach (var path in new[] { original, metadata, proof, replica })
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    }

    internal static bool HasProof(string metadata) =>
        File.Exists(Path.Combine(Path.GetDirectoryName(metadata)!, "snapshot.proof"));

    internal static byte[] Verify(string metadata)
    {
        try
        {
            var proof = Path.Combine(Path.GetDirectoryName(metadata)!, "snapshot.proof");
            var authenticated = Transform(File.ReadAllBytes(proof), protect: false);
            if (!CryptographicOperations.FixedTimeEquals(authenticated, File.ReadAllBytes(metadata)))
                throw new InvalidDataException("The protected original's metadata was changed.");
            return authenticated;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException("The original's protection record cannot be verified by this Windows profile. Repair After Effects to capture a fresh original.", ex);
        }
    }

    internal static string RecoverIfNeeded(string original, string metadata, string expectedHash)
    {
        if (File.Exists(original) && OriginalDllStore.Sha256(original).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            return original;
        var replica = Path.Combine(Path.GetDirectoryName(metadata)!, "original.recovery");
        if (!File.Exists(replica) || !OriginalDllStore.Sha256(replica).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Both copies of the preserved original are missing or damaged. No Adobe files were changed.");
        // Use the authenticated replica directly. Preserve damaged evidence for diagnosis.
        return replica;
    }

    private static byte[] Transform(byte[] bytes, bool protect)
    {
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        var output = new Blob();
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var succeeded = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!succeeded) throw new CryptographicException(Marshal.GetLastWin32Error());
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { internal int Length; internal IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, uint flags, out Blob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
