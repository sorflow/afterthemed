using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DvauiThemeEditor;

internal sealed record LegacyAeThemeCompanion(string InputPath, string TargetPath, string Sha256);

internal static class LegacyAeThemePatcher
{
    private static readonly string[] RequiredResourceNames =
        ["AECOLORTHEMES", "DVACOLORTHEMESV2", "DVACOLORTHEMESV4", "DVACOLORTHEMESV5"];

    private static readonly Regex KeyFrame = new(
        @"<KeyFrame\b(?<attributes>[^>]*?)/\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex Attribute = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_-]*)\s*=\s*(?<quote>[""'])(?<value>.*?)(?:\k<quote>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex XmlComment = new(
        @"<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex InterTagWhitespace = new(
        @">\s+<",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XmlWhitespace = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static LegacyAeThemeCompanion? GenerateForDvaui(
        string dvauiTargetPath,
        string originalsRoot,
        string outputPath,
        ThemeSettings settings)
    {
        if (!RequiresCompanion(dvauiTargetPath)) return null;

        var targetPath = CompanionTarget(dvauiTargetPath);
        if (!File.Exists(targetPath))
            throw new FileNotFoundException(
                "After Effects 2020 stores its native color themes in AfterFXLib.dll, but that companion file was not found.",
                targetPath);

        var originalPath = OriginalDllStore.CaptureIfMissing(targetPath, originalsRoot, out _);
        var hash = Generate(originalPath, outputPath, settings);
        return new LegacyAeThemeCompanion(outputPath, targetPath, hash);
    }

    internal static LegacyAeThemeCompanion? CreateRestoreForDvaui(
        string dvauiTargetPath,
        string originalsRoot,
        string outputPath)
    {
        if (!RequiresCompanion(dvauiTargetPath)) return null;
        var targetPath = CompanionTarget(dvauiTargetPath);
        var restorePath = OriginalDllStore.CreateRestoreDll(targetPath, originalsRoot, outputPath);
        return new LegacyAeThemeCompanion(restorePath, targetPath, OriginalDllStore.Sha256(restorePath));
    }

    internal static bool RequiresCompanion(string dvauiPath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(dvauiPath);
            return info.FileMajorPart == 14;
        }
        catch
        {
            return false;
        }
    }

