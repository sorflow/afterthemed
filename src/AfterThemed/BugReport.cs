using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace DvauiThemeEditor;

internal sealed record BugReportContext(
    string? TargetDllPath,
    string? PreservedOriginalPath,
    string DataRoot,
    string ReportsDirectory,
    string ThemeName,
    string PresetName,
    string LogText);

internal sealed record BugReportBundle(string Summary, string BundlePath);

/// <summary>
/// Collects what a maintainer needs to reproduce a failure without shipping Adobe's binary.
///
/// dvaui.dll and AfterFXLib.dll are Adobe's proprietary files, so AfterThemed never uploads them:
/// this project deliberately keeps compiled binaries out of the repository, and attaching one to a
/// public issue would republish Adobe code. Everything here describes those files instead — version,
/// size, SHA-256, and the PE theme-resource layout — which is what actually identifies a build. When
/// a maintainer needs the file itself, the user can send it privately.
/// </summary>
internal static class BugReportBuilder
{
    private const string IssueRepository = "sorflow/afterthemed";

    /// <summary>Keeps the prefilled issue inside the length a browser will carry in a URL.</summary>
    private const int MaximumUrlBodyLength = 6000;

    internal static BugReportBundle Create(BugReportContext context)
    {
        var report = BuildReport(context);
        var bundlePath = WriteBundle(context, report);
        return new BugReportBundle(report, bundlePath);
    }

    internal static string IssueUrl(string body)
    {
        var trimmed = body.Length <= MaximumUrlBodyLength
            ? body
            : body[..MaximumUrlBodyLength] + "\n\n_(truncated — the full diagnostics bundle is attached)_";
        return $"https://github.com/{IssueRepository}/issues/new" +
               $"?labels={Uri.EscapeDataString("bug")}" +
               $"&title={Uri.EscapeDataString("[Bug] ")}" +
               $"&body={Uri.EscapeDataString(trimmed)}";
    }

    private static string BuildReport(BugReportContext context)
    {
        var text = new StringBuilder();
        text.AppendLine("### What happened");
        text.AppendLine();
        text.AppendLine("<!-- Describe what you did and what went wrong. -->");
        text.AppendLine();
        text.AppendLine("### Environment");
        text.AppendLine();
        text.AppendLine("```");
        text.AppendLine($"AfterThemed   : {ApplicationLifetime.DisplayVersion()}");
        text.AppendLine($"Windows       : {Environment.OSVersion.VersionString}");
        text.AppendLine($".NET          : {Environment.Version}");
        text.AppendLine($"Process       : {(Environment.Is64BitProcess ? "x64" : "x86")}");
        text.AppendLine($"Elevated      : {IsElevated()}");
        text.AppendLine($"Theme         : {context.ThemeName}  (preset: {context.PresetName})");
        text.AppendLine("```");
        text.AppendLine();

        text.AppendLine("### Target");
        text.AppendLine();
        text.AppendLine("```");
        AppendFileFacts(text, "dvaui.dll", context.TargetDllPath);
        AppendCompanionFacts(text, context.TargetDllPath);
        AppendFileFacts(text, "preserved original", context.PreservedOriginalPath);
        text.AppendLine("```");
        text.AppendLine();

        AppendDetectedInstalls(text);
        AppendLatestInstallReport(text, context.ReportsDirectory);
        AppendLog(text, context.LogText);

        text.AppendLine();
        text.AppendLine("<!-- The diagnostics bundle named above is saved on your PC. Attach it by " +
                        "dragging it into this issue if you are willing to share it. It contains no " +
                        "Adobe binaries. -->");
        return text.ToString();
    }

    private static void AppendFileFacts(StringBuilder text, string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            text.AppendLine($"{label,-18}: (none selected)");
            return;
        }

        var trimmed = path.Trim();
        if (!File.Exists(trimmed))
        {
            text.AppendLine($"{label,-18}: MISSING · {trimmed}");
            return;
        }

        text.AppendLine($"{label,-18}: {trimmed}");
        try
        {
            var info = new FileInfo(trimmed);
            var version = FileVersionInfo.GetVersionInfo(trimmed);
            text.AppendLine($"{"  version",-18}: {version.FileVersion ?? "unknown"}");
            text.AppendLine($"{"  size",-18}: {info.Length:N0} bytes");
            text.AppendLine($"{"  modified",-18}: {info.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}Z");
            text.AppendLine($"{"  sha256",-18}: {OriginalDllStore.Sha256(trimmed)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            text.AppendLine($"{"  error",-18}: {exception.Message}");
        }
    }

