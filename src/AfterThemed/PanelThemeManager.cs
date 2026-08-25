using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DvauiThemeEditor;

internal sealed record CepExtensionTarget(
    string Name,
    string RootPath,
    bool IsSigned,
    IReadOnlyList<string> ThemeFiles);

internal sealed record ScriptUiPanelTarget(string Name, string Path, bool IsCompiled);

internal sealed record PanelDiscovery(
    IReadOnlyList<CepExtensionTarget> CepExtensions,
    IReadOnlyList<ScriptUiPanelTarget> ScriptUiPanels,
    IReadOnlyList<string> Warnings)
{
    internal int ThemeFileCount => CepExtensions.Sum(extension => extension.ThemeFiles.Count);
    internal int SignedExtensionCount => CepExtensions.Count(extension => extension.IsSigned);
}

internal sealed class PanelThemeConfiguration
{
    public string ThemeName { get; set; } = "Custom";
    public string Background { get; set; } = "#202124";
    public string Panel { get; set; } = "#292A2D";
    public string Raised { get; set; } = "#3C4043";
    public string Text { get; set; } = "#F1F3F4";
    public string Primary { get; set; } = "#8AB4F8";
    public string Secondary { get; set; } = "#81C995";
    public string Danger { get; set; } = "#F28B82";
    public string? FontFamily { get; set; }
    public bool ThemeSignedExtensions { get; set; } = true;

    internal ThemeSettings ToThemeSettings() => new(
        Parse(Background), Parse(Panel), Parse(Raised), Parse(Text),
        Parse(Primary), Parse(Secondary), Parse(Danger), .5f);

    private static Color Parse(string value)
    {
        try { return ColorTranslator.FromHtml(value); }
        catch { throw new InvalidDataException($"Invalid panel theme color: {value}"); }
    }
}

internal sealed class PanelOperationReport
{
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int CepExtensionsDetected { get; set; }
    public int ScriptUiPanelsDetected { get; set; }
    public int SignedExtensionsSkipped { get; set; }
    public int SignedExtensionsPatched { get; set; }
    public int CepDebugModeChanges { get; set; }
    public int CepDebugModeRestored { get; set; }
    public int ThemeFilesDetected { get; set; }
    public int FilesPatched { get; set; }
    public int FilesRestored { get; set; }
    public int FilesAlreadyRestored { get; set; }
    public int ColorReplacements { get; set; }
    public int HtmlOverridesInjected { get; set; }
    public int Conflicts { get; set; }
    public List<string> Warnings { get; set; } = [];
}

internal static class PanelThemeManager
{
    private const int MaximumThemeFileBytes = 16 * 1024 * 1024;
    private const string HtmlMarkerBegin = "<!-- AFTERTHEMED:THEME:BEGIN -->";
    private const string HtmlMarkerEnd = "<!-- AFTERTHEMED:THEME:END -->";
    private const string ManifestFileName = "panel-backups.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex DeclarationPattern = new(
        @"(?<property>(?:--[\w-]*color[\w-]*|background(?:-color)?|color|border(?:-(?:top|right|bottom|left))?(?:-color)?|outline-color|fill|stroke|box-shadow|text-shadow|caret-color|accent-color))\s*:\s*(?<value>[^;{}]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ColorTokenPattern = new(
        @"(?<![\w-])(?:\#(?:[0-9a-f]{8}|[0-9a-f]{6}|[0-9a-f]{4}|[0-9a-f]{3})(?![0-9a-f])|rgba?\([^)]*\)|hsla?\([^)]*\)|transparent|black|white|gray|grey)(?![\w-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LegacyHtmlColorPattern = new(
        @"(?<prefix>\b(?<attribute>bgcolor|color)\s*=\s*(?<quote>['""]?))(?<color>\#(?:[0-9a-f]{8}|[0-9a-f]{6}|[0-9a-f]{4}|[0-9a-f]{3})|rgba?\([^)]*\)|black|white|gray|grey)(?<suffix>\k<quote>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static void SaveConfiguration(string path, ThemeSettings settings, string themeName, string? fontFamily)
    {
        var configuration = new PanelThemeConfiguration
        {
            ThemeName = string.IsNullOrWhiteSpace(themeName) ? "Custom" : themeName.Trim(),
            Background = Hex(settings.Background),
            Panel = Hex(settings.Panel),
            Raised = Hex(settings.Raised),
            Text = Hex(settings.Text),
            Primary = Hex(settings.Primary),
            Secondary = Hex(settings.Secondary),
            Danger = Hex(settings.Danger),
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? null : fontFamily.Trim()
        };
        WriteJsonAtomic(path, configuration);
    }

