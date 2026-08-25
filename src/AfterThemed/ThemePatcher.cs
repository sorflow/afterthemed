using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DvauiThemeEditor;

public sealed record ThemeSettings(
    Color Background, Color Panel, Color Raised, Color Text,
    Color Primary, Color Secondary, Color Danger, float TextCutoff,
    bool ExactAccents = false, float ForegroundAlphaFloor = 0f)
{
    public static ThemeSettings Cyberpunk => new(
        ColorTranslator.FromHtml("#6F0623"), ColorTranslator.FromHtml("#6F0623"), ColorTranslator.FromHtml("#B10A3A"),
        ColorTranslator.FromHtml("#FCEE0A"), ColorTranslator.FromHtml("#FCEE0A"), ColorTranslator.FromHtml("#00F0FF"),
        ColorTranslator.FromHtml("#FF003C"), .43f);
    public static ThemeSettings GruvboxDark => new(
        ColorTranslator.FromHtml("#1D2021"), ColorTranslator.FromHtml("#282828"), ColorTranslator.FromHtml("#504945"),
        ColorTranslator.FromHtml("#EBDBB2"), ColorTranslator.FromHtml("#FABD2F"), ColorTranslator.FromHtml("#8EC07C"),
        ColorTranslator.FromHtml("#FB4934"), .43f);
    public static ThemeSettings GruvboxLight => new(
        ColorTranslator.FromHtml("#FBF1C7"), ColorTranslator.FromHtml("#F2E5BC"), ColorTranslator.FromHtml("#D5C4A1"),
        ColorTranslator.FromHtml("#3C3836"), ColorTranslator.FromHtml("#B57614"), ColorTranslator.FromHtml("#427B58"),
        ColorTranslator.FromHtml("#9D0006"), .43f);
    public static ThemeSettings MaterialLavender => new(
        ColorTranslator.FromHtml("#FFFAFA"), ColorTranslator.FromHtml("#FFFAFA"), ColorTranslator.FromHtml("#F0EAF7"),
        ColorTranslator.FromHtml("#301A49"), ColorTranslator.FromHtml("#9F82D9"), ColorTranslator.FromHtml("#6F559F"),
        ColorTranslator.FromHtml("#BA1A1A"), .43f, true);
    public static ThemeSettings MaterialLavenderRich => new(
        ColorTranslator.FromHtml("#F1EAF7"), ColorTranslator.FromHtml("#E6DAF1"), ColorTranslator.FromHtml("#CDBBE4"),
        ColorTranslator.FromHtml("#2F1946"), ColorTranslator.FromHtml("#9F82D9"), ColorTranslator.FromHtml("#6F559F"),
        ColorTranslator.FromHtml("#B3261E"), .70f, true, .45f);
    public static ThemeSettings HatsuneMikuAccessible => new(
        ColorTranslator.FromHtml("#1F2527"), ColorTranslator.FromHtml("#242F31"), ColorTranslator.FromHtml("#29383A"),
        ColorTranslator.FromHtml("#BEC8D1"), ColorTranslator.FromHtml("#86CECB"), ColorTranslator.FromHtml("#59C9CC"),
        ColorTranslator.FromHtml("#FF9ACC"), .62f, true, .55f);
}

public sealed record TextReplacement(string Find, string Replace);

public static class ThemePatcher
{
    private enum ThemeRole { Background, Panel, Raised, Text, Primary, Secondary, Danger }

    private sealed record DetectedSourceTheme(string Name, ThemeSettings Settings);

    private sealed record DvauiPatchPlan(
        string Name,
        (int Offset, int Length)[] FloatTables,
        PeResource[] JsonResources,
        int[] LegacyColors);

    private static readonly (int RelativeOffset, int Size, string AdobeValue, string SfValue)[] ModernFontSlots =
    {
        (0, 24, "AdobeClean-Regular", "SFProDisp-Regular"), (24, 16, "Adobe Clean", "SF Pro Disp"),
        (40, 16, "AdobeClean-Bold", "SFProDisp-Bold"), (56, 16, "AdobeClean-It", "SFProDisp-It"),
        (72, 24, "AdobeClean-Italic", "SFProDisp-It"), (96, 24, "AdobeClean-BoldIt", "SFProDisp-BdIt"),
        (120, 24, "AdobeClean-BoldItalic", "SFProDisp-BdIt")
    };

