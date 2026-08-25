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
    internal static string CaptureIfMissing(
        string targetPath,
        string originalsRoot,
        out bool captured,
        Func<string, AdobeSignature>? signatureInspector = null)
    {
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        if (!File.Exists(fullTarget)) throw new FileNotFoundException("The selected installed dvaui.dll was not found.", fullTarget);
        EnsurePortableExecutable(fullTarget);

        // Reuse an exact snapshot before checking the current target's signature.
        // This keeps snapshots from pre-signature-check builds usable when their
        // recorded SHA-256 still matches the selected file byte-for-byte.
        var existing = ExistingExactFor(fullTarget, originalsRoot, requireAdobeSignature: false);
        if (existing is not null)
        {
            MarkActiveSnapshot(fullTarget, originalsRoot, existing);
            captured = false;
            return existing;
        }

        AdobeSignature signature;
        try
        {
            // A fresh Adobe update must be captured even when an older snapshot has
            // the same installation path. Path-only reuse can silently downgrade AE.
            signature = (signatureInspector ?? EnsureAdobeSigned)(fullTarget);
        }
        catch (InvalidDataException)
        {
            // A themed target is expected to have an invalid Adobe signature. It is
            // safe only when a verified original for the same path and file version
            // already exists.
            existing = ExistingFor(fullTarget, originalsRoot, requireAdobeSignature: false);
            if (existing is not null)
            {
                MarkActiveSnapshot(fullTarget, originalsRoot, existing);
                captured = false;
                return existing;
            }
            throw;
        }

        var key = PathKey(fullTarget);
        var snapshotDirectory = Path.Combine(originalsRoot, key);
        var originalPath = Path.Combine(snapshotDirectory, "dvaui.dll.adobe-original");
        Directory.CreateDirectory(snapshotDirectory);

        if (File.Exists(originalPath))
        {
            ValidateSnapshot(originalPath, Path.Combine(snapshotDirectory, "snapshot.json"));
            MarkActiveSnapshot(fullTarget, originalsRoot, originalPath);
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
            MarkActiveSnapshot(fullTarget, originalsRoot, originalPath);
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
        var exact = ExistingExactFor(fullTarget, originalsRoot, requireAdobeSignature);
        if (exact is not null) return exact;
        var active = ExistingActiveFor(fullTarget, originalsRoot, requireAdobeSignature);
        if (active is not null) return active;

        if (!Directory.Exists(originalsRoot)) return null;
        var historical = new List<HistoricalSnapshot>();
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
                if (File.Exists(fullTarget) && !HaveSameFileVersion(candidate, fullTarget)) continue;
                ValidateSnapshot(candidate, metadataPath, requireAdobeSignature);
                var capturedAt = document.RootElement.TryGetProperty("CapturedAtUtc", out var capturedElement) &&
                                 capturedElement.TryGetDateTimeOffset(out var parsedCapturedAt)
                    ? parsedCapturedAt
                    : DateTimeOffset.MinValue;
                var hash = document.RootElement.TryGetProperty("Sha256", out var hashElement)
                    ? hashElement.GetString() ?? Sha256(candidate)
                    : Sha256(candidate);
                historical.Add(new HistoricalSnapshot(candidate, capturedAt, hash));
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

        if (historical.Count == 0) return null;
        var newestTimestamp = historical.Max(snapshot => snapshot.CapturedAtUtc);
        var newest = historical.Where(snapshot => snapshot.CapturedAtUtc == newestTimestamp).ToArray();
        if (newest.Select(snapshot => snapshot.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            return null;
        return newest.OrderBy(snapshot => snapshot.Path, StringComparer.OrdinalIgnoreCase).First().Path;
    }

    private sealed record HistoricalSnapshot(string Path, DateTimeOffset CapturedAtUtc, string Sha256);

    internal static void MarkActiveSnapshot(string targetPath, string originalsRoot, string originalPath)
    {
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        var fullRoot = Path.GetFullPath(originalsRoot).TrimEnd(Path.DirectorySeparatorChar);
        var fullOriginal = Path.GetFullPath(originalPath);
        if (!IsWithinRoot(fullOriginal, fullRoot))
            throw new InvalidOperationException("The active original snapshot is outside the originals store.");

        var activeDirectory = Path.Combine(fullRoot, "_active");
        Directory.CreateDirectory(activeDirectory);
        var pointerPath = Path.Combine(activeDirectory, $"{TargetPathKey(fullTarget)}.json");
        var temporaryPath = pointerPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var metadata = new
            {
                TargetPath = fullTarget,
                SnapshotRelativePath = Path.GetRelativePath(fullRoot, fullOriginal),
                Sha256 = Sha256(fullOriginal),
                FileVersion = FileVersionInfo.GetVersionInfo(fullOriginal).FileVersion,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            File.WriteAllText(temporaryPath,
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, pointerPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string? ExistingActiveFor(string fullTarget, string originalsRoot, bool requireAdobeSignature)
    {
        try
        {
            var fullRoot = Path.GetFullPath(originalsRoot).TrimEnd(Path.DirectorySeparatorChar);
            var pointerPath = Path.Combine(fullRoot, "_active", $"{TargetPathKey(fullTarget)}.json");
            if (!File.Exists(pointerPath)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(pointerPath));
            if (!document.RootElement.TryGetProperty("TargetPath", out var storedTarget) ||
                !string.Equals(Path.GetFullPath(storedTarget.GetString() ?? string.Empty), fullTarget,
                    StringComparison.OrdinalIgnoreCase) ||
                !document.RootElement.TryGetProperty("SnapshotRelativePath", out var relativeElement))
                return null;

            var candidate = Path.GetFullPath(Path.Combine(fullRoot, relativeElement.GetString() ?? string.Empty));
            if (!IsWithinRoot(candidate, fullRoot) || !File.Exists(candidate)) return null;
            if (File.Exists(fullTarget) && !HaveSameFileVersion(candidate, fullTarget)) return null;
            ValidateSnapshot(candidate, Path.Combine(Path.GetDirectoryName(candidate)!, "snapshot.json"),
                requireAdobeSignature);
            if (!document.RootElement.TryGetProperty("Sha256", out var expectedHash) ||
                !string.Equals(expectedHash.GetString(), Sha256(candidate), StringComparison.OrdinalIgnoreCase))
                return null;
            return candidate;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsWithinRoot(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string TargetPathKey(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    private static string? ExistingExactFor(string fullTarget, string originalsRoot, bool requireAdobeSignature)
    {
        if (!File.Exists(fullTarget)) return null;
        var directDirectory = Path.Combine(originalsRoot, PathKey(fullTarget));
        var direct = Path.Combine(directDirectory, "dvaui.dll.adobe-original");
        if (!File.Exists(direct)) return null;
        try
        {
            ValidateSnapshot(direct, Path.Combine(directDirectory, "snapshot.json"), requireAdobeSignature);
            return direct;
        }
        catch (InvalidDataException)
        {
            // A stale snapshot captured by an older AfterThemed build must not
            // prevent a repaired, Adobe-signed DLL from getting a clean snapshot.
            return null;
        }
    }

    private static bool HaveSameFileVersion(string leftPath, string rightPath)
    {
        var left = FileVersionInfo.GetVersionInfo(leftPath);
        var right = FileVersionInfo.GetVersionInfo(rightPath);
        return left.FileMajorPart == right.FileMajorPart &&
               left.FileMinorPart == right.FileMinorPart &&
               left.FileBuildPart == right.FileBuildPart &&
               left.FilePrivatePart == right.FilePrivatePart;
    }

    internal static string RequireExistingOriginal(string targetPath, string originalsRoot)
    {
        var original = ExistingFor(targetPath, originalsRoot);
        return original ?? throw new InvalidOperationException(
            "No preserved Adobe original exists for this installation. Restore will not use the currently installed DLL. " +
            "Repair or reinstall this After Effects version through Creative Cloud, then select its fresh dvaui.dll once so AfterThemed can preserve it.");
    }

    internal static string CreateRestoreDll(
        string targetPath,
        string originalsRoot,
        string outputPath,
        Func<string, AdobeSignature>? signatureInspector = null)
    {
        // Creative Cloud can replace dvaui.dll while AfterThemed is already open.
        // Re-evaluate the installed file at restore time so a newly signed hotfix
        // is captured instead of being overwritten by an older same-version snapshot.
        var original = File.Exists(targetPath)
            ? CaptureIfMissing(targetPath, originalsRoot, out _, signatureInspector)
            : RequireExistingOriginal(targetPath, originalsRoot);
        var fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        var temporary = fullOutput + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(original, temporary, false);
            if (!string.Equals(Sha256(original), Sha256(temporary), StringComparison.Ordinal))
                throw new IOException("The restore DLL did not match the preserved Adobe original.");
            File.Move(temporary, fullOutput, true);
            ValidateSnapshot(fullOutput, Path.Combine(Path.GetDirectoryName(original)!, "snapshot.json"),
                signatureInspector: signatureInspector);
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

    private static void ValidateSnapshot(
        string originalPath,
        string metadataPath,
        bool requireAdobeSignature = true,
        Func<string, AdobeSignature>? signatureInspector = null)
    {
        EnsurePortableExecutable(originalPath);
        if (requireAdobeSignature) (signatureInspector ?? EnsureAdobeSigned)(originalPath);
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

    internal sealed record AdobeSignature(string Subject, string Thumbprint);

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