    internal static PanelDiscovery Discover(string? targetDllPath, bool includeGlobalRoots = true)
    {
        var warnings = new List<string>();
        var extensions = new Dictionary<string, CepExtensionTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in DiscoverCepRoots(targetDllPath, includeGlobalRoots))
        {
            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(root, "manifest.xml", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    MaxRecursionDepth = 32
                }).Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "CSXS",
                    StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not scan CEP root {root}: {exception.Message}");
                continue;
            }

            foreach (var manifestPath in manifests)
            {
                try
                {
                    var extension = ReadCepExtension(manifestPath);
                    if (extension is not null) extensions[extension.RootPath] = extension;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
                {
                    warnings.Add($"Could not inspect {manifestPath}: {exception.Message}");
                }
            }
        }

        var scriptUi = DiscoverScriptUiPanels(targetDllPath, warnings);
        return new PanelDiscovery(
            extensions.Values.OrderBy(extension => extension.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            scriptUi,
            warnings);
    }

    internal static int ApplyFromConfiguration(string targetDllPath, string backupRoot, string configurationPath,
        string reportPath)
    {
        var report = new PanelOperationReport { Operation = "Apply" };
        try
        {
            var configuration = JsonSerializer.Deserialize<PanelThemeConfiguration>(
                                    File.ReadAllText(configurationPath), JsonOptions) ??
                                throw new InvalidDataException("The panel theme configuration is empty.");
            report = Apply(targetDllPath, backupRoot, configuration);
            WriteJsonAtomic(reportPath, report);
            return report.Conflicts == 0 ? 0 : 8;
        }
        catch (Exception exception)
        {
            report.Warnings.Add(exception.Message);
            TryWriteReport(reportPath, report);
            return 2;
        }
    }

    internal static int RestoreFromBackups(string backupRoot, string reportPath)
    {
        var report = new PanelOperationReport { Operation = "Restore" };
        try
        {
            report = Restore(backupRoot);
            WriteJsonAtomic(reportPath, report);
            return report.Conflicts == 0 ? 0 : 8;
        }
        catch (Exception exception)
        {
            report.Warnings.Add(exception.Message);
            TryWriteReport(reportPath, report);
            return 2;
        }
    }

    internal static PanelOperationReport Apply(string targetDllPath, string backupRoot,
        PanelThemeConfiguration configuration, bool includeGlobalRoots = true, bool manageCepDebugMode = true)
    {
        var discovery = Discover(targetDllPath, includeGlobalRoots);
        var report = new PanelOperationReport
        {
            Operation = "Apply",
            CepExtensionsDetected = discovery.CepExtensions.Count,
            ScriptUiPanelsDetected = discovery.ScriptUiPanels.Count,
            ThemeFilesDetected = discovery.ThemeFileCount,
            Warnings = [.. discovery.Warnings]
        };

        Directory.CreateDirectory(backupRoot);
        var signedExtensionsReady = configuration.ThemeSignedExtensions;
        if (signedExtensionsReady && discovery.SignedExtensionCount > 0 && manageCepDebugMode)
        {
            try
            {
                signedExtensionsReady = CepDeveloperModeManager.EnsureEnabled(targetDllPath, backupRoot,
                    out var changed, out var message);
                if (changed) report.CepDebugModeChanges++;
                report.Warnings.Add(message);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException)
            {
                signedExtensionsReady = false;
                report.Warnings.Add($"Signed CEP panels were skipped because developer mode could not be enabled: {exception.Message}");
            }
        }

        report.SignedExtensionsSkipped = signedExtensionsReady ? 0 : discovery.SignedExtensionCount;
        report.SignedExtensionsPatched = signedExtensionsReady ? discovery.SignedExtensionCount : 0;
        var manifestPath = Path.Combine(backupRoot, ManifestFileName);
        var manifest = LoadManifest(manifestPath);
        var settings = configuration.ToThemeSettings();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in discovery.CepExtensions)
        {
            if (extension.IsSigned && !signedExtensionsReady) continue;
            foreach (var path in extension.ThemeFiles)
            {
                if (!processedPaths.Add(Path.GetFullPath(path))) continue;
                try
                {
                    ApplyFile(path, extension.Name, backupRoot, manifest, settings, configuration, report);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    report.Conflicts++;
                    report.Warnings.Add($"Skipped {path}: {exception.Message}");
                }
            }
        }

        manifest.LastAppliedAtUtc = DateTimeOffset.UtcNow;
        SaveManifest(manifestPath, manifest);
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        return report;
    }

    internal static PanelOperationReport Restore(string backupRoot)
    {
        var manifestPath = Path.Combine(backupRoot, ManifestFileName);
        var manifest = LoadManifest(manifestPath, requireExisting: true);
        var report = new PanelOperationReport { Operation = "Restore" };

        foreach (var entry in manifest.Files
                     .GroupBy(item => item.TargetPath, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.OrderByDescending(item => item.PatchedAtUtc).First()))
        {
            try
            {
                if (!File.Exists(entry.TargetPath))
                {
                    report.Conflicts++;
                    report.Warnings.Add($"Cannot restore missing panel file: {entry.TargetPath}");
                    continue;
                }
                if (!File.Exists(entry.BackupPath) || !Hash(File.ReadAllBytes(entry.BackupPath)).Equals(entry.OriginalSha256, StringComparison.Ordinal))
                    throw new InvalidDataException("The verified panel backup is missing or does not match its recorded SHA-256 hash.");

                var currentHash = Hash(File.ReadAllBytes(entry.TargetPath));
                if (currentHash.Equals(entry.OriginalSha256, StringComparison.Ordinal))
                {
                    report.FilesAlreadyRestored++;
                    continue;
                }
                if (!currentHash.Equals(entry.PatchedSha256, StringComparison.Ordinal))
                {
                    report.Conflicts++;
                    report.Warnings.Add($"Not overwritten because it changed after theming: {entry.TargetPath}");
                    continue;
                }

                var original = File.ReadAllBytes(entry.BackupPath);
                WriteBytesAtomic(entry.TargetPath, original);
                if (!Hash(File.ReadAllBytes(entry.TargetPath)).Equals(entry.OriginalSha256, StringComparison.Ordinal))
                    throw new IOException("The restored panel file failed SHA-256 verification.");
                report.FilesRestored++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                report.Conflicts++;
                report.Warnings.Add($"Could not restore {entry.TargetPath}: {exception.Message}");
            }
        }

        manifest.LastRestoredAtUtc = DateTimeOffset.UtcNow;
        SaveManifest(manifestPath, manifest);
        var debugModeConflicts = 0;
        report.CepDebugModeRestored = CepDeveloperModeManager.Restore(backupRoot, report.Warnings, ref debugModeConflicts);
        report.Conflicts += debugModeConflicts;
        report.CompletedAtUtc = DateTimeOffset.UtcNow;
        return report;
    }

    internal static bool RunSmokeTest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"AfterThemed-panel-smoke-{Guid.NewGuid():N}");
        try
        {
            var support = Path.Combine(root, "Support Files");
            var unsignedRoot = Path.Combine(support, "Extensions", "com.afterthemed.unsigned");
            var signedRoot = Path.Combine(support, "Extensions", "com.afterthemed.signed");
            var signedChildRoot = Path.Combine(signedRoot, "extensions", "com.afterthemed.signed-child");
            Directory.CreateDirectory(Path.Combine(unsignedRoot, "CSXS"));
            Directory.CreateDirectory(Path.Combine(unsignedRoot, "css"));
            Directory.CreateDirectory(Path.Combine(signedRoot, "CSXS"));
            Directory.CreateDirectory(Path.Combine(signedRoot, "META-INF"));
            Directory.CreateDirectory(Path.Combine(signedChildRoot, "CSXS"));
            Directory.CreateDirectory(Path.Combine(support, "Scripts", "ScriptUI Panels"));

            var targetDll = Path.Combine(support, "dvaui.dll");
            File.WriteAllBytes(targetDll, [0x4D, 0x5A]);
            const string unsignedManifest = "<ExtensionManifest ExtensionBundleName=\"Unsigned Smoke\"><ExecutionEnvironment><HostList><Host Name=\"AEFT\" Version=\"[1.0,99.9]\" /></HostList></ExecutionEnvironment></ExtensionManifest>";
            const string signedManifest = "<ExtensionManifest ExtensionBundleName=\"Signed Smoke\"><ExecutionEnvironment><HostList><Host Name=\"AEFT\" Version=\"[1.0,99.9]\" /></HostList></ExecutionEnvironment></ExtensionManifest>";
            File.WriteAllText(Path.Combine(unsignedRoot, "CSXS", "manifest.xml"), unsignedManifest);
            File.WriteAllText(Path.Combine(signedRoot, "CSXS", "manifest.xml"), signedManifest);
            File.WriteAllText(Path.Combine(signedChildRoot, "CSXS", "manifest.xml"), signedManifest.Replace("Signed Smoke", "Signed Child Smoke"));
            File.WriteAllText(Path.Combine(signedRoot, "META-INF", "signatures.xml"), "<signatures />");
            File.WriteAllText(Path.Combine(support, "Scripts", "ScriptUI Panels", "Smoke.jsx"), "new Window('palette');");

            var htmlPath = Path.Combine(unsignedRoot, "index.html");
            var cssPath = Path.Combine(unsignedRoot, "css", "main.css");
            var conflictCssPath = Path.Combine(unsignedRoot, "css", "conflict.css");
            var signedHtmlPath = Path.Combine(signedRoot, "index.html");
            var signedChildHtmlPath = Path.Combine(signedChildRoot, "index.html");
            var originalHtml = new UTF8Encoding(false).GetBytes("<!doctype html><html><head></head><body style=\"background:#111;color:#eee\">Smoke</body></html>");
            var originalCss = new UTF8Encoding(false).GetBytes("body{background-color:#111;color:rgb(238,238,238)} button{border-color:#555;background:#333;color:white}");
            var originalConflictCss = new UTF8Encoding(false).GetBytes("div{color:#eee}");
            var signedHtml = new UTF8Encoding(false).GetBytes("<html><head></head><body style=\"background:black;color:white\">Signed</body></html>");
            File.WriteAllBytes(htmlPath, originalHtml);
            File.WriteAllBytes(cssPath, originalCss);
            File.WriteAllBytes(conflictCssPath, originalConflictCss);
            File.WriteAllBytes(signedHtmlPath, signedHtml);
            File.WriteAllBytes(signedChildHtmlPath, signedHtml);

            var discovery = Discover(targetDll, includeGlobalRoots: false);
            if (discovery.CepExtensions.Count != 3 || discovery.ScriptUiPanels.Count != 1 || discovery.SignedExtensionCount != 2)
                return false;

            var backupRoot = Path.Combine(root, "backups");
            var configuration = new PanelThemeConfiguration
            {
                ThemeName = "Smoke",
                Background = "#1D2021",
                Panel = "#282828",
                Raised = "#504945",
                Text = "#EBDBB2",
                Primary = "#FABD2F",
                Secondary = "#8EC07C",
                Danger = "#FB4934",
                FontFamily = "Inter"
            };
            var applied = Apply(targetDll, backupRoot, configuration, includeGlobalRoots: false, manageCepDebugMode: false);
            var themedSignedHtml = File.ReadAllText(signedHtmlPath);
            if (applied.FilesPatched != 5 || applied.SignedExtensionsSkipped != 0 || applied.ColorReplacements < 6 ||
                !File.ReadAllText(htmlPath).Contains(HtmlMarkerBegin, StringComparison.Ordinal) ||
                !themedSignedHtml.Contains(HtmlMarkerBegin, StringComparison.Ordinal) ||
                !themedSignedHtml.Contains("--color-panel-gray: #282828 !important", StringComparison.OrdinalIgnoreCase) ||
                !themedSignedHtml.Contains("CanvasRenderingContext2D", StringComparison.Ordinal) ||
                !File.ReadAllText(signedChildHtmlPath).Contains(HtmlMarkerBegin, StringComparison.Ordinal)) return false;

            configuration.Background = "#F1EAF7";
            configuration.Panel = "#E6DAF1";
            configuration.Raised = "#CDBBE4";
            configuration.Text = "#2F1946";
            configuration.Primary = "#9F82D9";
            configuration.Secondary = "#6F559F";
            configuration.Danger = "#B3261E";
            var reapplied = Apply(targetDll, backupRoot, configuration, includeGlobalRoots: false, manageCepDebugMode: false);
            var reappliedHtml = File.ReadAllText(htmlPath);
            if (reapplied.FilesPatched != 5 ||
                Regex.Matches(reappliedHtml, Regex.Escape(HtmlMarkerBegin)).Count != 1 ||
                !reappliedHtml.Contains("#F1EAF7", StringComparison.OrdinalIgnoreCase)) return false;

            var externalUpdate = new UTF8Encoding(false).GetBytes("div{color:#123456}/* external update */");
            File.WriteAllBytes(conflictCssPath, externalUpdate);
            var restored = Restore(backupRoot);
            return restored.FilesRestored == 4 && restored.Conflicts == 1 &&
                   File.ReadAllBytes(htmlPath).SequenceEqual(originalHtml) &&
                   File.ReadAllBytes(cssPath).SequenceEqual(originalCss) &&
                   File.ReadAllBytes(conflictCssPath).SequenceEqual(externalUpdate) &&
                   File.ReadAllBytes(signedHtmlPath).SequenceEqual(signedHtml) &&
                   File.ReadAllBytes(signedChildHtmlPath).SequenceEqual(signedHtml);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void ApplyFile(string path, string extensionName, string backupRoot, PanelBackupManifest manifest,
        ThemeSettings settings, PanelThemeConfiguration configuration, PanelOperationReport report)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The detected panel theme file no longer exists.", path);
        if (info.Length > MaximumThemeFileBytes)
            throw new InvalidDataException($"The file exceeds the {MaximumThemeFileBytes / 1024 / 1024} MB safety limit.");

        var current = File.ReadAllBytes(path);
        var currentHash = Hash(current);
        var entry = manifest.Files
            .Where(item => item.TargetPath.Equals(path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.PatchedAtUtc)
            .FirstOrDefault();

        byte[] original;
        if (entry is not null &&
            (currentHash.Equals(entry.PatchedSha256, StringComparison.Ordinal) ||
             currentHash.Equals(entry.OriginalSha256, StringComparison.Ordinal)) &&
            File.Exists(entry.BackupPath))
        {
            original = File.ReadAllBytes(entry.BackupPath);
            if (!Hash(original).Equals(entry.OriginalSha256, StringComparison.Ordinal))
                throw new InvalidDataException("The existing panel backup failed SHA-256 verification.");
        }
        else
        {
            original = current;
            var originalHash = Hash(original);
            var pathKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path))))[..16];
            var backupDirectory = Path.Combine(backupRoot, "files", pathKey);
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(backupDirectory, $"{Path.GetFileName(path)}.{originalHash[..16]}.original");
            if (!File.Exists(backupPath)) WriteBytesAtomic(backupPath, original);
            if (!Hash(File.ReadAllBytes(backupPath)).Equals(originalHash, StringComparison.Ordinal))
                throw new IOException("The panel backup failed SHA-256 verification.");

            if (entry is not null) manifest.Files.Remove(entry);
            entry = new PanelBackupEntry
            {
                TargetPath = Path.GetFullPath(path),
                ExtensionName = extensionName,
                BackupPath = backupPath,
                OriginalSha256 = originalHash
            };
            manifest.Files.Add(entry);
        }

        var decoded = DecodeText(original);
        var profile = PaletteProfile.Create(decoded.Text);
        var transformed = RewriteDeclarations(decoded.Text, settings, profile, out var replacements);
        var isHtml = Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetExtension(path).Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                     Path.GetExtension(path).Equals(".xhtml", StringComparison.OrdinalIgnoreCase);
        if (isHtml)
        {
            transformed = RewriteLegacyHtmlColors(transformed, settings, profile, ref replacements);
            transformed = InjectHtmlOverride(transformed, settings, configuration.FontFamily);
            report.HtmlOverridesInjected++;
        }

        if (!isHtml && replacements == 0) return;
        var patched = decoded.Encode(transformed);
        var patchedHash = Hash(patched);
        entry.PatchedSha256 = patchedHash;
        entry.PatchedAtUtc = DateTimeOffset.UtcNow;
        entry.ColorReplacements = replacements;

        if (!currentHash.Equals(patchedHash, StringComparison.Ordinal))
        {
            WriteBytesAtomic(path, patched);
            if (!Hash(File.ReadAllBytes(path)).Equals(patchedHash, StringComparison.Ordinal))
                throw new IOException("The themed panel file failed final SHA-256 verification.");
        }
        report.FilesPatched++;
        report.ColorReplacements += replacements;
    }

    private static string RewriteDeclarations(string text, ThemeSettings settings, PaletteProfile profile,
        out int replacements)
    {
        var changed = 0;
        var result = DeclarationPattern.Replace(text, declaration =>
        {
            var property = declaration.Groups["property"].Value;
            var value = declaration.Groups["value"].Value;
            var mapped = ReplaceColorTokens(value, property, settings, profile, ref changed);
            var valueOffset = declaration.Groups["value"].Index - declaration.Index;
            return declaration.Value[..valueOffset] + mapped;
        });
        replacements = changed;
        return result;
    }

    private static string ReplaceColorTokens(string value, string property, ThemeSettings settings,
        PaletteProfile profile, ref int replacements)
    {
        var localChanges = 0;
        var result = ColorTokenPattern.Replace(value, match =>
        {
            if (IsInsideUrl(value, match.Index) || !TryParseColor(match.Value, out var source)) return match.Value;
            if (source.A == 0) return match.Value;
            var mapped = MapPanelColor(source, property, settings, profile);
            localChanges++;
            return Css(mapped);
        });
        replacements += localChanges;
        return result;
    }

    private static string RewriteLegacyHtmlColors(string text, ThemeSettings settings, PaletteProfile profile,
        ref int replacements)
    {
        var localChanges = 0;
        var result = LegacyHtmlColorPattern.Replace(text, match =>
        {
            if (!TryParseColor(match.Groups["color"].Value, out var source)) return match.Value;
            var property = match.Groups["attribute"].Value.Equals("bgcolor", StringComparison.OrdinalIgnoreCase)
                ? "background-color"
                : "color";
            var mapped = MapPanelColor(source, property, settings, profile);
            localChanges++;
            return match.Groups["prefix"].Value + Css(mapped) + match.Groups["suffix"].Value;
        });
        replacements += localChanges;
        return result;
    }

    private static string InjectHtmlOverride(string text, ThemeSettings settings, string? fontFamily)
    {
        var start = text.IndexOf(HtmlMarkerBegin, StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = text.IndexOf(HtmlMarkerEnd, start, StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidDataException("An incomplete AfterThemed HTML marker was found; the file was not overwritten.");
            text = text.Remove(start, end + HtmlMarkerEnd.Length - start);
        }

        var fontRule = string.IsNullOrWhiteSpace(fontFamily)
            ? string.Empty
            : $"\n  font-family: '{CssEscape(fontFamily)}', sans-serif !important;";
        var onPrimary = BestText(settings.Primary, settings.Text);
        var block = $$"""
{{HtmlMarkerBegin}}
<style id="afterthemed-theme" type="text/css">
:root {
  --afterthemed-background: {{Css(settings.Background)}};
  --afterthemed-panel: {{Css(settings.Panel)}};
  --afterthemed-raised: {{Css(settings.Raised)}};
  --afterthemed-text: {{Css(settings.Text)}};
  --afterthemed-primary: {{Css(settings.Primary)}};
  --afterthemed-secondary: {{Css(settings.Secondary)}};
  --afterthemed-danger: {{Css(settings.Danger)}};
  --color-panel-gray: {{Css(settings.Panel)}} !important;
  --color-dark-gray: {{Css(settings.Background)}} !important;
  --color-mid-gray: {{Css(settings.Text)}} !important;
  --color-graph-gray: {{Css(settings.Raised)}} !important;
  --color-graph-gray-with-alpha: {{Css(Color.FromArgb(119, settings.Raised))}} !important;
  --color-light-gray: {{Css(settings.Text)}} !important;
  --color-white: {{Css(settings.Text)}} !important;
  --color-blue: {{Css(settings.Primary)}} !important;
  --color-yellow: {{Css(settings.Primary)}} !important;
  --color-orange: {{Css(settings.Danger)}} !important;
  --colorAccent: {{Css(settings.Primary)}} !important;
  --colorBlue: {{Css(settings.Primary)}} !important;
  --colorGreen: {{Css(settings.Secondary)}} !important;
  --colorYellow: {{Css(settings.Primary)}} !important;
  --colorOrange: {{Css(settings.Danger)}} !important;
  --colorRed: {{Css(settings.Danger)}} !important;
}
html, body {
  background-color: var(--afterthemed-background) !important;
  color: var(--afterthemed-text) !important;{{fontRule}}
}
#container, #root, .hostBgd, .hostBg, .bg-elevation2 {
  background-color: var(--afterthemed-panel) !important;
  color: var(--afterthemed-text) !important;
}
#graphPanel-top, #canvas-wrapper, #graphEditor, #graphEditorSpace, .bg-elevation3 {
  background-color: var(--afterthemed-background) !important;
}
.bg-elevation1 { background-color: var(--afterthemed-raised) !important; }
.text-highlight1, .text-highlight2, .text-highlight3, .text-white, .text-wht-400 {
  color: var(--afterthemed-text) !important;
}
button, select, input, textarea {
  background-color: var(--afterthemed-raised) !important;
  color: var(--afterthemed-text) !important;
  border-color: var(--afterthemed-primary) !important;{{fontRule}}
}
a, [role="link"] { color: var(--afterthemed-secondary) !important; }
progress, input[type="range"], input[type="checkbox"], input[type="radio"] {
  accent-color: var(--afterthemed-primary) !important;
}
::selection { background: var(--afterthemed-primary); color: {{Css(onPrimary)}}; }
</style>
<script id="afterthemed-runtime" type="text/javascript">
(function () {
  'use strict';
  var palette = {
    background: '{{Css(settings.Background)}}',
    panel: '{{Css(settings.Panel)}}',
    raised: '{{Css(settings.Raised)}}',
    text: '{{Css(settings.Text)}}',
    primary: '{{Css(settings.Primary)}}',
    secondary: '{{Css(settings.Secondary)}}',
    danger: '{{Css(settings.Danger)}}'
  };
  var aliases = {
    '--color-panel-gray': palette.panel,
    '--color-dark-gray': palette.background,
    '--color-mid-gray': palette.text,
    '--color-graph-gray': palette.raised,
    '--color-graph-gray-with-alpha': '{{Css(Color.FromArgb(119, settings.Raised))}}',
    '--color-light-gray': palette.text,
    '--color-white': palette.text,
    '--color-blue': palette.primary,
    '--color-yellow': palette.primary,
    '--color-orange': palette.danger,
    '--colorAccent': palette.primary,
    '--colorBlue': palette.primary,
    '--colorGreen': palette.secondary,
    '--colorYellow': palette.primary,
    '--colorOrange': palette.danger,
    '--colorRed': palette.danger
  };
  var colorToken = /#[0-9a-f]{3,8}\b|rgba?\([^)]*\)|\b(?:black|white|gray|grey)\b/gi;
  var colorProperty = /color|background|border|outline|fill|stroke|shadow|caret|accent/i;

  function parseColor(value) {
    var input = String(value || '').trim().toLowerCase();
    var match;
    if (input === 'black') return { r: 0, g: 0, b: 0, a: 1 };
    if (input === 'white') return { r: 255, g: 255, b: 255, a: 1 };
    if (input === 'gray' || input === 'grey') return { r: 128, g: 128, b: 128, a: 1 };
    if (input.charAt(0) === '#') {
      var hex = input.substring(1);
      if (hex.length === 3 || hex.length === 4) {
        return {
          r: parseInt(hex.charAt(0) + hex.charAt(0), 16),
          g: parseInt(hex.charAt(1) + hex.charAt(1), 16),
          b: parseInt(hex.charAt(2) + hex.charAt(2), 16),
          a: hex.length === 4 ? parseInt(hex.charAt(3) + hex.charAt(3), 16) / 255 : 1
        };
      }
      if (hex.length === 6 || hex.length === 8) {
        return {
          r: parseInt(hex.substring(0, 2), 16),
          g: parseInt(hex.substring(2, 4), 16),
          b: parseInt(hex.substring(4, 6), 16),
          a: hex.length === 8 ? parseInt(hex.substring(6, 8), 16) / 255 : 1
        };
      }
    }
    match = input.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*[,/]\s*([\d.]+%?))?\s*\)$/i);
    if (!match) return null;
    var alpha = 1;
    if (match[4]) alpha = match[4].indexOf('%') > -1 ? parseFloat(match[4]) / 100 : parseFloat(match[4]);
    return { r: +match[1], g: +match[2], b: +match[3], a: alpha };
  }

  function hexByte(value) {
    var result = Math.max(0, Math.min(255, Math.round(value))).toString(16).toUpperCase();
    return result.length === 1 ? '0' + result : result;
  }

  function withAlpha(color, alpha) {
    if (alpha >= 0.999) return color;
    var parsed = parseColor(color);
    return parsed ? 'rgba(' + parsed.r + ', ' + parsed.g + ', ' + parsed.b + ', ' + Math.max(0, alpha).toFixed(3) + ')' : color;
  }

  function sameTarget(source) {
    for (var name in palette) {
      if (!Object.prototype.hasOwnProperty.call(palette, name)) continue;
      var target = parseColor(palette[name]);
      if (target && Math.round(source.r) === target.r && Math.round(source.g) === target.g && Math.round(source.b) === target.b)
        return withAlpha(palette[name], source.a);
    }
    return null;
  }

  function mapColor(token, property) {
    var source = parseColor(token);
    if (!source || source.a <= 0) return token;
    var existing = sameTarget(source);
    if (existing) return existing;
    var key = String(property || '').toLowerCase();
    var max = Math.max(source.r, source.g, source.b);
    var min = Math.min(source.r, source.g, source.b);
    var saturation = max <= 0 ? 0 : (max - min) / max;
    var luminance = (0.2126 * source.r + 0.7152 * source.g + 0.0722 * source.b) / 255;
    var mapped;

    if (/danger|error|panic|red|orange/.test(key)) mapped = palette.danger;
    else if (/secondary|success|green/.test(key)) mapped = palette.secondary;
    else if (/primary|accent|blue|yellow/.test(key)) mapped = palette.primary;
    else if (/graph|raised|elevation1/.test(key)) mapped = palette.raised;
    else if (/panel|surface|elevation2/.test(key)) mapped = palette.panel;
    else if (/background|dark|elevation3/.test(key)) mapped = palette.background;
    else if (/^(color|fill|stroke)|text|font|foreground|highlight|white|light|caret|shadow/.test(key)) {
      if (saturation < 0.22) mapped = palette.text;
      else if (source.r > source.g * 1.25 && source.r > source.b * 1.25) mapped = palette.danger;
      else if (source.g > source.r * 1.12 && source.g > source.b * 1.05) mapped = palette.secondary;
      else mapped = palette.primary;
    }
    else if (saturation >= 0.35) {
      if (source.r > source.g * 1.25 && source.r > source.b * 1.25) mapped = palette.danger;
      else if (source.g > source.r * 1.12 && source.g > source.b * 1.05) mapped = palette.secondary;
      else mapped = palette.primary;
    }
    else mapped = luminance < 0.22 ? palette.background : (luminance < 0.55 ? palette.panel : palette.raised);
    return withAlpha(mapped, source.a);
  }

  function replaceColors(value, property) {
    return String(value).replace(colorToken, function (token) { return mapColor(token, property); });
  }

  function themeStyle(style, forceImportant) {
    if (!style) return;
    for (var index = 0; index < style.length; index++) {
      var property = style[index];
      if (!colorProperty.test(property)) continue;
      var current = style.getPropertyValue(property);
      var themed = replaceColors(current, property);
      if (themed !== current) style.setProperty(property, themed, forceImportant ? 'important' : style.getPropertyPriority(property));
    }
  }

  function themeRules(rules) {
    if (!rules) return;
    for (var index = 0; index < rules.length; index++) {
      var rule = rules[index];
      try {
        if (rule.cssRules) themeRules(rule.cssRules);
        if (rule.style) themeStyle(rule.style, false);
      } catch (_) { }
    }
  }

  function themeSheets() {
    for (var index = 0; index < document.styleSheets.length; index++) {
      var sheet = document.styleSheets[index];
      if (sheet.ownerNode && sheet.ownerNode.id === 'afterthemed-theme') continue;
      try { themeRules(sheet.cssRules); } catch (_) { }
    }
  }

  function themeElement(element) {
    if (!element || element.nodeType !== 1) return;
    themeStyle(element.style, true);
    var stroke = element.getAttribute('stroke');
    if (stroke) {
      var themedStroke = replaceColors(stroke, 'stroke');
      if (themedStroke !== stroke) element.setAttribute('stroke', themedStroke);
    }
    var fill = element.getAttribute('fill');
    if (fill && fill.toLowerCase() !== 'none' && fill.toLowerCase() !== 'transparent') {
      var themedFill = replaceColors(fill, 'fill');
      if (themedFill !== fill) element.setAttribute('fill', themedFill);
    }
  }

  function themeTree(root) {
    themeElement(root);
    if (!root || !root.querySelectorAll) return;
    var elements = root.querySelectorAll('[style], [stroke], [fill]');
    for (var index = 0; index < elements.length; index++) themeElement(elements[index]);
  }

  function applyAliases() {
    var root = document.documentElement;
    if (!root) return;
    for (var property in aliases) {
      if (!Object.prototype.hasOwnProperty.call(aliases, property)) continue;
      if (root.style.getPropertyValue(property) !== aliases[property] || root.style.getPropertyPriority(property) !== 'important')
        root.style.setProperty(property, aliases[property], 'important');
    }
  }

  function applyTheme() {
    applyAliases();
    themeSheets();
    themeTree(document.documentElement);
  }

  try {
    var canvasPrototype = window.CanvasRenderingContext2D && window.CanvasRenderingContext2D.prototype;
    var strokeDescriptor = canvasPrototype && Object.getOwnPropertyDescriptor(canvasPrototype, 'strokeStyle');
    if (strokeDescriptor && strokeDescriptor.get && strokeDescriptor.set && strokeDescriptor.configurable) {
      Object.defineProperty(canvasPrototype, 'strokeStyle', {
        configurable: strokeDescriptor.configurable,
        enumerable: strokeDescriptor.enumerable,
        get: strokeDescriptor.get,
        set: function (value) {
          strokeDescriptor.set.call(this, typeof value === 'string' ? mapColor(value, 'stroke') : value);
        }
      });
    }
  } catch (_) { }

  try {
    var sheetPrototype = window.CSSStyleSheet && window.CSSStyleSheet.prototype;
    var originalInsertRule = sheetPrototype && sheetPrototype.insertRule;
    var originalAddRule = sheetPrototype && sheetPrototype.addRule;
    if (originalInsertRule) {
      sheetPrototype.insertRule = function () {
        var result = originalInsertRule.apply(this, arguments);
        try { themeRules(this.cssRules); } catch (_) { }
        return result;
      };
    }
    if (originalAddRule) {
      sheetPrototype.addRule = function () {
        var result = originalAddRule.apply(this, arguments);
        try { themeRules(this.cssRules); } catch (_) { }
        return result;
      };
    }
  } catch (_) { }

  function startObserver() {
    if (!document.documentElement || !window.MutationObserver) return;
    new MutationObserver(function (records) {
      for (var index = 0; index < records.length; index++) {
        var record = records[index];
        if (record.type === 'attributes') themeElement(record.target);
        for (var child = 0; child < record.addedNodes.length; child++) themeTree(record.addedNodes[child]);
      }
      applyAliases();
      themeSheets();
    }).observe(document.documentElement, {
      subtree: true,
      childList: true,
      attributes: true,
      attributeFilter: ['style', 'stroke', 'fill']
    });
  }

  applyAliases();
  document.addEventListener('DOMContentLoaded', function () { applyTheme(); startObserver(); });
  window.addEventListener('load', function () {
    applyTheme();
    window.setTimeout(applyTheme, 50);
    window.setTimeout(applyTheme, 250);
    window.setTimeout(applyTheme, 1000);
  }, true);
}());
</script>
{{HtmlMarkerEnd}}
""";
        var headEnd = text.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headEnd >= 0 ? text.Insert(headEnd, block + Environment.NewLine) : block + Environment.NewLine + text;
    }

    private static Color MapPanelColor(Color source, string property, ThemeSettings target, PaletteProfile profile)
    {
        var alpha = source.A;
        var saturation = Saturation(source);
        var luminance = Luminance(source);
        Color mapped;

        if (property.Contains("shadow", StringComparison.OrdinalIgnoreCase))
            mapped = target.Text;
        else if (property.Equals("color", StringComparison.OrdinalIgnoreCase) ||
                 property.Equals("fill", StringComparison.OrdinalIgnoreCase) ||
                 property.Equals("stroke", StringComparison.OrdinalIgnoreCase) ||
                 property.Contains("caret", StringComparison.OrdinalIgnoreCase))
            mapped = saturation < .18 ? target.Text : AccentFor(source, target);
        else if (property.Contains("border", StringComparison.OrdinalIgnoreCase) ||
                 property.Contains("outline", StringComparison.OrdinalIgnoreCase))
            mapped = saturation < .18 ? target.Raised : AccentFor(source, target);
        else if (property.Contains("accent", StringComparison.OrdinalIgnoreCase))
            mapped = target.Primary;
        else if (saturation >= .35)
            mapped = AccentFor(source, target);
        else
            mapped = profile.MapSurface(luminance, target);

        return Color.FromArgb(alpha, mapped);
    }

    private static Color AccentFor(Color source, ThemeSettings settings)
    {
        var hue = source.GetHue();
        if (hue < 25 || hue >= 330) return settings.Danger;
        if (hue is >= 70 and < 200) return settings.Secondary;
        return settings.Primary;
    }

    private static Color BestText(Color background, Color preferred)
    {
        var white = Color.White;
        var black = Color.Black;
        var preferredContrast = Contrast(background, preferred);
        var whiteContrast = Contrast(background, white);
        var blackContrast = Contrast(background, black);
        if (preferredContrast >= 4.5) return preferred;
        return whiteContrast >= blackContrast ? white : black;
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= .04045 ? normalized / 12.92 : Math.Pow((normalized + .055) / 1.055, 2.4);
        }
        static double Relative(Color color) => .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        var a = Relative(first);
        var b = Relative(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static bool TryParseColor(string token, out Color color)
    {
        color = default;
        var value = token.Trim();
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.Transparent;
            return true;
        }
        if (value.Equals("black", StringComparison.OrdinalIgnoreCase)) { color = Color.Black; return true; }
        if (value.Equals("white", StringComparison.OrdinalIgnoreCase)) { color = Color.White; return true; }
        if (value.Equals("gray", StringComparison.OrdinalIgnoreCase) || value.Equals("grey", StringComparison.OrdinalIgnoreCase))
        { color = Color.Gray; return true; }

        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            try
            {
                color = hex.Length switch
                {
                    3 => Color.FromArgb(255, Expand(hex[0]), Expand(hex[1]), Expand(hex[2])),
                    4 => Color.FromArgb(Expand(hex[3]), Expand(hex[0]), Expand(hex[1]), Expand(hex[2])),
                    6 => Color.FromArgb(255, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16)),
                    8 => Color.FromArgb(Convert.ToByte(hex[6..8], 16), Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16)),
                    _ => default
                };
                return hex.Length is 3 or 4 or 6 or 8;
            }
            catch (FormatException) { return false; }
        }

        var function = Regex.Match(value, @"^(?<name>rgba?|hsla?)\((?<values>.*)\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!function.Success) return false;
        var parts = function.Groups["values"].Value.Split(',', StringSplitOptions.TrimEntries);
        if (function.Groups["name"].Value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length is < 3 or > 4 || !TryRgb(parts[0], out var r) || !TryRgb(parts[1], out var g) ||
                !TryRgb(parts[2], out var b) || !TryAlpha(parts.ElementAtOrDefault(3), out var a)) return false;
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        if (parts.Length is < 3 or > 4 || !double.TryParse(parts[0].TrimEnd('d', 'e', 'g'),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hue) ||
            !TryPercent(parts[1], out var saturation) || !TryPercent(parts[2], out var lightness) ||
            !TryAlpha(parts.ElementAtOrDefault(3), out var alpha)) return false;
        color = Hsl(hue, saturation, lightness, alpha);
        return true;
    }

    private static bool TryRgb(string value, out int component)
    {
        component = 0;
        var percent = value.EndsWith('%');
        if (!double.TryParse(value.TrimEnd('%'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return false;
        component = (int)Math.Clamp(Math.Round(percent ? parsed * 2.55 : parsed), 0, 255);
        return true;
    }

    private static bool TryAlpha(string? value, out int alpha)
    {
        alpha = 255;
        if (string.IsNullOrWhiteSpace(value)) return true;
        var percent = value.EndsWith('%');
        if (!double.TryParse(value.TrimEnd('%'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return false;
        alpha = (int)Math.Clamp(Math.Round(percent ? parsed * 2.55 : parsed * 255), 0, 255);
        return true;
    }

    private static bool TryPercent(string value, out double result)
    {
        result = 0;
        if (!value.EndsWith('%') || !double.TryParse(value.TrimEnd('%'),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return false;
        result = Math.Clamp(parsed / 100, 0, 1);
        return true;
    }

    private static Color Hsl(double hue, double saturation, double lightness, int alpha)
    {
        hue = ((hue % 360) + 360) % 360;
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - chroma / 2;
        var (r, g, b) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return Color.FromArgb(alpha, ClampByte((r + m) * 255), ClampByte((g + m) * 255), ClampByte((b + m) * 255));
    }

    private static IEnumerable<string> DiscoverCepRoots(string? targetDllPath, bool includeGlobalRoots)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(targetDllPath) && File.Exists(targetDllPath))
            candidates.Add(Path.GetDirectoryName(Path.GetFullPath(targetDllPath))!);

        if (!includeGlobalRoots)
            return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        candidates.AddRange([
            Path.Combine(appData, "Adobe", "CEP", "extensions"),
            Path.Combine(localAppData, "Adobe", "CEP", "extensions"),
            Path.Combine(programFiles, "Common Files", "Adobe", "CEP", "extensions"),
            Path.Combine(programFilesX86, "Common Files", "Adobe", "CEP", "extensions")
        ]);
        return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static CepExtensionTarget? ReadCepExtension(string manifestPath)
    {
        var document = XDocument.Load(manifestPath, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("The CEP manifest has no document element.");
        var hosts = document.Descendants()
            .Where(element => element.Name.LocalName.Equals("Host", StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        if (hosts.Length > 0 && !hosts.Any(name => name!.Equals("AEFT", StringComparison.OrdinalIgnoreCase))) return null;

        var csxsDirectory = Path.GetDirectoryName(manifestPath)!;
        var extensionRoot = Path.GetFullPath(Path.GetDirectoryName(csxsDirectory)!);
        var name = (string?)root.Attribute("ExtensionBundleName") ??
                   (string?)root.Attribute("ExtensionBundleId") ?? Path.GetFileName(extensionRoot);
        var signed = HasCepPackageSignature(extensionRoot);
        var themeFiles = Directory.EnumerateFiles(extensionRoot, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MaxRecursionDepth = 32
        })
            .Where(path => Path.GetExtension(path) is ".css" or ".html" or ".htm" or ".xhtml" ||
                           Path.GetExtension(path).Equals(".css", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetExtension(path).Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetExtension(path).Equals(".xhtml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !HasDirectorySegment(path, "META-INF") && !HasDirectorySegment(path, ".git"))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CepExtensionTarget(name, extensionRoot, signed, themeFiles);
    }

    private static IReadOnlyList<ScriptUiPanelTarget> DiscoverScriptUiPanels(string? targetDllPath, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(targetDllPath) || !File.Exists(targetDllPath)) return [];
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(targetDllPath))!, "Scripts", "ScriptUI Panels");
        if (!Directory.Exists(root)) return [];
        try
        {
            return Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                MaxRecursionDepth = 16
            })
                .Where(path => Path.GetExtension(path).Equals(".jsx", StringComparison.OrdinalIgnoreCase) ||
                               Path.GetExtension(path).Equals(".jsxbin", StringComparison.OrdinalIgnoreCase))
                .Select(path => new ScriptUiPanelTarget(Path.GetFileNameWithoutExtension(path), Path.GetFullPath(path),
                    Path.GetExtension(path).Equals(".jsxbin", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(panel => panel.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not scan ScriptUI panels: {exception.Message}");
            return [];
        }
    }

    private static bool HasDirectorySegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));

    private static bool HasCepPackageSignature(string extensionRoot)
    {
        var directory = new DirectoryInfo(extensionRoot);
        for (var depth = 0; depth < 10 && directory is not null; depth++, directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "META-INF", "signatures.xml"))) return true;
            if (directory.Parent is null || directory.Parent.FullName.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase))
                break;
        }
        return false;
    }

    private static bool IsInsideUrl(string value, int index)
    {
        var prefix = value[..index];
        var open = prefix.LastIndexOf("url(", StringComparison.OrdinalIgnoreCase);
        var close = prefix.LastIndexOf(')');
        return open > close;
    }

    private static string Css(Color color) => color.A == 255
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"rgba({color.R}, {color.G}, {color.B}, {(color.A / 255d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static string CssEscape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
    private static int Expand(char value) => Convert.ToInt32(new string(value, 2), 16);
    private static int ClampByte(double value) => (int)Math.Clamp(Math.Round(value), 0, 255);
    private static double Saturation(Color color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B)) / 255d;
        var min = Math.Min(color.R, Math.Min(color.G, color.B)) / 255d;
        return max <= 0 ? 0 : (max - min) / max;
    }
    private static double Luminance(Color color) => (.2126 * color.R + .7152 * color.G + .0722 * color.B) / 255d;
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static DecodedText DecodeText(byte[] data)
    {
        if (data.AsSpan().StartsWith(Encoding.UTF8.Preamble))
            return new DecodedText(Encoding.UTF8.GetString(data, Encoding.UTF8.Preamble.Length, data.Length - Encoding.UTF8.Preamble.Length),
                new UTF8Encoding(true));
        if (data.AsSpan().StartsWith(Encoding.Unicode.Preamble))
            return new DecodedText(Encoding.Unicode.GetString(data, Encoding.Unicode.Preamble.Length, data.Length - Encoding.Unicode.Preamble.Length), Encoding.Unicode);
        if (data.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
            return new DecodedText(Encoding.BigEndianUnicode.GetString(data, Encoding.BigEndianUnicode.Preamble.Length,
                data.Length - Encoding.BigEndianUnicode.Preamble.Length), Encoding.BigEndianUnicode);
        try { return new DecodedText(new UTF8Encoding(false, true).GetString(data), new UTF8Encoding(false)); }
        catch (DecoderFallbackException) { return new DecodedText(Encoding.Latin1.GetString(data), Encoding.Latin1); }
    }

    private static PanelBackupManifest LoadManifest(string path, bool requireExisting = false)
    {
        if (!File.Exists(path))
        {
            if (requireExisting) throw new FileNotFoundException("No CEP panel backup manifest exists to restore.", path);
            return new PanelBackupManifest();
        }
        return JsonSerializer.Deserialize<PanelBackupManifest>(File.ReadAllText(path), JsonOptions) ??
               throw new InvalidDataException("The CEP panel backup manifest is empty.");
    }

    private static void SaveManifest(string path, PanelBackupManifest manifest) => WriteJsonAtomic(path, manifest);

    private static void TryWriteReport(string path, PanelOperationReport report)
    {
        try { WriteJsonAtomic(path, report); }
        catch { /* Preserve the operation's original error code. */ }
    }

    private static void WriteJsonAtomic<T>(string path, T value) =>
        WriteBytesAtomic(path, new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(value, JsonOptions)));

    private static void WriteBytesAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".afterthemed-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path))
            {
                try { File.Replace(temporary, path, null, true); }
                catch (IOException) { File.Move(temporary, path, true); }
                catch (PlatformNotSupportedException) { File.Move(temporary, path, true); }
            }
            else File.Move(temporary, path, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed record DecodedText(string Text, Encoding Encoding)
    {
        internal byte[] Encode(string value)
        {
            var content = Encoding.GetBytes(value);
            var preamble = Encoding.GetPreamble();
            if (preamble.Length == 0) return content;
            var result = new byte[preamble.Length + content.Length];
            preamble.CopyTo(result, 0);
            content.CopyTo(result, preamble.Length);
            return result;
        }
    }

    private sealed class PanelBackupManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTimeOffset? LastAppliedAtUtc { get; set; }
        public DateTimeOffset? LastRestoredAtUtc { get; set; }
        public List<PanelBackupEntry> Files { get; set; } = [];
    }

    private sealed class PanelBackupEntry
    {
        public string TargetPath { get; set; } = string.Empty;
        public string ExtensionName { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public string OriginalSha256 { get; set; } = string.Empty;
        public string PatchedSha256 { get; set; } = string.Empty;
        public DateTimeOffset PatchedAtUtc { get; set; }
        public int ColorReplacements { get; set; }
    }

    private sealed record PaletteProfile(bool SourceIsDark, double MinimumSurface, double MaximumSurface)
    {
        internal static PaletteProfile Create(string text)
        {
            var surfaces = new List<double>();
            foreach (Match declaration in DeclarationPattern.Matches(text))
            {
                var property = declaration.Groups["property"].Value;
                if (!property.Contains("background", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (Match token in ColorTokenPattern.Matches(declaration.Groups["value"].Value))
                    if (TryParseColor(token.Value, out var color) && color.A > 0) surfaces.Add(Luminance(color));
            }
            if (surfaces.Count == 0) surfaces.Add(.2);
            var median = surfaces.Order().ElementAt(surfaces.Count / 2);
            return new PaletteProfile(median < .5, surfaces.Min(), surfaces.Max());
        }

        internal Color MapSurface(double luminance, ThemeSettings target)
        {
            var range = Math.Max(.08, MaximumSurface - MinimumSurface);
            var normalized = Math.Clamp((luminance - MinimumSurface) / range, 0, 1);
            if (SourceIsDark)
                return normalized < .34 ? target.Background : normalized < .68 ? target.Panel : target.Raised;
            return normalized > .66 ? target.Background : normalized > .32 ? target.Panel : target.Raised;
        }
    }
}
