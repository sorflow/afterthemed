using System.Diagnostics;
using Microsoft.Win32;

namespace DvauiThemeEditor;

/// <summary>
/// One discovered After Effects installation. <see cref="Version"/> is read from dvaui.dll and is
/// deliberately not treated as the release identity: CC 2019 and 2023 stamp the application version
/// onto dvaui.dll while 2020 and 2021 stamp the DVA version, so the installation folder is the only
/// name that reads correctly across releases.
/// </summary>
internal sealed record AfterEffectsInstall(
    string DllPath,
    string InstallRoot,
    string DisplayName,
    Version Version,
    string? CompanionPath,
    string DiscoverySource)
{
    internal bool HasNativeCompanion => CompanionPath is not null;

    /// <summary>The release year parsed from the folder name, used for ordering when present.</summary>
    internal int ReleaseYear { get; init; }
}

/// <summary>
/// Finds every After Effects installation on the machine. Adobe does not record one authoritative
/// location, and After Effects is commonly moved off the system drive, so several independent
/// sources are merged and de-duplicated by the resolved dvaui.dll path.
/// </summary>
internal static class AfterEffectsCatalog
{
    private const string DvauiFileName = "dvaui.dll";
    private const string CompanionFileName = "AfterFXLib.dll";
    private const string SupportFiles = "Support Files";
    private const string InstallPattern = "Adobe After Effects*";

    internal static IReadOnlyList<AfterEffectsInstall> Discover()
    {
        var found = new Dictionary<string, AfterEffectsInstall>(StringComparer.OrdinalIgnoreCase);
        foreach (var (root, source) in CandidateInstallRoots())
            TryAdd(found, root, source);

        return InReleaseOrder(found.Values);
    }