    private static readonly (int RelativeOffset, int Size, string AdobeValue, string SfValue)[] LegacyFontSlots =
    {
        (0, 16, "Adobe Clean UX", "SF Pro Display"), (16, 24, "AdobeCleanUX-Regular", "SFProDisp-Regular"),
        (40, 24, "AdobeCleanUX-Bold", "SFProDisp-Bold"), (64, 24, "AdobeCleanUX-Italic", "SFProDisp-It"),
        (88, 24, "AdobeCleanUX-BoldItalic", "SFProDisp-BdIt")
    };

    private static readonly (int RelativeOffset, int Size, string AdobeValue, string SfValue)[] TransitionalFontSlots =
    {
        (0, 24, "AdobeClean-Regular", "SFProDisp-Regular"), (24, 16, "AdobeClean-Bold", "SFProDisp-Bold"),
        (40, 16, "AdobeClean-It", "SFProDisp-It"), (56, 24, "AdobeClean-Italic", "SFProDisp-It"),
        (80, 24, "AdobeClean-BoldIt", "SFProDisp-BdIt"), (104, 24, "AdobeClean-BoldItalic", "SFProDisp-BdIt")
    };

    private static readonly (int RelativeOffset, int Size, string AdobeValue, string SfValue)[] EarlyModernFontSlots =
    {
        (0, 16, "Adobe Clean", "SF Pro Disp"), (16, 24, "AdobeClean-Regular", "SFProDisp-Regular"),
        (40, 16, "AdobeClean-Bold", "SFProDisp-Bold"), (56, 24, "AdobeClean-Italic", "SFProDisp-It"),
        (80, 24, "AdobeClean-BoldItalic", "SFProDisp-BdIt")
    };

    private static readonly Regex CssColor = new(
        @"^\s*(rgb|rgba)\(\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)(?:\s*,\s*(\d+(?:\.\d+)?))?\s*\)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly (string Name, ThemeSettings Settings)[] KnownSourceThemes =
    {
        ("Cyberpunk", ThemeSettings.Cyberpunk),
        ("Gruvbox Dark", ThemeSettings.GruvboxDark),
        ("Gruvbox Light", ThemeSettings.GruvboxLight),
        ("Material Lavender", ThemeSettings.MaterialLavender),
        ("Material Lavender Rich", ThemeSettings.MaterialLavenderRich),
        ("Hatsune Miku Accessible", ThemeSettings.HatsuneMikuAccessible)
    };

    public static string Generate(string source, string output, ThemeSettings settings, bool useSfDisplay,
        IReadOnlyList<TextReplacement>? textReplacements = null) =>
        Generate(source, output, settings, useSfDisplay ? "SF Pro Display" : null, textReplacements);

    public static string Generate(string source, string output, ThemeSettings settings, string? fontFamily,
        IReadOnlyList<TextReplacement>? textReplacements = null)
    {
        var data = File.ReadAllBytes(source);
        var plan = ResolvePlan(data, source);
        var sourceTheme = DetectSourceTheme(data, plan);
        foreach (var table in plan.FloatTables)
            for (var i = 0; i < table.Length; i++)
            {
                var p = table.Offset + i * 16;
                var rf = BitConverter.ToSingle(data, p);
                var gf = BitConverter.ToSingle(data, p + 4);
                var bf = BitConverter.ToSingle(data, p + 8);
                var af = BitConverter.ToSingle(data, p + 12);
                var replacement = MapColor(rf, gf, bf, settings, sourceTheme);
                WriteFloat(data, p, replacement.R / 255f);
                WriteFloat(data, p + 4, replacement.G / 255f);
                WriteFloat(data, p + 8, replacement.B / 255f);
                if (settings.ForegroundAlphaFloor > 0 && af > .04f && af < settings.ForegroundAlphaFloor &&
                    IsNeutralForeground(rf, gf, bf, settings, sourceTheme))
                    WriteFloat(data, p + 12, settings.ForegroundAlphaFloor);
            }
        foreach (var offset in plan.LegacyColors)
        {
            var replacement = MapColor(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8), settings, sourceTheme);
            WriteFloat(data, offset, replacement.R / 255f);
            WriteFloat(data, offset + 4, replacement.G / 255f);
            WriteFloat(data, offset + 8, replacement.B / 255f);
        }
        ApplyJsonResources(data, plan.JsonResources, settings, sourceTheme);
        if (!string.IsNullOrWhiteSpace(fontFamily)) ApplyFontFamily(data, plan.Name, fontFamily.Trim());
        if (textReplacements is { Count: > 0 }) ApplyTextReplacements(data, textReplacements);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllBytes(output, data);
        return Sha256(output);
    }

