using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace DvauiThemeEditor;

internal sealed record AfterEffectsInstallation(string DllPath, Version Version);

internal static class AfterEffectsLocator
{
    internal static IReadOnlyList<AfterEffectsInstallation> FindInstalled()
    {
        var found = new Dictionary<string, AfterEffectsInstallation>(StringComparer.OrdinalIgnoreCase);
        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var adobe = Path.Combine(programFiles, "Adobe");
            if (!Directory.Exists(adobe)) continue;
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(adobe, "Adobe After Effects *", SearchOption.TopDirectoryOnly))
                {
                    var dll = Path.Combine(directory, "Support Files", "dvaui.dll");
                    if (!File.Exists(dll)) continue;
                    var fullPath = Path.GetFullPath(dll);
                    found[fullPath] = new AfterEffectsInstallation(fullPath, ReadVersion(fullPath));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // A locked Adobe directory should not hide other readable installs.
            }
            catch (IOException)
            {
                // A concurrently updating Creative Cloud install may be transiently unavailable.
            }
        }

        return found.Values
            .OrderByDescending(install => install.Version)
            .ThenBy(install => install.DllPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Version ReadVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(Math.Max(0, info.FileMajorPart), Math.Max(0, info.FileMinorPart),
                Math.Max(0, info.FileBuildPart), Math.Max(0, info.FilePrivatePart));
        }
        catch
        {
            return new Version(0, 0);
        }
    }
}

internal static class OriginalDllStore
{
    internal static string CaptureIfMissing(string targetPath, string originalsRoot, out bool captured)
    {
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        if (!File.Exists(fullTarget)) throw new FileNotFoundException("The selected installed dvaui.dll was not found.", fullTarget);
        EnsurePortableExecutable(fullTarget);

        // Existing snapshots from pre-signature-check builds remain usable as
        // generation baselines when their recorded SHA-256 still matches. New
        // captures must still be Adobe-signed, and Restore remains strict.
        var existing = ExistingFor(fullTarget, originalsRoot, requireAdobeSignature: false);
        if (existing is not null)
        {
            captured = false;
            return existing;
        }

        // A themed target is expected to have an invalid Adobe signature, but it is
        // safe only when a separately verified original already exists for its path.
        var signature = EnsureAdobeSigned(fullTarget);

        var key = PathKey(fullTarget);
        var snapshotDirectory = Path.Combine(originalsRoot, key);
        var originalPath = Path.Combine(snapshotDirectory, "dvaui.dll.adobe-original");
        Directory.CreateDirectory(snapshotDirectory);

        if (File.Exists(originalPath))
        {
            ValidateSnapshot(originalPath, Path.Combine(snapshotDirectory, "snapshot.json"));
            captured = false;
            return originalPath;
        }

        var temporaryPath = Path.Combine(snapshotDirectory, $"capture-{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(fullTarget, temporaryPath, false);
            var targetHash = Sha256(fullTarget);
            var capturedHash = Sha256(temporaryPath);
            if (!string.Equals(targetHash, capturedHash, StringComparison.Ordinal))
                throw new IOException("The original DLL snapshot did not match the selected file.");

            try
            {
                File.Move(temporaryPath, originalPath, false);
            }
            catch (IOException) when (File.Exists(originalPath))
            {
                // Another instance completed the same immutable capture first.
            }

            var version = FileVersionInfo.GetVersionInfo(fullTarget);
            var metadata = new
            {
                TargetPath = fullTarget,
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Sha256 = targetHash,
                AuthenticodeSubject = signature.Subject,
                AuthenticodeThumbprint = signature.Thumbprint,
                version.ProductName,
                version.ProductVersion,
                version.FileVersion
            };
            File.WriteAllText(Path.Combine(snapshotDirectory, "snapshot.json"),
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
            captured = true;
            return originalPath;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal static string? ExistingFor(string targetPath, string originalsRoot, bool requireAdobeSignature = true)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return null;
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        if (File.Exists(fullTarget))
        {
            var directDirectory = Path.Combine(originalsRoot, PathKey(fullTarget));
            var direct = Path.Combine(directDirectory, "dvaui.dll.adobe-original");
            if (File.Exists(direct))
            {
                try
                {
                    ValidateSnapshot(direct, Path.Combine(directDirectory, "snapshot.json"), requireAdobeSignature);
                    return direct;
                }
                catch (InvalidDataException)
                {
                    // A stale snapshot captured by an older AfterThemed build must not
                    // prevent a repaired, Adobe-signed DLL from getting a clean snapshot.
                }
            }
        }

        if (!Directory.Exists(originalsRoot)) return null;
        foreach (var metadataPath in Directory.EnumerateFiles(originalsRoot, "snapshot.json", SearchOption.AllDirectories))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
                if (!document.RootElement.TryGetProperty("TargetPath", out var storedTarget) ||
                    !string.Equals(Path.GetFullPath(storedTarget.GetString() ?? string.Empty), fullTarget,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var candidate = Path.Combine(Path.GetDirectoryName(metadataPath)!, "dvaui.dll.adobe-original");
                if (!File.Exists(candidate)) continue;
                ValidateSnapshot(candidate, metadataPath, requireAdobeSignature);
                return candidate;
            }
            catch (JsonException)
            {
                // Ignore unrelated or damaged metadata and continue looking for a valid snapshot.
            }
            catch (ArgumentException)
            {
                // Ignore a malformed path in metadata.
            }
            catch (InvalidDataException)
            {
                // Ignore a modified or otherwise unverifiable historical snapshot.
            }
        }

        return null;
    }

    internal static string RequireExistingOriginal(string targetPath, string originalsRoot)
    {
        var original = ExistingFor(targetPath, originalsRoot);
        return original ?? throw new InvalidOperationException(
            "No preserved Adobe original exists for this installation. Restore will not use the currently installed DLL. " +
            "Repair or reinstall this After Effects version through Creative Cloud, then select its fresh dvaui.dll once so AfterThemed can preserve it.");
    }

    internal static string CreateRestoreDll(string targetPath, string originalsRoot, string outputPath)
    {
        var original = RequireExistingOriginal(targetPath, originalsRoot);
        var fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        var temporary = fullOutput + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(original, temporary, false);
            if (!string.Equals(Sha256(original), Sha256(temporary), StringComparison.Ordinal))
                throw new IOException("The restore DLL did not match the preserved Adobe original.");
            File.Move(temporary, fullOutput, true);
            ValidateSnapshot(fullOutput, Path.Combine(Path.GetDirectoryName(original)!, "snapshot.json"));
            return fullOutput;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string PathKey(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var version = FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown-version";
        var contentHash = Sha256(path);
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(normalized + "|" + version + "|" + contentHash)))[..16];
    }

    internal static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void ValidateSnapshot(string originalPath, string metadataPath, bool requireAdobeSignature = true)
    {
        EnsurePortableExecutable(originalPath);
        if (requireAdobeSignature) EnsureAdobeSigned(originalPath);
        if (!File.Exists(metadataPath))
            throw new InvalidDataException("The preserved original is missing its verification metadata.");

        using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        if (!document.RootElement.TryGetProperty("Sha256", out var expectedElement))
            throw new InvalidDataException("The preserved original has no recorded SHA-256 hash.");
        var expected = expectedElement.GetString();
        var actual = Sha256(originalPath);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The preserved original failed SHA-256 verification and will not be restored.");
    }

    private static AdobeSignature EnsureAdobeSigned(string path)
    {
        var fileInfo = new WinTrustFileInfo
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            FilePath = Path.GetFullPath(path)
        };
        var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        var structureWritten = false;
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
            structureWritten = true;
            var trustData = new WinTrustData
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
                UiChoice = 2,       // WTD_UI_NONE
                UnionChoice = 1,    // WTD_CHOICE_FILE
                FileInfo = fileInfoPointer,
                StateAction = 0,    // WTD_STATEACTION_IGNORE
                ProviderFlags = 0x00000010 // WTD_REVOCATION_CHECK_NONE
            };
            var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            var status = WinVerifyTrust(IntPtr.Zero, ref action, ref trustData);
            if (status != 0)
                throw InvalidAdobeOriginal(
                    $"Windows Authenticode verification returned 0x{unchecked((uint)status):X8}.");

