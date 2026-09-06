using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace DvauiThemeEditor;

internal static class OriginalDllStore
{
    internal static string CaptureIfMissing(
        string targetPath,
        string originalsRoot,
        out bool captured,
        Func<string, AdobeSignature>? signatureInspector = null)
        => CaptureCore(targetPath, originalsRoot, out captured, signatureInspector, null);

    internal static string ImportVerifiedOriginal(string targetPath, string originalsRoot, string backupPath,
        Func<string, AdobeSignature>? signatureInspector = null)
        => CaptureCore(targetPath, originalsRoot, out _, signatureInspector, Path.GetFullPath(backupPath));

    private static string CaptureCore(string targetPath, string originalsRoot, out bool captured,
        Func<string, AdobeSignature>? signatureInspector, string? importSource)
    {
        using var libraryLock = OriginalLibraryLayout.Lock(originalsRoot);
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        using var captureMutex = new Mutex(false, "AfterThemed.Capture." + TargetPathKey(fullTarget));
        var acquired = false;
        string? staging = null;
        try
        {
            try { acquired = captureMutex.WaitOne(TimeSpan.FromSeconds(30)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Another original capture is still running. Try again after it finishes.");
            if (!File.Exists(fullTarget)) throw new FileNotFoundException("The selected installed DLL was not found.", fullTarget);
            using var targetLock = new FileStream(fullTarget, FileMode.Open, FileAccess.Read, FileShare.Read);
            EnsurePortableExecutable(fullTarget);
            var existing = importSource is null ? ExistingExactFor(fullTarget, originalsRoot, requireAdobeSignature: false) : null;
            if (existing is not null)
            {
                MarkActiveSnapshot(fullTarget, originalsRoot, existing);
                captured = false;
                return existing;
            }

            staging = Path.Combine(originalsRoot, "_pending", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var stagedOriginal = Path.Combine(staging, "dvaui.dll.adobe-original");
            using var sourceLock = importSource is null ? null : new FileStream(importSource, FileMode.Open, FileAccess.Read, FileShare.Read);
            File.Copy(importSource ?? fullTarget, stagedOriginal, false);
            AdobeSignature signature;
            try
            {
                // Validate the exact bytes that will be preserved, while the target is locked.
                signature = (signatureInspector ?? EnsureAdobeSigned)(stagedOriginal);
                if (importSource is not null) EnsureMatchingBuild(stagedOriginal, fullTarget);
            }
            catch (InvalidDataException) when (importSource is null)
            {
                existing = ExistingFor(fullTarget, originalsRoot, requireAdobeSignature: false);
                if (existing is null)
                    throw new InvalidDataException(
                        "No verified Adobe original is available for this installation. Its installed DLL or older backup is modified or cannot be authenticated. " +
                        "Repair this After Effects version in Creative Cloud, then select it again. Existing backups have been preserved.");
                MarkActiveSnapshot(fullTarget, originalsRoot, existing);
                captured = false;
                return existing;
            }

            var targetHash = Sha256(importSource ?? fullTarget);
            if (targetHash != Sha256(stagedOriginal))
                throw new IOException("The captured DLL did not match the selected installation.");
            var metadataPath = Path.Combine(staging, "snapshot.json");
            var version = FileVersionInfo.GetVersionInfo(stagedOriginal);
            var metadata = new
            {
                SchemaVersion = 2,
                TargetPath = fullTarget,
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Sha256 = targetHash,
                AuthenticodeSubject = signature.Subject,
                AuthenticodeThumbprint = signature.Thumbprint,
                ValidationPolicy = signatureInspector is null ? "WindowsAuthenticode" : "CompanionValidation",
                RecoveredFrom = importSource,
                HostSha256 = HostHash(fullTarget),
                version.ProductName,
                version.ProductVersion,
                version.FileVersion
            };
            using (var stream = new FileStream(metadataPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, metadata, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            SnapshotProtection.Seal(stagedOriginal, metadataPath);
            var destination = OriginalLibraryLayout.Destination(originalsRoot, fullTarget, version.FileVersion, PathKey(fullTarget, stagedOriginal));
            if (Directory.Exists(destination))
            {
                // Preserve incomplete or untrusted captures rather than overwriting their evidence.
                var quarantine = Path.Combine(originalsRoot, "_quarantine", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
                Directory.Move(destination, quarantine);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging, destination);
            var original = Path.Combine(destination, "dvaui.dll.adobe-original");
            MarkActiveSnapshot(fullTarget, originalsRoot, original);
            captured = true;
            return original;
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
            {
                // Only this invocation's generated staging directory; never a published snapshot.
                try
                {
                    foreach (var file in Directory.EnumerateFiles(staging))
                        File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(staging, recursive: true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (acquired) captureMutex.ReleaseMutex();
        }
    }

    internal static string? ExistingFor(string targetPath, string originalsRoot, bool requireAdobeSignature = true)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return null;
        using var libraryLock = OriginalLibraryLayout.Lock(originalsRoot);
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        var exact = ExistingExactFor(fullTarget, originalsRoot, requireAdobeSignature);
        if (exact is not null) return exact;
        var active = ExistingActiveFor(fullTarget, originalsRoot, requireAdobeSignature);
        if (active is not null) return active;

        if (!Directory.Exists(originalsRoot)) return null;
        var historical = new List<HistoricalSnapshot>();
        foreach (var metadataPath in OriginalLibraryLayout.SnapshotDirectories(originalsRoot)
                     .Select(path => Path.Combine(path, "snapshot.json")).Where(File.Exists))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
                if (!document.RootElement.TryGetProperty("TargetPath", out var storedTarget) ||
                    !string.Equals(Path.GetFullPath(storedTarget.GetString() ?? string.Empty), fullTarget,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var candidate = Path.Combine(Path.GetDirectoryName(metadataPath)!, "dvaui.dll.adobe-original");
                candidate = ValidateSnapshot(candidate, metadataPath, requireAdobeSignature, expectedTarget: fullTarget);
                if (File.Exists(fullTarget) && !HaveSameFileVersion(candidate, fullTarget)) continue;
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
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
        using var libraryLock = OriginalLibraryLayout.Lock(originalsRoot);
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
            if (!IsWithinRoot(candidate, fullRoot)) return null;
            candidate = OriginalLibraryLayout.ResolveMovedPointer(fullRoot, candidate);
            candidate = ValidateSnapshot(candidate, Path.Combine(Path.GetDirectoryName(candidate)!, "snapshot.json"),
                requireAdobeSignature, expectedTarget: fullTarget);
            if (File.Exists(fullTarget) && !HaveSameFileVersion(candidate, fullTarget)) return null;
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
        catch (UnauthorizedAccessException) { return null; }
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
        var directDirectory = OriginalLibraryLayout.FindByKey(originalsRoot, PathKey(fullTarget));
        if (directDirectory is null) return null;
        var direct = Path.Combine(directDirectory, "dvaui.dll.adobe-original");
        if (!File.Exists(Path.Combine(directDirectory, "snapshot.json"))) return null;
        try
        {
            direct = ValidateSnapshot(direct, Path.Combine(directDirectory, "snapshot.json"), requireAdobeSignature,
                expectedTarget: fullTarget);
            return direct;
        }
        catch (InvalidDataException)
        {
            // A stale snapshot captured by an older AfterThemed build must not
            // prevent a repaired, Adobe-signed DLL from getting a clean snapshot.
            return null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static bool HaveSameFileVersion(string leftPath, string rightPath)
    {
        var left = FileVersionInfo.GetVersionInfo(leftPath);
        var right = FileVersionInfo.GetVersionInfo(rightPath);
        // A truncated/missing version resource cannot identify an Adobe update. The authenticated
        // installation path and host hash are checked separately before permitting recovery.
        if (right.FileMajorPart == 0 && right.FileMinorPart == 0) return true;
        return left.FileMajorPart == right.FileMajorPart &&
               left.FileMinorPart == right.FileMinorPart &&
               left.FileBuildPart == right.FileBuildPart &&
               left.FilePrivatePart == right.FilePrivatePart;
    }

    internal static string RequireExistingOriginal(string targetPath, string originalsRoot)
    {
        var original = ExistingFor(targetPath, originalsRoot, requireAdobeSignature: false);
        if (original is not null) return original;
        var rejected = DescribeRejectedSnapshots(targetPath, originalsRoot);
        throw new InvalidOperationException(rejected ??
            "No stored snapshot was found for this installation. Restore will not use the currently installed DLL. " +
            "Repair or reinstall this After Effects version through Creative Cloud, then select its fresh dvaui.dll once so AfterThemed can preserve it.");
    }

    private static string? DescribeRejectedSnapshots(string targetPath, string originalsRoot)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(originalsRoot)) return null;
        var fullTarget = Path.GetFullPath(targetPath.Trim());
        foreach (var directory in OriginalLibraryLayout.SnapshotDirectories(originalsRoot))
        {
            var metadata = Path.Combine(directory, "snapshot.json");
            try
            {
                // Raw metadata is used only to locate a diagnostic candidate. It never authorizes restore.
                using var document = JsonDocument.Parse(File.ReadAllBytes(metadata));
                if (!document.RootElement.TryGetProperty("TargetPath", out var storedTarget) ||
                    !string.Equals(Path.GetFullPath(storedTarget.GetString() ?? string.Empty), fullTarget,
                        StringComparison.OrdinalIgnoreCase)) continue;
                var candidate = Path.Combine(directory, "dvaui.dll.adobe-original");
                try
                {
                    ValidateSnapshot(candidate, metadata, requireAdobeSignature: false, expectedTarget: fullTarget);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or CryptographicException)
                {
                    var theme = File.Exists(candidate) ? ThemePatcher.DetectKnownTheme(candidate) : null;
                    var themeText = theme is null ? string.Empty : $" It matches the built-in '{theme}' theme, so it is a modified DLL—not an Adobe original.";
                    return $"A stored snapshot exists, but it failed verification and cannot safely repair After Effects.{themeText} " +
                           $"File: {candidate}. Validation: {ex.Message} Repair or reinstall this After Effects version through Creative Cloud, then select its fresh dvaui.dll once so AfterThemed can protect it.";
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Keep looking: an unrelated malformed record must not hide a useful diagnostic.
            }
        }
        return null;
    }

    internal static string CreateRestoreDll(
        string targetPath,
        string originalsRoot,
        string outputPath,
        Func<string, AdobeSignature>? signatureInspector = null)
    {
        using var libraryLock = OriginalLibraryLayout.Lock(originalsRoot);
        // Creative Cloud can replace dvaui.dll while AfterThemed is already open.
        // Re-evaluate the installed file at restore time so a newly signed hotfix
        // is captured instead of being overwritten by an older same-version snapshot.
        string original;
        try
        {
            original = File.Exists(targetPath)
                ? CaptureIfMissing(targetPath, originalsRoot, out _, signatureInspector)
                : RequireExistingOriginal(targetPath, originalsRoot);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            // Recovery must not require the installed DLL to be a readable PE.
            original = RequireExistingOriginal(targetPath, originalsRoot);
        }
        var fullOutput = Path.GetFullPath(outputPath);
        if (IsWithinRoot(fullOutput, Path.GetFullPath(originalsRoot).TrimEnd(Path.DirectorySeparatorChar)) ||
            string.Equals(fullOutput, Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Restore output must be separate from the originals store and installed target.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        var temporary = fullOutput + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var metadataPath = Path.Combine(Path.GetDirectoryName(original)!, "snapshot.json");
            using var authenticatedMetadata = JsonDocument.Parse(SnapshotProtection.Verify(metadataPath));
            var expectedHash = authenticatedMetadata.RootElement.GetProperty("Sha256").GetString();
            using var originalLock = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read);
            File.Copy(original, temporary, false);
            File.SetAttributes(temporary, FileAttributes.Normal);
            if (!string.Equals(expectedHash, Sha256(temporary), StringComparison.OrdinalIgnoreCase))
                throw new IOException("The restore DLL did not match the preserved Adobe original.");
            // Verification must finish before publishing output. A protected source stays read-only.
            File.Move(temporary, fullOutput, true);
            return fullOutput;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string PathKey(string path, string? contentPath = null)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var version = FileVersionInfo.GetVersionInfo(contentPath ?? path).FileVersion ?? "unknown-version";
        var contentHash = Sha256(contentPath ?? path);
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(normalized + "|" + version + "|" + contentHash)))[..16];
    }

    // Signed backup import must match the live installation's DLL role and build. The installed
    // bytes may be themed, but a different build must never be selected just because its year matches.
    internal static void EnsureMatchingBuild(string candidate, string target)
    {
        var sourceVersion = FileVersionInfo.GetVersionInfo(candidate);
        var targetVersion = FileVersionInfo.GetVersionInfo(target);
        if (targetVersion.FileMajorPart == 0 || !HaveSameFileVersion(candidate, target) ||
            string.IsNullOrWhiteSpace(targetVersion.OriginalFilename) ||
            !string.Equals(sourceVersion.OriginalFilename, targetVersion.OriginalFilename, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceVersion.ProductName, targetVersion.ProductName, StringComparison.Ordinal))
            throw new InvalidDataException("This backup does not match the selected DLL's identity and exact file build. Select a backup from this same After Effects installation.");
        var sourceBytes = File.ReadAllBytes(candidate);
        var targetBytes = File.ReadAllBytes(target);
        var sourcePe = new DvauiPeImage(sourceBytes);
        var targetPe = new DvauiPeImage(targetBytes);
        var sourceHeader = BitConverter.ToInt32(sourceBytes, 0x3C);
        var targetHeader = BitConverter.ToInt32(targetBytes, 0x3C);
        if (sourcePe.Is64Bit != targetPe.Is64Bit || sourcePe.ImageBase != targetPe.ImageBase ||
            BitConverter.ToUInt16(sourceBytes, sourceHeader + 4) != BitConverter.ToUInt16(targetBytes, targetHeader + 4) ||
            BitConverter.ToUInt32(sourceBytes, sourceHeader + 8) != BitConverter.ToUInt32(targetBytes, targetHeader + 8) ||
            !sourcePe.Sections.SequenceEqual(targetPe.Sections))
            throw new InvalidDataException("This backup has a different executable build layout. It cannot repair the selected installation.");
    }

    internal static bool IsVerifiedMatchingBackup(string candidate, string target)
    {
        try
        {
            EnsureMatchingBuild(candidate, target);
            EnsureAdobeSigned(candidate);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    internal static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ValidateSnapshot(
        string originalPath,
        string metadataPath,
        bool requireAdobeSignature = true,
        Func<string, AdobeSignature>? signatureInspector = null,
        string? expectedTarget = null)
    {
        if (!File.Exists(metadataPath))
            throw new InvalidDataException("The preserved original is missing its verification metadata.");

        var protectedSnapshot = SnapshotProtection.HasProof(metadataPath);
        using var document = JsonDocument.Parse(protectedSnapshot
            ? SnapshotProtection.Verify(metadataPath) : File.ReadAllBytes(metadataPath));
        if (expectedTarget is not null &&
            (!document.RootElement.TryGetProperty("TargetPath", out var storedTarget) ||
             !string.Equals(storedTarget.GetString(), expectedTarget, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("This preserved original belongs to a different installation.");
        if (expectedTarget is not null &&
            document.RootElement.TryGetProperty("HostSha256", out var hostHash) &&
            hostHash.ValueKind == JsonValueKind.String &&
            !string.Equals(hostHash.GetString(), HostHash(expectedTarget), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("After Effects changed since this original was captured. Repair or select the updated installation before restoring.");
        if (!document.RootElement.TryGetProperty("Sha256", out var expectedElement))
            throw new InvalidDataException("The preserved original has no recorded SHA-256 hash.");
        var expected = expectedElement.GetString();
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidDataException("The preserved original has no usable SHA-256 hash.");
        if (protectedSnapshot)
        {
            originalPath = SnapshotProtection.RecoverIfNeeded(originalPath, metadataPath, expected);
        }
        else
        {
            // Legacy hashes prove consistency, not Adobe provenance. Never grandfather an
            // unsigned/themed legacy backup into the new protected vault.
            (signatureInspector ?? EnsureAdobeSigned)(originalPath);
        }
        EnsurePortableExecutable(originalPath);
        var actual = Sha256(originalPath);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The preserved original failed SHA-256 verification and will not be restored.");
        if (!SnapshotProtection.HasProof(metadataPath))
            SnapshotProtection.Seal(originalPath, metadataPath);
        return originalPath;
    }

    private static string? HostHash(string target)
    {
        var host = Path.Combine(Path.GetDirectoryName(target)!, "AfterFX.exe");
        return File.Exists(host) ? Sha256(host) : null;
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

    /// <summary>
    /// Adobe ships AfterFXLib.dll with an embedded Adobe signature whose Authenticode hash does not
    /// validate, in every release checked from CC 2019 to 2025, so the companion original is accepted
    /// on its embedded Adobe signer alone. The file is still required to be a readable PE signed by
    /// Adobe, and the snapshot still records the SHA-256 that pins byte-exact restore and rollback.
    /// This relaxation is only ever passed for the AfterFXLib.dll companion; dvaui.dll continues to
    /// require full Authenticode validation through <see cref="EnsureAdobeSigned"/>.
    /// </summary>
    internal static AdobeSignature EnsureAdobeCompanionSigner(string path)
    {
        var fullPath = Path.GetFullPath(path);
        EnsurePortableExecutable(fullPath);
        try
        {
#pragma warning disable SYSLIB0057 // Required to read the signer embedded in a signed PE, not a standalone certificate file.
            using var certificate = X509Certificate.CreateFromSignedFile(fullPath);
#pragma warning restore SYSLIB0057
            var subject = certificate.Subject ?? string.Empty;
            if (!subject.Contains("Adobe", StringComparison.OrdinalIgnoreCase))
                throw InvalidAdobeOriginal($"The companion signer is '{subject}', not Adobe.");
            return new AdobeSignature(subject, certificate.GetCertHashString());
        }
        catch (CryptographicException exception)
        {
            throw InvalidAdobeOriginal("The companion has no readable embedded Adobe signing certificate.", exception);
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