    public static string Inventory(string source)
    {
        var data = File.ReadAllBytes(source);
        var plan = ResolvePlan(data, source);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in plan.FloatTables)
            for (var i = 0; i < table.Length; i++)
            {
                var p = table.Offset + i * 16;
                var key = $"#{ClampByte(BitConverter.ToSingle(data, p)):X2}{ClampByte(BitConverter.ToSingle(data, p + 4)):X2}{ClampByte(BitConverter.ToSingle(data, p + 8)):X2}{ClampByte(BitConverter.ToSingle(data, p + 12)):X2}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        foreach (var offset in plan.LegacyColors)
        {
            var key = $"#{ClampByte(BitConverter.ToSingle(data, offset)):X2}{ClampByte(BitConverter.ToSingle(data, offset + 4)):X2}{ClampByte(BitConverter.ToSingle(data, offset + 8)):X2}{ClampByte(BitConverter.ToSingle(data, offset + 12)):X2}";
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        InventoryJsonResources(data, plan.JsonResources, counts);
        var sb = new StringBuilder($"Source: {source}\r\nLayout: {plan.Name}\r\nSHA-256: {Sha256(source)}\r\nGenerated: {DateTime.Now:O}\r\nColor (RGBA)\tOccurrences\r\n");
        foreach (var item in counts.OrderByDescending(x => x.Value).ThenBy(x => x.Key)) sb.AppendLine($"{item.Key}\t{item.Value}");
        return sb.ToString();
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static DvauiPatchPlan ResolvePlan(byte[] data, string source)
    {
        var pe = new DvauiPeImage(data);
        var version = "unknown";
        var major = 0;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(source);
            version = info.ProductVersion ?? info.FileVersion ?? version;
            major = Math.Max(0, info.FileMajorPart);
        }
        catch
        {
            // Version metadata is diagnostic only; structural validation remains authoritative.
        }
        if (major is < 11 or > 26)
            throw new InvalidDataException($"This Adobe DVA version is outside the supported CC 2018–2026 range (detected version: {version}). No changes were made.");

        var floatTables = FindSpectrumFloatTables(data, pe);
        var jsonResources = FindSpectrumJsonResources(data, pe);
        var legacyColors = floatTables.Length == 0 && jsonResources.Length == 0
            ? FindLegacyThemeColors(data, pe)
            : [];
        if (floatTables.Length == 0 && jsonResources.Length == 0 && legacyColors.Length == 0)
            throw new InvalidDataException($"A safe DVAUI theme structure was not found for Adobe DVA {version}. No changes were made.");

        var engines = new List<string>();
        if (floatTables.Length > 0) engines.Add($"Spectrum RGBA ({floatTables.Sum(x => x.Length):N0} colors)");
        if (jsonResources.Length > 0) engines.Add($"Spectrum DNA JSON ({jsonResources.Length} resources)");
        if (legacyColors.Length > 0) engines.Add("legacy DVA base theme");
        return new DvauiPatchPlan($"Adobe DVA {version} · {string.Join(" + ", engines)}", floatTables, jsonResources, legacyColors);
    }

    private static (int Offset, int Length)[] FindSpectrumFloatTables(byte[] data, DvauiPeImage pe)
    {
        var section = pe.Sections.FirstOrDefault(x => string.Equals(x.Name, ".rdata", StringComparison.Ordinal));
        if (section is null || section.Size < 16) return [];
        var candidates = new List<(int Offset, int Length)>();
        for (var phase = 0; phase < 16; phase += 4)
        {
            var first = section.Offset + ((phase - section.Offset) & 15);
            var runStart = -1;
            var runLength = 0;
            var alphaVisible = 0;
            var binaryAlpha = 0;
            var nonZeroRgb = 0;
            var uniqueRgb = new HashSet<int>();
            var uniqueAlpha = new HashSet<int>();
            for (var offset = first; offset <= section.Offset + section.Size - 16; offset += 16)
            {
                var r = BitConverter.ToSingle(data, offset);
                var g = BitConverter.ToSingle(data, offset + 4);
                var b = BitConverter.ToSingle(data, offset + 8);
                var a = BitConverter.ToSingle(data, offset + 12);
                var valid = IsUnitFloat(r) && IsUnitFloat(g) && IsUnitFloat(b) && IsUnitFloat(a);
                if (valid)
                {
                    if (runStart < 0) runStart = offset;
                    runLength++;
                    if (a > .01f) alphaVisible++;
                    if (a <= .001f || Math.Abs(a - 1f) <= .001f) binaryAlpha++;
                    if (r > .01f || g > .01f || b > .01f) nonZeroRgb++;
                    uniqueRgb.Add((ClampByte(r) << 16) | (ClampByte(g) << 8) | ClampByte(b));
                    uniqueAlpha.Add(BitConverter.SingleToInt32Bits(a));
                    continue;
                }

                AddFloatRun(candidates, runStart, runLength, alphaVisible, binaryAlpha, nonZeroRgb, uniqueRgb.Count, uniqueAlpha.Count);
                runStart = -1;
                runLength = alphaVisible = binaryAlpha = nonZeroRgb = 0;
                uniqueRgb.Clear();
                uniqueAlpha.Clear();
            }
            AddFloatRun(candidates, runStart, runLength, alphaVisible, binaryAlpha, nonZeroRgb, uniqueRgb.Count, uniqueAlpha.Count);
        }

        var selected = new List<(int Offset, int Length)>();
        foreach (var candidate in candidates.OrderBy(x => x.Offset).ThenByDescending(x => x.Length))
        {
            if (selected.Any(x => candidate.Offset < x.Offset + x.Length * 16 && x.Offset < candidate.Offset + candidate.Length * 16))
                continue;
            selected.Add(candidate);
        }
        return selected.ToArray();
    }

    private static void AddFloatRun(List<(int Offset, int Length)> result, int start, int length,
        int alphaVisible, int binaryAlpha, int nonZeroRgb, int uniqueRgb, int uniqueAlpha)
    {
        if (start < 0 || length < 300 || length > 10_000) return;
        if (alphaVisible < length * .95 || binaryAlpha < length * .85 || nonZeroRgb < length * .65 ||
            uniqueRgb < 48 || uniqueAlpha > 64) return;
        result.Add((start, length));
    }

    private static bool IsUnitFloat(float value) => float.IsFinite(value) && value >= 0 && value <= 1.001f;

    private static PeResource[] FindSpectrumJsonResources(byte[] data, DvauiPeImage pe)
    {
        var rgb = Encoding.ASCII.GetBytes("rgb(");
        return pe.Resources()
            .Where(resource => string.Equals(resource.Type, "JSON", StringComparison.OrdinalIgnoreCase))
            .Where(resource => IsSpectrumJsonResourceName(resource.Name))
            .Where(resource => CountSequence(data, resource.Offset, resource.Size, rgb) >= 8)
            .OrderBy(resource => resource.Offset)
            .ToArray();
    }

    internal static bool IsSpectrumJsonResourceName(string name) =>
        name.Contains("DNA-VARS", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("DROVER-VARS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "DROVER", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "VARIABLES", StringComparison.OrdinalIgnoreCase);

    private static int[] FindLegacyThemeColors(byte[] data, DvauiPeImage pe)
    {
        if (!pe.TryFindExport(name => name.Contains("?InitializeColors@Theme@ui@dvaui@@", StringComparison.Ordinal), out var functionRva))
            return [];
        var functionOffset = pe.RvaToOffset(functionRva);
        var colors = new HashSet<int>();
        var available = Math.Min(1024, data.Length - functionOffset - 7);
        for (var i = 0; i < available; i++)
        {
            if (data[functionOffset + i] != 0x0F ||
                data[functionOffset + i + 1] is not (0x10 or 0x28) ||
                data[functionOffset + i + 2] != 0x05) continue;
            var displacement = BitConverter.ToInt32(data, functionOffset + i + 3);
            var targetRva = (long)functionRva + i + 7 + displacement;
            if (targetRva is <= 0 or > uint.MaxValue) continue;
            int target;
            try { target = pe.RvaToOffset((uint)targetRva); }
            catch (InvalidDataException) { continue; }
            if (target < 0 || target > data.Length - 16) continue;
            var r = BitConverter.ToSingle(data, target);
            var g = BitConverter.ToSingle(data, target + 4);
            var b = BitConverter.ToSingle(data, target + 8);
            var a = BitConverter.ToSingle(data, target + 12);
            if (!IsUnitFloat(r) || !IsUnitFloat(g) || !IsUnitFloat(b) || a < .95f) continue;
            colors.Add(target);
        }
        return colors.Order().ToArray();
    }

    private static void ApplyJsonResources(byte[] data, IReadOnlyList<PeResource> resources, ThemeSettings settings,
        DetectedSourceTheme? sourceTheme)
    {
        foreach (var resource in resources)
        {
            var json = Encoding.UTF8.GetString(data, resource.Offset, resource.Size);
            var node = JsonNode.Parse(json) ?? throw new InvalidDataException($"The embedded {resource.Name} theme resource is empty.");
            var changed = RewriteJsonColors(node, settings, sourceTheme);
            if (changed < 8) throw new InvalidDataException($"The embedded {resource.Name} resource did not contain a supported color table. No changes were made.");
            var replacement = Encoding.UTF8.GetBytes(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            if (replacement.Length > resource.Size)
                throw new InvalidDataException($"The customized {resource.Name} theme resource no longer fits safely inside the DLL. No changes were made.");
            data.AsSpan(resource.Offset, resource.Size).Fill((byte)' ');
            replacement.CopyTo(data, resource.Offset);
        }
    }

    private static int RewriteJsonColors(JsonNode node, ThemeSettings settings, DetectedSourceTheme? sourceTheme)
    {
        var changed = 0;
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                var child = obj[key];
                if (child is JsonValue value && value.TryGetValue<string>(out var text) &&
                    TryParseCssColor(text, out var r, out var g, out var b, out var alpha))
                {
                    var mapped = MapJsonColor(key, r / 255f, g / 255f, b / 255f, settings, sourceTheme);
                    obj[key] = alpha >= .999f
                        ? $"rgb({mapped.R}, {mapped.G}, {mapped.B})"
                        : $"rgba({mapped.R}, {mapped.G}, {mapped.B}, {alpha.ToString("0.###", CultureInfo.InvariantCulture)})";
                    changed++;
                }
                else if (child is not null)
                {
                    changed += RewriteJsonColors(child, settings, sourceTheme);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) changed += RewriteJsonColors(child, settings, sourceTheme);
        }
        return changed;
    }

    private static void InventoryJsonResources(byte[] data, IReadOnlyList<PeResource> resources, Dictionary<string, int> counts)
    {
        foreach (var resource in resources)
        {
            var node = JsonNode.Parse(Encoding.UTF8.GetString(data, resource.Offset, resource.Size));
            if (node is not null) CollectJsonColors(node, counts);
        }
    }

    private static void CollectJsonColors(JsonNode node, Dictionary<string, int> counts)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text) &&
            TryParseCssColor(text, out var r, out var g, out var b, out var alpha))
        {
            var key = $"#{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}{Clamp(alpha * 255):X2}";
            counts[key] = counts.GetValueOrDefault(key) + 1;
            return;
        }
        if (node is JsonObject obj)
        {
            foreach (var propertyValue in obj.Select(x => x.Value))
                if (propertyValue is not null) CollectJsonColors(propertyValue, counts);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                if (item is not null) CollectJsonColors(item, counts);
        }
    }