    private static void AppendCompanionFacts(StringBuilder text, string? targetDllPath)
    {
        if (string.IsNullOrWhiteSpace(targetDllPath)) return;
        string companion;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(targetDllPath.Trim()));
            if (directory is null) return;
            companion = Path.Combine(directory, "AfterFXLib.dll");
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            return;
        }

        if (!File.Exists(companion))
        {
            text.AppendLine($"{"AfterFXLib.dll",-18}: not present beside dvaui.dll");
            return;
        }

        AppendFileFacts(text, "AfterFXLib.dll", companion);
        // Which theme resources the companion carries is the fact that decides whether AfterThemed
        // patches it at all, so it is worth stating outright rather than leaving to a hash.
        text.AppendLine($"{"  theme resources",-18}: " +
                        (LegacyAeThemePatcher.HasNativeThemeResources(companion) ? "complete" : "absent or partial"));
        text.AppendLine($"{"  already themed",-18}: {LegacyAeThemePatcher.IsAlreadyThemed(companion)}");
    }

    private static void AppendDetectedInstalls(StringBuilder text)
    {
        text.AppendLine("### Detected installations");
        text.AppendLine();
        text.AppendLine("```");
        try
        {
            var installs = AfterEffectsCatalog.Discover();
            if (installs.Count == 0) text.AppendLine("(none detected)");
            foreach (var install in installs)
                text.AppendLine($"{install.DisplayName} · dvaui {install.Version} · " +
                                $"companion {(install.HasNativeCompanion ? "yes" : "no")} · " +
                                $"via {install.DiscoverySource}");
        }
        catch (Exception exception)
        {
            text.AppendLine($"discovery failed: {exception.Message}");
        }
        text.AppendLine("```");
        text.AppendLine();
    }

    private static void AppendLatestInstallReport(StringBuilder text, string reportsDirectory)
    {
        var latest = LatestReportFile(reportsDirectory);
        if (latest is null) return;

        text.AppendLine("### Last install report");
        text.AppendLine();
        text.AppendLine("```json");
        try
        {
            text.AppendLine(Truncate(File.ReadAllText(latest), 2000));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            text.AppendLine($"could not read {latest}: {exception.Message}");
        }
        text.AppendLine("```");
        text.AppendLine();
    }

    private static void AppendLog(StringBuilder text, string logText)
    {
        if (string.IsNullOrWhiteSpace(logText)) return;
        text.AppendLine("### Activity log");
        text.AppendLine();
        text.AppendLine("```");
        text.AppendLine(TailLines(logText, 40));
        text.AppendLine("```");
    }

    private static string? LatestReportFile(string reportsDirectory)
    {
        try
        {
            if (!Directory.Exists(reportsDirectory)) return null;
            return new DirectoryInfo(reportsDirectory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the shareable bundle. Only text goes in: the report itself, recent install reports,
    /// and the activity log.
    /// </summary>
    private static string WriteBundle(BugReportContext context, string report)
    {
        var diagnostics = Path.Combine(context.DataRoot, "Diagnostics");
        Directory.CreateDirectory(diagnostics);
        var bundlePath = Path.Combine(diagnostics,
            $"afterthemed-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        using (var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "report.md", report);
            if (!string.IsNullOrWhiteSpace(context.LogText))
                WriteEntry(archive, "activity-log.txt", context.LogText);

            try
            {
                if (Directory.Exists(context.ReportsDirectory))
                {
                    foreach (var file in new DirectoryInfo(context.ReportsDirectory)
                                 .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                                 .OrderByDescending(file => file.LastWriteTimeUtc)
                                 .Take(5))
                        archive.CreateEntryFromFile(file.FullName, $"install-reports/{file.Name}");
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                WriteEntry(archive, "install-reports/READ-ERROR.txt", exception.Message);
            }
        }

        return bundlePath;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum] + "\n… truncated …";

    private static string TailLines(string value, int lines)
    {
        var all = value.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("\r\n", all.Length <= lines ? all : all[^lines..]);
    }
}