            try
            {
#pragma warning disable SYSLIB0057 // Required to read the signer embedded in a signed PE, not a standalone certificate file.
                using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
                var subject = certificate.Subject ?? string.Empty;
                if (!subject.Contains("Adobe", StringComparison.OrdinalIgnoreCase))
                    throw InvalidAdobeOriginal($"The signer is '{subject}', not Adobe.");
                return new AdobeSignature(subject, certificate.GetCertHashString());
            }
            catch (CryptographicException exception)
            {
                throw InvalidAdobeOriginal("The embedded Adobe signing certificate could not be read.", exception);
            }
        }
        finally
        {
            if (structureWritten) Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
            Marshal.FreeHGlobal(fileInfoPointer);
        }
    }

    private static InvalidDataException InvalidAdobeOriginal(string detail, Exception? inner = null) =>
        new("This dvaui.dll is modified or its Adobe signature is invalid. AfterThemed will not preserve, " +
            "theme, or restore it as an original. Repair this After Effects version in Creative Cloud, then " +
            $"select the fresh Adobe-signed dvaui.dll. {detail}", inner);

    private sealed record AdobeSignature(string Subject, string Thumbprint);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        internal uint StructSize;
        [MarshalAs(UnmanagedType.LPWStr)] internal string FilePath;
        internal IntPtr FileHandle;
        internal IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        internal uint StructSize;
        internal IntPtr PolicyCallbackData;
        internal IntPtr SipClientData;
        internal uint UiChoice;
        internal uint RevocationChecks;
        internal uint UnionChoice;
        internal IntPtr FileInfo;
        internal uint StateAction;
        internal IntPtr StateData;
        internal IntPtr UrlReference;
        internal uint ProviderFlags;
        internal uint UiContext;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid actionId, ref WinTrustData data);

    private static void EnsurePortableExecutable(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
            throw new InvalidDataException("The selected file is not a Windows DLL/PE file.");
    }
}
