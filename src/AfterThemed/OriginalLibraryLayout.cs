using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DvauiThemeEditor;

/// <summary>Readable labels are navigation aids, never evidence that an original is trusted.</summary>
internal static class OriginalLibraryLayout
{
    internal sealed record OrganizationResult(int Moved, IReadOnlyList<string> Warnings);

    internal static IDisposable Lock(string root) => new LibraryLock(root);

    internal static string Destination(string root, string target, string? version, string key)
    {
        var install = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(target))!);
        while (install is not null && !install.Name.Contains("After Effects", StringComparison.OrdinalIgnoreCase))
            install = install.Parent;
        // DVA's internal version is not the AE release year (AE 2020 ships DVA 14.2).
        var label = install?.Name ?? "After Effects - Unidentified release";
        if (label.StartsWith("Adobe ", StringComparison.OrdinalIgnoreCase)) label = label[6..];
        return Path.Combine(Path.GetFullPath(root), SafeLabel(label, 65),
            $"{SafeLabel(Path.GetFileName(target), 32)} - {SafeLabel(version ?? "Unknown build", 32)} [{key}]");
    }

    private static string SafeLabel(string value, int maximum)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var text = new string(value.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
        text = text.Trim().TrimEnd('.');
        return string.IsNullOrEmpty(text) ? "Unknown" : text[..Math.Min(text.Length, maximum)].TrimEnd('.', ' ');
    }

    // Deliberately bounded: legacy snapshots at the root, or snapshots one level below release groups.
    // Never traverse housekeeping, junctions, or arbitrary recursive trees.
    internal static IEnumerable<string> SnapshotDirectories(string root)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var directory in VisibleDirectories(root))
        {
            if (File.Exists(Path.Combine(directory, "snapshot.json"))) yield return directory;
            else
                foreach (var child in VisibleDirectories(directory))
                    if (File.Exists(Path.Combine(child, "snapshot.json"))) yield return child;
        }
    }

    private static IEnumerable<string> VisibleDirectories(string root) =>
        Directory.EnumerateDirectories(root).Where(path =>
            !Path.GetFileName(path).StartsWith('_') &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);

    internal static string? FindByKey(string root, string key)
    {
        if (!Regex.IsMatch(key, "\\A[0-9A-Fa-f]{16}\\z")) return null;
        var matches = SnapshotDirectories(root).Where(path =>
            Path.GetFileName(path).Equals(key, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(path).EndsWith($"[{key}]", StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        // Ambiguous copies must not silently choose an unrelated capture.
        return matches.Length == 1 ? matches[0] : null;
    }

    internal static string ResolveMovedPointer(string root, string candidate)
    {
        if (Directory.Exists(Path.GetDirectoryName(candidate))) return candidate;
        var directory = Path.GetDirectoryName(candidate)!;
        if (!string.Equals(Path.GetDirectoryName(directory), Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)) return candidate;
        var moved = FindByKey(root, Path.GetFileName(directory));
        return moved is null ? candidate : Path.Combine(moved, Path.GetFileName(candidate));
    }

    internal static OrganizationResult Organize(string originalsRoot)
    {
        var root = Path.GetFullPath(originalsRoot).TrimEnd(Path.DirectorySeparatorChar);
        using var libraryLock = Lock(root);
        var warnings = new List<string>();
        var moved = 0;
        if (!Directory.Exists(root)) return new(0, warnings);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The originals library is a linked directory; automatic organization was skipped.");
        foreach (var directory in VisibleDirectories(root).ToArray())
        {
            var key = Path.GetFileName(directory);
            if (!Regex.IsMatch(key, "\\A[0-9A-Fa-f]{16}\\z")) continue;
            try
            {
                // Preserve malformed and unusual records in place; never edit capture metadata or DLL bytes.
                if (Directory.EnumerateDirectories(directory).Any() ||
                    Directory.EnumerateFiles(directory).Any(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
                    throw new IOException("Unexpected nested or linked snapshot contents.");
                var metadataPath = Path.Combine(directory, "snapshot.json");
                using var document = JsonDocument.Parse(SnapshotProtection.HasProof(metadataPath)
                    ? SnapshotProtection.Verify(metadataPath) : File.ReadAllBytes(metadataPath));
                var target = document.RootElement.GetProperty("TargetPath").GetString();
                if (string.IsNullOrWhiteSpace(target) || !Path.IsPathFullyQualified(target))
                    throw new InvalidDataException("Snapshot has no absolute installation path.");
                var version = document.RootElement.TryGetProperty("FileVersion", out var element)
                    && element.ValueKind == JsonValueKind.String ? element.GetString() : null;
                var destination = Destination(root, target, version, key);
                var group = Path.GetDirectoryName(destination)!;
                if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Organization destination is outside the originals library.");
                Directory.CreateDirectory(group);
                if ((File.GetAttributes(group) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("The release folder is a linked directory.");
                // No overwrite or merge: conflicting records are retained for manual inspection.
                Directory.Move(directory, destination);
                moved++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
                                       ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                warnings.Add($"{key}: left in place. {ex.Message}");
            }
        }
        // Can be retried after interruption. The reader also resolves old pointer paths by their
        // preserved key, so a crash between the directory move and this rewrite cannot lose provenance.
        RefreshPointers(root, warnings);
        var guide = Path.Combine(root, "README.txt");
        try
        {
            if (!File.Exists(guide))
            {
                using var stream = new FileStream(guide, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                var bytes = Encoding.UTF8.GetBytes("""
                    AFTERTHEMED ORIGINALS LIBRARY

                    Open the folder for your After Effects release (for example, After Effects 2020).
                    Inside, each snapshot folder identifies the DLL and its exact internal file build.
                    The short ID keeps different installations and captures separate. Do not merge them.
                    A DLL's internal build number is not necessarily the After Effects release year.

                    Restore using AfterThemed's RESTORE button, not by manually copying these files.
                    Legacy stored files may be called dvaui.dll.adobe-original even for AfterFXLib.dll;
                    the containing folder and snapshot.json identify the actual installed target.
                    A readable folder label is not a guarantee of authenticity or restorability.
                    Existing signature, hash, installation, and protected-record checks still apply.

                    snapshot.json: capture details and installation path. Do not edit.
                    snapshot.proof: Windows-profile-bound protection record, when present.
                    original.recovery: second verified copy, when present.
                    _active, _pending, _quarantine: internal bookkeeping; leave these alone.

                    Keep the entire library together when backing up. A second copy on the same disk
                    cannot protect against disk loss. Protected records require the original Windows
                    user profile. If no valid matching original survives, obtain a clean copy from Adobe.

                    Organized libraries require AfterThemed 1.3.13 or later.
                    """);
                stream.Write(bytes);
                stream.Flush(true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add("Library instructions could not be created: " + ex.Message);
        }
        return new(moved, warnings);
    }

    private static void RefreshPointers(string root, List<string> warnings)
    {
        var active = Path.Combine(root, "_active");
        if (!Directory.Exists(active)) return;
        if ((File.GetAttributes(active) & FileAttributes.ReparsePoint) != 0) return;
        foreach (var pointer in Directory.EnumerateFiles(active, "*.json"))
        {
            string? temporary = null;
            try
            {
                if ((File.GetAttributes(pointer) & FileAttributes.ReparsePoint) != 0) continue;
                var record = JsonNode.Parse(File.ReadAllText(pointer))?.AsObject();
                var relative = record?["SnapshotRelativePath"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                var candidate = Path.GetFullPath(Path.Combine(root, relative));
                if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                var resolved = ResolveMovedPointer(root, candidate);
                if (resolved == candidate) continue;
                record!["SnapshotRelativePath"] = Path.GetRelativePath(root, resolved);
                temporary = pointer + $".{Guid.NewGuid():N}.tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, record, new JsonSerializerOptions { WriteIndented = true });
                    stream.Flush(true);
                }
                File.Move(temporary, pointer, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
                                       ArgumentException or InvalidOperationException)
            {
                warnings.Add($"Active reference retained; migration can be retried: {ex.Message}");
            }
            finally
            {
                if (temporary is not null && File.Exists(temporary))
                    try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal static bool OtherAppIsOpen() =>
        new[] { "AfterThemed", "DVAUI Theme Editor" }.Any(name =>
            Process.GetProcessesByName(name).Any(process =>
            {
                using (process) return process.Id != Environment.ProcessId;
            }));

    private sealed class LibraryLock : IDisposable
    {
        private readonly Mutex mutex;
        internal LibraryLock(string root)
        {
            var identity = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
            mutex = new Mutex(false, "AfterThemed.Library." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))));
            try
            {
                try { if (mutex.WaitOne(TimeSpan.FromSeconds(30))) return; }
                catch (AbandonedMutexException) { return; }
                throw new IOException("Another AfterThemed operation is using the originals library. Try again shortly.");
            }
            catch { mutex.Dispose(); throw; }
        }
        public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
    }
}