    internal static string Generate(string sourcePath, string outputPath, ThemeSettings settings)
    {
        var data = File.ReadAllBytes(sourcePath);
        var pe = new DvauiPeImage(data);
        var resources = pe.Resources()
            .Where(resource => string.Equals(resource.Type, "XML", StringComparison.OrdinalIgnoreCase) &&
                               RequiredResourceNames.Contains(resource.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(resource => resource.Offset)
            .ToArray();

        var missing = RequiredResourceNames
            .Where(name => resources.All(resource => !string.Equals(resource.Name, name,
                StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException(
                $"The After Effects 2020 native theme resources are incomplete ({string.Join(", ", missing)} missing). No changes were made.");

        var totalChanged = 0;
        foreach (var resource in resources)
        {
            var xml = Encoding.UTF8.GetString(data, resource.Offset, resource.Size);
            if (!xml.Contains("<ThemeColors", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The {resource.Name} resource is not a supported color theme. No changes were made.");

            var rewritten = RewriteThemeXml(xml, settings, out var changed);
            if (changed < 8)
                throw new InvalidDataException(
                    $"The {resource.Name} resource did not expose enough semantic UI colors ({changed} found). No changes were made.");
            var replacement = Encoding.UTF8.GetBytes(rewritten);
            if (replacement.Length > resource.Size)
                replacement = Encoding.UTF8.GetBytes(InterTagWhitespace.Replace(rewritten, "><"));
            if (replacement.Length > resource.Size)
                throw new InvalidDataException(
                    $"The themed {resource.Name} resource grew by {replacement.Length - resource.Size:N0} bytes and cannot be written safely. No changes were made.");

            data.AsSpan(resource.Offset, resource.Size).Fill((byte)' ');
            replacement.CopyTo(data, resource.Offset);
            totalChanged += changed;
        }

        if (totalChanged < 100)
            throw new InvalidDataException(
                $"Only {totalChanged} native After Effects color entries were mapped; the companion DLL was not written.");

        var fullOutput = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        var temporaryPath = fullOutput + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, data);
            if (new FileInfo(temporaryPath).Length != new FileInfo(sourcePath).Length)
                throw new IOException("The generated AfterFXLib.dll changed size.");
            _ = new DvauiPeImage(File.ReadAllBytes(temporaryPath));
            File.Move(temporaryPath, fullOutput, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return OriginalDllStore.Sha256(fullOutput);
    }

    internal static string RewriteThemeXml(string xml, ThemeSettings settings, out int changed)
    {
        var replacements = 0;
        var rewritten = KeyFrame.Replace(xml, match =>
        {
            var attributes = ParseAttributes(match.Groups["attributes"].Value);
            var name = attributes.FirstOrDefault(attribute =>
                string.Equals(attribute.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            var target = TargetFor(name, settings);
            if (target is null) return match.Value;

            var kept = attributes
                .Where(attribute => attribute.Name is not "h" and not "s" and not "v")
                .Select(attribute => attribute with
                {
                    Value = NormalizeAlpha(attribute, name, settings)
                })
                .ToList();
            var (h, s, v) = ToHsv(target.Value);
            kept.Add(new ThemeAttribute("h", Format(h)));
            kept.Add(new ThemeAttribute("s", Format(s)));
            kept.Add(new ThemeAttribute("v", Format(v)));
            replacements++;
            return $"<KeyFrame {string.Join(" ", kept.Select(attribute =>
                $"{attribute.Name}=\"{attribute.Value}\""))} />";
        });
        changed = replacements;
        // PE resources have fixed extents. Comments and formatting whitespace are
        // semantically inert, so reclaim them before adding explicit HSV values.
        rewritten = XmlComment.Replace(rewritten, string.Empty);
        rewritten = InterTagWhitespace.Replace(rewritten, "><");
        rewritten = XmlWhitespace.Replace(rewritten, " ");
        return rewritten.TrimEnd();
    }

    private static string NormalizeAlpha(ThemeAttribute attribute, string key, ThemeSettings settings)
    {
        if (!string.Equals(attribute.Name, "a", StringComparison.OrdinalIgnoreCase) ||
            settings.ForegroundAlphaFloor <= 0 || !IsForegroundKey(key)) return attribute.Value;
        if (!double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha))
            return Format(settings.ForegroundAlphaFloor);
        return alpha > .04 && alpha < settings.ForegroundAlphaFloor
            ? Format(settings.ForegroundAlphaFloor)
            : attribute.Value;
    }

    private static List<ThemeAttribute> ParseAttributes(string text) => Attribute.Matches(text)
        .Select(match => new ThemeAttribute(
            match.Groups["name"].Value.ToLowerInvariant(),
            match.Groups["value"].Value))
        .ToList();

    private static Color? TargetFor(string rawKey, ThemeSettings settings)
    {
        var key = rawKey.Trim('&', ';').ToLowerInvariant();
        if (key.Length == 0 || IsDocumentColor(key)) return null;

        // State is not a semantic role. A selected/focused text key must remain
        // foreground even when its paired surface uses the primary accent.
        if (IsForegroundKey(key))
        {
            if (IsAccentStateKey(key)) return ContrastingForeground(settings.Primary, settings);
            if (key.Contains("error") || key.Contains("danger") || key.Contains("negative") ||
                key.Contains("alert") || key.Contains("invalid"))
                return settings.Danger;
            return key.Contains("disabled") || key.Contains("muted") || key.Contains("shadow")
                ? Mix(settings.Panel, settings.Text, .55)
                : settings.Text;
        }

        if (key.Contains("error") || key.Contains("danger") || key.Contains("negative") ||
            key.Contains("alert") || key.Contains("invalid"))
            return settings.Danger;
        if (key.Contains("success") || key.Contains("positive") || key.Contains("confirm"))
            return settings.Secondary;
        if (key.Contains("focus") || key.Contains("selected") || key.Contains("selection") ||
            key.Contains("highlight") || key.Contains("primary") || key.Contains("link") ||
            key.Contains("progressbar"))
            return settings.Primary;

        var grayIndex = GrayIndex(key);
        if (grayIndex is >= 1 and <= 3) return settings.Background;
        if (grayIndex is >= 4 and <= 7) return settings.Panel;
        if (grayIndex is >= 8 and <= 10) return settings.Text;

        if (key.Contains("applicationbackground") || key.Contains("appbackground") ||
            key.Contains("workspacebackground") || key.Contains("canvasbackground"))
            return settings.Background;
        if (key.Contains("contentbackground") || key.Contains("panel") || key.Contains("tab") ||
            key.Contains("well") || key.Contains("list") || key.Contains("menu") ||
            key.Contains("header") || key.Contains("track") || key.Contains("divider") ||
            key.Contains("separator") || key.Contains("outline") || key.Contains("border") ||
            key.Contains("stroke") || key.Contains("line") || key.Contains("shadow"))
            return settings.Panel;
        if (key.Contains("background")) return settings.Background;
        if (key.Contains("button") || key.Contains("control") || key.Contains("field") ||
            key.Contains("input") || key.Contains("widget") || key.Contains("scroll") ||
            key.Contains("slider") || key.Contains("popup") || key.Contains("swatch") ||
            key.Contains("fill"))
            return settings.Raised;
        return null;
    }

    private static bool IsForegroundKey(string key)
    {
        if (key.Contains("background") || key.Contains("fill") || key.Contains("gradient") ||
            key.Contains("stroke") || key.Contains("outline") || key.Contains("border"))
            return false;
        return key.Contains("text") || key.Contains("foreground") || key.Contains("font") ||
               key.Contains("glyph") || key.Contains("icon") || key.Contains("caret") ||
               key.Contains("face") || key.Contains("label");
    }

    private static bool IsAccentStateKey(string key) =>
        key.Contains("focus") || key.Contains("selected") || key.Contains("selection") ||
        key.Contains("highlight");

    private static bool IsDocumentColor(string key) =>
        key.Contains("labelcolor") || key.Contains("channel") || key.Contains("mask") ||
        key.Contains("motionpath") || key.Contains("keyframe") || key.Contains("gizmo") ||
        key.Contains("waveform") || key.Contains("audiometer") || key.Contains("safezone");

    private static int GrayIndex(string key)
    {
        var match = Regex.Match(key, @"gray_(?<index>\d{1,2})(?:\D|$)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["index"].Value, out var value) ? value : 0;
    }

    private static Color Mix(Color first, Color second, double amount) => Color.FromArgb(
        (int)Math.Round(first.R + (second.R - first.R) * amount),
        (int)Math.Round(first.G + (second.G - first.G) * amount),
        (int)Math.Round(first.B + (second.B - first.B) * amount));

    private static Color ContrastingForeground(Color surface, ThemeSettings settings) =>
        ContrastRatio(surface, settings.Background) >= ContrastRatio(surface, settings.Text)
            ? settings.Background
            : settings.Text;

    private static double ContrastRatio(Color first, Color second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + .05) /
               (Math.Min(firstLuminance, secondLuminance) + .05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }

        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }

    private static (double H, double S, double V) ToHsv(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var maximum = Math.Max(r, Math.Max(g, b));
        var minimum = Math.Min(r, Math.Min(g, b));
        var delta = maximum - minimum;
        var saturation = maximum == 0 ? 0 : delta / maximum;
        double hue;
        if (delta == 0) hue = 0;
        else if (maximum == r) hue = 60 * (((g - b) / delta) % 6);
        else if (maximum == g) hue = 60 * (((b - r) / delta) + 2);
        else hue = 60 * (((r - g) / delta) + 4);
        if (hue < 0) hue += 360;
        return (hue, saturation, maximum);
    }

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string CompanionTarget(string dvauiPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dvauiPath))!, "AfterFXLib.dll");

    private sealed record ThemeAttribute(string Name, string Value);
}