    private static bool TryParseCssColor(string text, out float r, out float g, out float b, out float alpha)
    {
        r = g = b = 0;
        alpha = 1;
        var match = CssColor.Match(text);
        if (!match.Success) return false;
        if (!float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ||
            !float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out g) ||
            !float.TryParse(match.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out b)) return false;
        if (match.Groups[5].Success &&
            !float.TryParse(match.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out alpha)) return false;
        return r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255 && alpha is >= 0 and <= 1;
    }

    private static int CountSequence(byte[] data, int offset, int size, byte[] sequence)
    {
        var count = 0;
        var end = offset + size - sequence.Length;
        for (var i = offset; i <= end; i++)
        {
            if (!data.AsSpan(i, sequence.Length).SequenceEqual(sequence)) continue;
            count++;
            i += sequence.Length - 1;
        }
        return count;
    }

    private static Color MapColor(float rf, float gf, float bf, ThemeSettings s,
        DetectedSourceTheme? sourceTheme = null)
    {
        var r = Math.Clamp(rf, 0, 1); var g = Math.Clamp(gf, 0, 1); var b = Math.Clamp(bf, 0, 1);
        if (sourceTheme is not null && TryMapKnownRole(r, g, b, sourceTheme.Settings, s, out var semantic))
            return semantic;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b));
        var lum = .2126f * r + .7152f * g + .0722f * b;
        var sat = max <= 0 ? 0 : (max - min) / max;
        if (sat < .10f)
        {
            if (lum >= s.TextCutoff) return s.Text;
            // Spectrum grays are discrete tokens (GetGrayColor / InitializeColorRange),
            // not a spatial ramp. Snap each shade onto the nearest solid role.
            var shade = Math.Clamp(lum / Math.Max(.05f, s.TextCutoff), 0, 1);
            if (shade < .28f) return s.Background;
            if (shade < .62f) return s.Panel;
            return s.Raised;
        }
        var hue = Hue(r, g, b);
        var target = hue < 25 || hue >= 285 ? s.Danger : hue < 80 ? s.Primary : hue < 190 ? s.Secondary : s.Primary;
        if (s.ExactAccents && sat >= .55f) return target;
        return Blend(s.Panel, target, Math.Clamp(.35f + sat * .65f, 0, 1), Math.Clamp(.65f + lum * .6f, .65f, 1.18f));
    }

    private static Color MapJsonColor(string key, float r, float g, float b, ThemeSettings target,
        DetectedSourceTheme? sourceTheme)
    {
        // DVA's small DROVER resource contains semantic tokens. Their names are
        // more authoritative than their original luminance—especially the light
        // table background, which must not become dark UI text.
        if (key.Contains("levelmeterskin", StringComparison.OrdinalIgnoreCase))
            return MapColor(r, g, b, target);
        if (key.Contains("picker-disabled", StringComparison.OrdinalIgnoreCase))
            return target.Text;
        if (key.Contains("table-cell-background", StringComparison.OrdinalIgnoreCase))
            return target.Raised;
        if (key.Contains("background-selected", StringComparison.OrdinalIgnoreCase))
            return target.Primary;
        if (key.Contains("scrollbar-thumb-over", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("scrollbar-thumb-down", StringComparison.OrdinalIgnoreCase))
            return target.Primary;
        if (key.Contains("scrollbar-thumb", StringComparison.OrdinalIgnoreCase))
            return target.Raised;
        return MapColor(r, g, b, target, sourceTheme);
    }

    private static bool IsNeutralForeground(float rf, float gf, float bf, ThemeSettings s,
        DetectedSourceTheme? sourceTheme = null)
    {
        var r = Math.Clamp(rf, 0, 1); var g = Math.Clamp(gf, 0, 1); var b = Math.Clamp(bf, 0, 1);
        if (sourceTheme is not null && MatchesRole(r, g, b, RoleColor(sourceTheme.Settings, ThemeRole.Text)))
            return true;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b));
        var sat = max <= 0 ? 0 : (max - min) / max;
        var lum = .2126f * r + .7152f * g + .0722f * b;
        return sat < .10f && lum >= s.TextCutoff;
    }

    private static DetectedSourceTheme? DetectSourceTheme(byte[] data, DvauiPatchPlan plan)
    {
        if (plan.FloatTables.Length == 0) return null;
        var counts = new Dictionary<int, int>();
        foreach (var table in plan.FloatTables)
            for (var i = 0; i < table.Length; i++)
            {
                var offset = table.Offset + i * 16;
                var rgb = (ClampByte(BitConverter.ToSingle(data, offset)) << 16) |
                          (ClampByte(BitConverter.ToSingle(data, offset + 4)) << 8) |
                          ClampByte(BitConverter.ToSingle(data, offset + 8));
                counts[rgb] = counts.GetValueOrDefault(rgb) + 1;
            }

        DetectedSourceTheme? best = null;
        var bestScore = 0;
        foreach (var (name, settings) in KnownSourceThemes)
        {
            var anchors = RolePairs(settings)
                .Select(pair => pair.Color.ToArgb() & 0xFFFFFF)
                .Distinct()
                .ToArray();
            var matchedAnchors = anchors.Count(anchor => counts.GetValueOrDefault(anchor) >= 3);
            var score = anchors.Sum(anchor => counts.GetValueOrDefault(anchor));
            if (matchedAnchors < 4 || score < 50 || score <= bestScore) continue;
            best = new DetectedSourceTheme(name, settings);
            bestScore = score;
        }
        return best;
    }

    private static bool TryMapKnownRole(float r, float g, float b, ThemeSettings source, ThemeSettings target,
        out Color mapped)
    {
        foreach (var (role, sourceColor) in RolePairs(source))
        {
            if (!MatchesRole(r, g, b, sourceColor)) continue;
            mapped = RoleColor(target, role);
            return true;
        }
        mapped = default;
        return false;
    }

    private static bool MatchesRole(float r, float g, float b, Color role) =>
        ClampByte(r) == role.R && ClampByte(g) == role.G && ClampByte(b) == role.B;

    private static (ThemeRole Role, Color Color)[] RolePairs(ThemeSettings settings) =>
    [
        (ThemeRole.Background, settings.Background),
        (ThemeRole.Panel, settings.Panel),
        (ThemeRole.Raised, settings.Raised),
        (ThemeRole.Text, settings.Text),
        (ThemeRole.Primary, settings.Primary),
        (ThemeRole.Secondary, settings.Secondary),
        (ThemeRole.Danger, settings.Danger)
    ];

    private static Color RoleColor(ThemeSettings settings, ThemeRole role) => role switch
    {
        ThemeRole.Background => settings.Background,
        ThemeRole.Panel => settings.Panel,
        ThemeRole.Raised => settings.Raised,
        ThemeRole.Text => settings.Text,
        ThemeRole.Primary => settings.Primary,
        ThemeRole.Secondary => settings.Secondary,
        ThemeRole.Danger => settings.Danger,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
    private static float Hue(float r, float g, float b)
    {
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var d = max - min;
        if (d == 0) return 0;
        var h = max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        return h < 0 ? h + 360 : h;
    }

    private static Color Blend(Color a, Color b, float t, float brightness) => Color.FromArgb(Clamp((a.R + (b.R - a.R) * t) * brightness), Clamp((a.G + (b.G - a.G) * t) * brightness), Clamp((a.B + (b.B - a.B) * t) * brightness));
    private static int Clamp(float v) => (int)Math.Clamp(MathF.Round(v), 0, 255);
    private static int ClampByte(float v) => Clamp(v * 255);
    private static void WriteFloat(byte[] bytes, int offset, float value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);

    private static void ApplySfDisplay(byte[] data, string layoutName)
    {
        var table = FindFontTable(data, ModernFontSlots) ?? FindFontTable(data, TransitionalFontSlots) ??
                    FindFontTable(data, EarlyModernFontSlots) ?? FindFontTable(data, LegacyFontSlots);
        if (table is null)
            throw new InvalidDataException($"The UI-font table does not match the supported {layoutName} structure. No changes were made.");
        var (baseOffset, slots) = table.Value;
        foreach (var slot in slots)
        {
            var offset = baseOffset + slot.RelativeOffset;
            if (offset + slot.Size > data.Length)
                throw new InvalidDataException($"The SF Display font table is outside the supported {layoutName} structure. No changes were made.");
            var existing = ReadAsciiSlot(data, offset, slot.Size);
            if (!string.Equals(existing, slot.AdobeValue, StringComparison.Ordinal) &&
                !string.Equals(existing, slot.SfValue, StringComparison.Ordinal))
                throw new InvalidDataException($"The UI-font table does not match the supported {layoutName} structure. No changes were made.");
            Array.Clear(data, offset, slot.Size);
            var text = Encoding.ASCII.GetBytes(slot.SfValue);
            Array.Copy(text, 0, data, offset, Math.Min(text.Length, slot.Size - 1));
        }
    }

    private static void ApplyFontFamily(byte[] data, string layoutName, string familyName)
    {
        // Preserve the proven aliases used by older builds. SF Pro Display's
        // canonical face names are too long for two of DVAUI's 16-byte slots.
        if (familyName.Equals("SF Pro Display", StringComparison.OrdinalIgnoreCase) ||
            familyName.Equals("SF Pro Disp", StringComparison.OrdinalIgnoreCase))
        {
            ApplySfDisplay(data, layoutName);
            return;
        }

        InstalledFontCatalog.ValidatePatchName(familyName);
        var table = FindFontTable(data, ModernFontSlots) ?? FindFontTable(data, TransitionalFontSlots) ??
                    FindFontTable(data, EarlyModernFontSlots) ?? FindFontTable(data, LegacyFontSlots);
        if (table is null)
            throw new InvalidDataException($"The UI-font table does not match the supported {layoutName} structure. No changes were made.");

        var text = Encoding.ASCII.GetBytes(familyName);
        var (baseOffset, slots) = table.Value;
        foreach (var slot in slots)
        {
            var offset = baseOffset + slot.RelativeOffset;
            if (offset < 0 || offset + slot.Size > data.Length || text.Length >= slot.Size)
                throw new InvalidDataException($"The selected font does not fit the supported {layoutName} font table. No changes were made.");
        }

        // The generic path deliberately writes one installed family name to all
        // face slots. DVAUI/Windows can synthesize bold and italic styling while
        // every lookup remains resolvable to the selected installed family.
        foreach (var slot in slots)
        {
            var offset = baseOffset + slot.RelativeOffset;
            Array.Clear(data, offset, slot.Size);
            Array.Copy(text, 0, data, offset, text.Length);
        }
    }

    private static (int BaseOffset, (int RelativeOffset, int Size, string AdobeValue, string SfValue)[] Slots)? FindFontTable(
        byte[] data, (int RelativeOffset, int Size, string AdobeValue, string SfValue)[] slots)
    {
        foreach (var signature in new[] { slots[0].AdobeValue, slots[0].SfValue })
        {
            var bytes = Encoding.ASCII.GetBytes(signature);
            for (var offset = 0; offset <= data.Length - bytes.Length; offset++)
            {
                if (!data.AsSpan(offset, bytes.Length).SequenceEqual(bytes)) continue;
                var matches = true;
                foreach (var slot in slots)
                {
                    var position = offset + slot.RelativeOffset;
                    if (position < 0 || position + slot.Size > data.Length)
                    {
                        matches = false;
                        break;
                    }
                    var existing = ReadAsciiSlot(data, position, slot.Size);
                    if (!string.Equals(existing, slot.AdobeValue, StringComparison.Ordinal) &&
                        !string.Equals(existing, slot.SfValue, StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches) return (offset, slots);
                offset += bytes.Length - 1;
            }
        }
        return null;
    }

    private static string ReadAsciiSlot(byte[] data, int offset, int size)
    {
        var length = 0;
        while (length < size && data[offset + length] != 0) length++;
        return Encoding.ASCII.GetString(data, offset, length);
    }

    private static void ApplyTextReplacements(byte[] data, IReadOnlyList<TextReplacement> replacements)
    {
        foreach (var replacement in replacements)
        {
            if (replacement.Find.Length < 3) throw new InvalidDataException("UI text search values must contain at least 3 characters.");
            var asciiFind = Encoding.ASCII.GetBytes(replacement.Find); var asciiReplace = Encoding.ASCII.GetBytes(replacement.Replace);
            var wideFind = Encoding.Unicode.GetBytes(replacement.Find); var wideReplace = Encoding.Unicode.GetBytes(replacement.Replace);
            if (asciiReplace.Length > asciiFind.Length || wideReplace.Length > wideFind.Length)
                throw new InvalidDataException($"Replacement text must not be longer than the original: {replacement.Find}");
            var count = ReplaceAll(data, asciiFind, asciiReplace) + ReplaceAll(data, wideFind, wideReplace);
            if (count == 0) throw new InvalidDataException($"UI text was not found in this DLL: {replacement.Find}");
        }
    }

    private static int ReplaceAll(byte[] data, byte[] find, byte[] replacement)
    {
        var count = 0;
        for (var i = 0; i <= data.Length - find.Length; i++)
        {
            if (!data.AsSpan(i, find.Length).SequenceEqual(find)) continue;
            Array.Clear(data, i, find.Length);
            replacement.CopyTo(data, i);
            count++; i += find.Length - 1;
        }
        return count;
    }
}
