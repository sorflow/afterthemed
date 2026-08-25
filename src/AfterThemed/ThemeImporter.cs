using System.Text.RegularExpressions;
using System.Text.Json;

namespace DvauiThemeEditor;

public sealed record ImportedTheme(string Name, IReadOnlyList<Color> Colors, ThemeSettings Suggested);

public static partial class ThemeImporter
{
    public static ImportedTheme Load(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".theme" or ".css" or ".json" or ".xml"))
            throw new NotSupportedException("Supported theme files: .theme, .css, .json, and .xml.");
        var text = File.ReadAllText(path);
        var colors = ExtractColors(text, extension is ".theme" or ".xml" or ".json", extension == ".theme");
        if (colors.Count < 2) throw new InvalidDataException("The theme file did not contain enough recognizable colors. Use #RGB/#RRGGBB, rgb(...), or numeric RGB triplets.");
        var name = Path.GetFileNameWithoutExtension(path);
        var suggested = extension == ".json" ? ReadExplicitRoles(text) ?? Suggest(name, colors) : Suggest(name, colors);
        return new ImportedTheme(name, colors, suggested);
    }

    public static List<Color> ExtractColors(string text, bool allowBareTriplets = false, bool allowWindowsArgb = false)
    {
        var found = new List<(int Index, Color Color)>();
        foreach (Match match in HexColorRegex().Matches(text))
            if (TryHex(match.Groups[1].Value, out var color)) found.Add((match.Index, color));
        foreach (Match match in RgbColorRegex().Matches(text))
            if (TryRgb(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, out var color)) found.Add((match.Index, color));
        foreach (Match match in HslColorRegex().Matches(text))
            if (TryHsl(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, out var color)) found.Add((match.Index, color));
        if (allowBareTriplets)
            foreach (Match match in BareTripletRegex().Matches(text))
                if (TryRgb(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, out var color)) found.Add((match.Index, color));
        if (allowWindowsArgb)
            foreach (Match match in WindowsArgbRegex().Matches(text))
                if (TryHex(match.Groups[1].Value[2..], out var color)) found.Add((match.Index, color));
        var result = new List<Color>();
        var seen = new HashSet<int>();
        foreach (var item in found.OrderBy(x => x.Index)) if (seen.Add(item.Color.ToArgb())) result.Add(item.Color);
        return result;
    }

    private static ThemeSettings Suggest(string name, IReadOnlyList<Color> colors)
    {
        var neutrals = colors.Where(c => Saturation(c) < .22f).OrderBy(Luminance).ToList();
        var ordered = (neutrals.Count >= 3 ? neutrals : colors.OrderBy(Luminance).ToList());
        var average = ordered.Average(c => Luminance(c));
        var light = name.Contains("light", StringComparison.OrdinalIgnoreCase) || average > .58f;
        var background = light ? ordered[^1] : ordered[0];
        var text = light ? ordered[0] : ordered[^1];
        var panel = ordered[Math.Clamp(light ? ordered.Count - 2 : 1, 0, ordered.Count - 1)];
        var raised = ordered[Math.Clamp(light ? ordered.Count - 3 : 2, 0, ordered.Count - 1)];

        var accents = colors.Where(c => Saturation(c) >= .28f).ToList();
        var primary = accents.FirstOrDefault(Color.Gold);
        var secondary = accents.FirstOrDefault(c => HueDistance(Hue(c), Hue(primary)) > 70, accents.Skip(1).FirstOrDefault(Color.Cyan));
        var danger = accents.OrderBy(c => HueDistance(Hue(c), 0)).FirstOrDefault(Color.IndianRed);
        return new ThemeSettings(background, panel, raised, text, primary, secondary, danger, .43f);
    }

    private static ThemeSettings? ReadExplicitRoles(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Object)
                return null;

            if (!ReadRole(roles, ["background"], out var background) ||
                !ReadRole(roles, ["panel", "surface"], out var panel) ||
                !ReadRole(roles, ["raised", "surfaceVariant"], out var raised) ||
                !ReadRole(roles, ["text", "onSurface"], out var foreground) ||
                !ReadRole(roles, ["primary"], out var primary) ||
                !ReadRole(roles, ["secondary"], out var secondary) ||
                !ReadRole(roles, ["danger", "error"], out var danger))
                return null;

            var cutoff = .43f;
            var alphaFloor = 0f;
            if (document.RootElement.TryGetProperty("mapping", out var mapping) && mapping.ValueKind == JsonValueKind.Object)
            {
                if (mapping.TryGetProperty("textCutoff", out var cutoffValue) && cutoffValue.TryGetSingle(out var parsedCutoff))
                    cutoff = Math.Clamp(parsedCutoff, .20f, .80f);
                if (mapping.TryGetProperty("foregroundAlphaFloor", out var alphaValue) && alphaValue.TryGetSingle(out var parsedAlpha))
                    alphaFloor = Math.Clamp(parsedAlpha, 0f, 1f);
            }

            return new ThemeSettings(background, panel, raised, foreground, primary, secondary, danger,
                cutoff, true, alphaFloor);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ReadRole(JsonElement roles, string[] names, out Color color)
    {
        foreach (var name in names)
            if (roles.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                TryHex((value.GetString() ?? string.Empty).Trim().TrimStart('#'), out color))
                return true;
        color = Color.Empty;
        return false;
    }

    private static bool TryHex(string value, out Color color)
    {
        color = Color.Empty;
        if (value.Length is 3 or 4) value = string.Concat(value.Take(3).Select(c => $"{c}{c}"));
        if (value.Length == 8) value = value[..6];
        if (value.Length != 6 || !int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return false;
        color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); return true;
    }
    private static bool TryRgb(string rs, string gs, string bs, out Color color)
    {
        color = Color.Empty;
        if (!int.TryParse(rs, out var r) || !int.TryParse(gs, out var g) || !int.TryParse(bs, out var b) || r > 255 || g > 255 || b > 255) return false;
        color = Color.FromArgb(r, g, b); return true;
    }
    private static bool TryHsl(string hs, string ss, string ls, out Color color)
    {
        color = Color.Empty;
        if (!float.TryParse(hs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) ||
            !float.TryParse(ss, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ||
            !float.TryParse(ls, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var l)) return false;
        h = ((h % 360) + 360) % 360; s = Math.Clamp(s / 100, 0, 1); l = Math.Clamp(l / 100, 0, 1);
        var c = (1 - Math.Abs(2 * l - 1)) * s; var x = c * (1 - Math.Abs((h / 60) % 2 - 1)); var m = l - c / 2;
        var (r, g, b) = h switch { < 60 => (c, x, 0f), < 120 => (x, c, 0f), < 180 => (0f, c, x), < 240 => (0f, x, c), < 300 => (x, 0f, c), _ => (c, 0f, x) };
        color = Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255)); return true;
    }
    private static float Luminance(Color c) => (.2126f * c.R + .7152f * c.G + .0722f * c.B) / 255f;
    private static float Saturation(Color c) { var max = Math.Max(c.R, Math.Max(c.G, c.B)); var min = Math.Min(c.R, Math.Min(c.G, c.B)); return max == 0 ? 0 : (max - min) / (float)max; }
    private static float Hue(Color c) => c.GetHue();
    private static float HueDistance(float a, float b) { var d = Math.Abs(a - b) % 360; return Math.Min(d, 360 - d); }

    [GeneratedRegex(@"(?<![0-9A-Fa-f])#([0-9A-Fa-f]{3,4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})(?![0-9A-Fa-f])")]
    private static partial Regex HexColorRegex();
    [GeneratedRegex(@"rgba?\s*\(\s*(\d{1,3})\s*[, ]\s*(\d{1,3})\s*[, ]\s*(\d{1,3})", RegexOptions.IgnoreCase)]
    private static partial Regex RgbColorRegex();
    [GeneratedRegex(@"hsla?\s*\(\s*(-?\d+(?:\.\d+)?)\s*[, ]\s*(\d+(?:\.\d+)?)%\s*[, ]\s*(\d+(?:\.\d+)?)%", RegexOptions.IgnoreCase)]
    private static partial Regex HslColorRegex();
    [GeneratedRegex(@"(?im)^\s*(?:[\w.\""'-]+\s*=|[\w.\""'-]+\s*:)\s*[\[(]?\s*(\d{1,3})\s*[, ]\s*(\d{1,3})\s*[, ]\s*(\d{1,3})(?:\s|[\])},]|$)")]
    private static partial Regex BareTripletRegex();
    [GeneratedRegex(@"(?i)0x([0-9a-f]{8})(?![0-9a-f])")]
    private static partial Regex WindowsArgbRegex();
}