    /// <summary>
    /// Newest release first. The dvaui.dll file version cannot drive this: After Effects CC 2019
    /// ships dvaui 16.1 while After Effects 2021 ships dvaui 15.4, so ordering by file version puts
    /// a 2019 release above a 2021 one. The release year in the installation folder is authoritative,
    /// and the file version only separates installations that have no year to compare.
    /// </summary>
    internal static IReadOnlyList<AfterEffectsInstall> InReleaseOrder(IEnumerable<AfterEffectsInstall> installs) =>
        installs
            .OrderByDescending(install => install.ReleaseYear)
            .ThenByDescending(install => install.Version)
            .ThenBy(install => install.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Resolves a single installation from a dvaui.dll the user chose by hand, so a browsed target
    /// carries the same details as a discovered one.
    /// </summary>
    internal static AfterEffectsInstall? Describe(string dllPath, string discoverySource = "Selected")
    {
        try
        {
            var fullPath = Path.GetFullPath(dllPath.Trim());
            if (!File.Exists(fullPath)) return null;
            var supportFiles = Path.GetDirectoryName(fullPath);
            var installRoot = supportFiles is null ? null : Path.GetDirectoryName(supportFiles);
            return Build(fullPath, installRoot ?? supportFiles ?? fullPath, discoverySource);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static void TryAdd(Dictionary<string, AfterEffectsInstall> found, string installRoot, string source)
    {
        try
        {
            var dll = Path.Combine(installRoot, SupportFiles, DvauiFileName);
            if (!File.Exists(dll)) return;
            var fullPath = Path.GetFullPath(dll);
            // The first source to resolve a path wins, so the ordered scan below keeps the most
            // descriptive origin rather than whichever source happened to run last.
            if (found.ContainsKey(fullPath)) return;
            found[fullPath] = Build(fullPath, Path.GetFullPath(installRoot), source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A locked or half-updated Creative Cloud install must not hide the readable ones.
        }
    }

    private static AfterEffectsInstall Build(string dllPath, string installRoot, string source)
    {
        var companion = Path.Combine(Path.GetDirectoryName(dllPath)!, CompanionFileName);
        var displayName = DisplayNameFor(installRoot);
        return new AfterEffectsInstall(
            dllPath,
            installRoot,
            displayName,
            ReadVersion(dllPath),
            File.Exists(companion) ? companion : null,
            source)
        {
            ReleaseYear = ReleaseYearFor(displayName)
        };
    }

    private static IEnumerable<(string Root, string Source)> CandidateInstallRoots()
    {
        foreach (var root in AdobeParents())
            foreach (var install in InstallsUnder(root.Directory))
                yield return (install, root.Source);

        foreach (var install in RegisteredInstallRoots())
            yield return install;
    }

    /// <summary>
    /// Adobe parent directories worth enumerating. The standard Program Files locations come first,
    /// then the same layout on every other fixed drive, because After Effects is routinely installed
    /// to a second drive for scratch and cache reasons.
    /// </summary>
    private static IEnumerable<(string Directory, string Source)> AdobeParents()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrWhiteSpace(programFiles)) continue;
            var adobe = Path.Combine(programFiles, "Adobe");
            if (seen.Add(adobe)) yield return (adobe, "Program Files");
        }

        foreach (var drive in FixedDrives())
        {
            foreach (var relative in new[] { @"Program Files\Adobe", @"Program Files (x86)\Adobe", "Adobe" })
            {
                var adobe = Path.Combine(drive, relative);
                if (seen.Add(adobe)) yield return (adobe, $"Drive {drive.TrimEnd('\\')}");
            }
        }
    }

    private static IEnumerable<string> FixedDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var drive in drives)
        {
            string root;
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                root = drive.RootDirectory.FullName;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            yield return root;
        }
    }

    private static IEnumerable<string> InstallsUnder(string adobeDirectory)
    {
        string[] directories;
        try
        {
            if (!Directory.Exists(adobeDirectory)) return [];
            directories = Directory.GetDirectories(adobeDirectory, InstallPattern, SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        return directories;
    }

    /// <summary>
    /// Installations recorded by the uninstall registry and Adobe's own keys. This is the only source
    /// that finds an install placed outside every conventional Adobe directory.
    /// </summary>
    private static IEnumerable<(string Root, string Source)> RegisteredInstallRoots()
    {
        foreach (var path in UninstallLocations()) yield return (path, "Registry");
        foreach (var path in AdobeKeyLocations()) yield return (path, "Registry");
    }

    private static IEnumerable<string> UninstallLocations()
    {
        var results = new List<string>();
        foreach (var (hive, view) in RegistryScopes())
        {
            using var baseKey = OpenBaseKey(hive, view);
            using var uninstall = TryOpen(baseKey, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null) continue;

            foreach (var subKeyName in SafeSubKeyNames(uninstall))
            {
                using var entry = TryOpen(uninstall, subKeyName);
                if (entry is null) continue;
                var displayName = entry.GetValue("DisplayName") as string;
                if (displayName is null ||
                    displayName.IndexOf("After Effects", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (entry.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
                    results.Add(location.Trim().Trim('"'));
            }
        }
        return results;
    }

    private static IEnumerable<string> AdobeKeyLocations()
    {
        var results = new List<string>();
        foreach (var (hive, view) in RegistryScopes())
        {
            using var baseKey = OpenBaseKey(hive, view);
            using var afterEffects = TryOpen(baseKey, @"SOFTWARE\Adobe\After Effects");
            if (afterEffects is null) continue;

            foreach (var versionName in SafeSubKeyNames(afterEffects))
            {
                using var versionKey = TryOpen(afterEffects, versionName);
                if (versionKey?.GetValue("InstallPath") is string install && !string.IsNullOrWhiteSpace(install))
                    results.Add(install.Trim().Trim('"'));
            }
        }
        return results;
    }

    private static IEnumerable<(RegistryHive Hive, RegistryView View)> RegistryScopes() =>
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),
        (RegistryHive.CurrentUser, RegistryView.Registry64)
    ];

    private static RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view) =>
        RegistryKey.OpenBaseKey(hive, view);

    private static RegistryKey? TryOpen(RegistryKey? parent, string name)
    {
        try
        {
            return parent?.OpenSubKey(name);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string[] SafeSubKeyNames(RegistryKey key)
    {
        try
        {
            return key.GetSubKeyNames();
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    /// <summary>
    /// "C:\Program Files\Adobe\Adobe After Effects 2025" becomes "After Effects 2025". The folder is
    /// the most faithful release name available; the file version cannot supply one.
    /// </summary>
    private static string DisplayNameFor(string installRoot)
    {
        var folder = Path.GetFileName(installRoot.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folder)) return "After Effects";
        const string adobePrefix = "Adobe ";
        return folder.StartsWith(adobePrefix, StringComparison.OrdinalIgnoreCase)
            ? folder[adobePrefix.Length..]
            : folder;
    }

    private static int ReleaseYearFor(string displayName)
    {
        foreach (var token in displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (token.Length == 4 && int.TryParse(token, out var year) && year is >= 2000 and <= 2100)
                return year;
        return 0;
    }

    private static Version ReadVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(Math.Max(0, info.FileMajorPart), Math.Max(0, info.FileMinorPart),
                Math.Max(0, info.FileBuildPart), Math.Max(0, info.FilePrivatePart));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new Version(0, 0);
        }
    }
}
