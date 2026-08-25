using System.Text.Json;

namespace DvauiThemeEditor;

internal sealed record ThemeFileInstall(string InputPath, string TargetPath);

internal sealed record ThemeFileSetManifest(string BackupDirectory, IReadOnlyList<ThemeFileInstall> Files);

internal sealed record ThemeFileInstallResult(
    ThemeFileInstall File,
    NativeInstallReport Install,
    NativeInstallReport? Rollback = null);

internal sealed record ThemeFileSetReport(
    int ExitCode,
    string Stage,
    string Message,
    IReadOnlyList<ThemeFileInstallResult> Files)
{
    internal bool Succeeded => ExitCode == 0;
}

internal static class ThemeFileSetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static void WriteManifest(string path, ThemeFileSetManifest manifest)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(manifest, JsonOptions));
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal static ThemeFileSetManifest ReadManifest(string path) =>
        JsonSerializer.Deserialize<ThemeFileSetManifest>(File.ReadAllText(path)) ??
        throw new InvalidDataException("The theme file-set manifest is empty.");

    internal static void WriteReport(string path, ThemeFileSetReport report)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(report, JsonOptions));
            File.Move(temporaryPath, fullPath, false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal static ThemeFileSetReport? TryReadReport(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ThemeFileSetReport>(File.ReadAllText(path))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

internal static class ThemeFileSetInstaller
{
    internal static int Run(string manifestPath, string reportPath, bool requireAfterEffectsClosed = true)
    {
        if (!NativeInstallReportStore.CanWrite(reportPath)) return 2;

        ThemeFileSetReport report;
        try
        {
            var manifest = ThemeFileSetStore.ReadManifest(manifestPath);
            report = Install(manifest, requireAfterEffectsClosed);
        }
        catch (Exception ex)
        {
            report = new ThemeFileSetReport(2, "manifest validation", ex.Message, []);
        }

        try
        {
            ThemeFileSetStore.WriteReport(reportPath, report);
        }
        catch
        {
            return 2;
        }
        return report.ExitCode;
    }

    internal static ThemeFileSetReport Install(
        ThemeFileSetManifest manifest,
        bool requireAfterEffectsClosed = true,
        Func<ThemeFileInstall, NativeInstallReport>? installer = null)
    {
        var files = Validate(manifest);
        if (requireAfterEffectsClosed && System.Diagnostics.Process.GetProcessesByName("AfterFX").Length > 0)
            return new ThemeFileSetReport(2, "process check", "Close After Effects before installing.", []);

        installer ??= file => NativeDllInstaller.Install(
            file.InputPath, file.TargetPath, manifest.BackupDirectory, requireAfterEffectsClosed: false);

        var results = new List<ThemeFileInstallResult>();
        foreach (var file in files)
        {
            var installed = installer(file);
            results.Add(new ThemeFileInstallResult(file, installed));
            if (installed.Succeeded) continue;

            var rollbackFailed = false;
            for (var index = results.Count - 2; index >= 0; index--)
            {
                var previous = results[index];
                NativeInstallReport rollback;
                if (string.IsNullOrWhiteSpace(previous.Install.BackupPath) ||
                    !File.Exists(previous.Install.BackupPath))
                {
                    rollback = new NativeInstallReport(2, "file-set rollback",
                        "The verified pre-install backup was unavailable.", previous.Install.BackupPath);
                }
                else
                {
                    rollback = NativeDllInstaller.Install(previous.Install.BackupPath,
                        previous.File.TargetPath, manifest.BackupDirectory, requireAfterEffectsClosed: false);
                }
                rollbackFailed |= !rollback.Succeeded;
                results[index] = previous with { Rollback = rollback };
            }

            var rollbackMessage = results.Count <= 1
                ? "No earlier file required rollback."
                : rollbackFailed
                    ? "One or more earlier theme files could not be restored; repair After Effects before launching it."
                    : "Earlier theme files were restored from their verified backups.";
            return new ThemeFileSetReport(2, installed.Stage,
                $"{Path.GetFileName(file.TargetPath)}: {installed.Message} {rollbackMessage}", results);
        }

        return new ThemeFileSetReport(0, "completed",
            $"Installed and verified {results.Count} theme files.", results);
    }

    private static ThemeFileInstall[] Validate(ThemeFileSetManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.BackupDirectory))
            throw new InvalidDataException("The theme file-set backup directory is missing.");
        if (manifest.Files is null || manifest.Files.Count is < 1 or > 4)
            throw new InvalidDataException("A theme file set must contain between one and four files.");

        var files = manifest.Files.Select(file => new ThemeFileInstall(
            Path.GetFullPath(file.InputPath), Path.GetFullPath(file.TargetPath))).ToArray();
        if (files.Select(file => file.TargetPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
            throw new InvalidDataException("A theme file-set target was listed more than once.");
        foreach (var file in files)
        {
            if (!File.Exists(file.InputPath))
                throw new FileNotFoundException("A generated theme file was not found.", file.InputPath);
            if (!File.Exists(file.TargetPath))
                throw new FileNotFoundException("An installed theme target was not found.", file.TargetPath);
        }
        return files;
    }
}

internal static class ThemeFileSetVerifier
{
    internal static void EnsureSucceeded(
        int processExitCode,
        ThemeFileSetManifest manifest,
        ThemeFileSetReport? report,
        string reportPath,
        string operation)
    {
        if (report is null)
            throw new InvalidOperationException(
                $"{operation} did not return its required file-set report. Diagnostic report: {reportPath}");
        // Combined native + panel commands can return a panel-specific nonzero
        // exit after this report has positively verified every native file. The
        // caller handles that later phase; this verifier owns the file set only.
        if (!report.Succeeded)
            throw new InvalidOperationException(
                $"{operation} failed during {report.Stage}: {report.Message} Diagnostic report: {reportPath}");
        foreach (var file in manifest.Files)
        {
            if (!string.Equals(OriginalDllStore.Sha256(file.InputPath),
                    OriginalDllStore.Sha256(file.TargetPath), StringComparison.Ordinal))
                throw new IOException(
                    $"{operation} completed, but {Path.GetFileName(file.TargetPath)} failed final SHA-256 verification.");
        }
    }
}
