using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DvauiThemeEditor;

/// <summary>A complete, portable theme: the .afterthemed file, share codes, history, and gallery entries.</summary>
internal sealed record ThemeDocument(string Name, ThemeSettings Settings)
{
    public string? Author { get; init; }
    public string? Font { get; init; }
    public string? TextReplacements { get; init; }
    public bool? ThemePanels { get; init; }
    /// <summary>A PNG data URL; only kept when it is small and well formed.</summary>
    public string? Thumbnail { get; init; }
}

internal static class ThemeDocuments
{
    internal const string Extension = ".afterthemed";
    private const string Format = "afterthemed";
    private const string SharePrefix = "AT1-";
    private const int MaxNameLength = 64;
    private const int MaxThumbnailLength = 256 * 1024;
    private const int MaxReplacementsLength = 16 * 1024;
    private static readonly string[] Roles = ["background", "panel", "raised", "text", "primary", "secondary", "danger"];

    internal static string Serialize(ThemeDocument document, bool includeThumbnail = true)
    {
        var s = document.Settings;
        var colors = Colors(s);
        var roles = new JsonObject();
        for (var i = 0; i < Roles.Length; i++) roles[Roles[i]] = Hex(colors[i]);
        var root = new JsonObject
        {
            ["format"] = Format,
            ["version"] = 1,
            ["name"] = CleanName(document.Name),
            ["roles"] = roles,
            ["mapping"] = new JsonObject
            {
                ["textCutoff"] = Math.Round(s.TextCutoff, 2),
                ["exactAccents"] = s.ExactAccents,
                ["foregroundAlphaFloor"] = Math.Round(s.ForegroundAlphaFloor, 2)
            }
        };
        if (!string.IsNullOrWhiteSpace(document.Author)) root["author"] = CleanName(document.Author);
        if (!string.IsNullOrWhiteSpace(document.Font)) root["font"] = document.Font;
        if (!string.IsNullOrEmpty(document.TextReplacements)) root["textReplacements"] = document.TextReplacements;
        if (document.ThemePanels is { } panels) root["themePanels"] = panels;
        if (includeThumbnail && ValidThumbnail(document.Thumbnail)) root["thumbnail"] = document.Thumbnail;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Parses untrusted JSON (files, gallery downloads) and rejects anything that is not a complete theme.</summary>
    internal static ThemeDocument Parse(string json) => Read(JsonNode.Parse(json) ?? throw Invalid("The file is empty."));

    internal static ThemeDocument Read(JsonNode node)
    {
        if (node is not JsonObject root || (string?)root["format"] != Format)
            throw Invalid("This is not an AfterThemed theme.");
        if (root["roles"] is not JsonObject roles) throw Invalid("The theme has no color roles.");
        var colors = new Color[Roles.Length];
        for (var i = 0; i < Roles.Length; i++)
            if (!TryHex(String(roles[Roles[i]]), out colors[i]))
                throw Invalid($"The theme's {Roles[i]} color is missing or invalid.");
        var mapping = root["mapping"] as JsonObject;
        var cutoff = Math.Clamp(Number(mapping?["textCutoff"]) ?? .43f, .20f, .80f);
        var exact = mapping?["exactAccents"] is JsonValue e && e.TryGetValue<bool>(out var exactValue) && exactValue;
        var floor = Math.Clamp(Number(mapping?["foregroundAlphaFloor"]) ?? 0f, 0f, 1f);
        var replacements = String(root["textReplacements"]);
        var thumbnail = String(root["thumbnail"]);
        return new ThemeDocument(CleanName(String(root["name"]) ?? "Imported theme"),
            new ThemeSettings(colors[0], colors[1], colors[2], colors[3], colors[4], colors[5], colors[6], cutoff, exact, floor))
        {
            Author = String(root["author"]) is { } author ? CleanName(author) : null,
            Font = String(root["font"]),
            TextReplacements = replacements is { Length: <= MaxReplacementsLength } ? replacements : null,
            ThemePanels = root["themePanels"] is JsonValue p && p.TryGetValue<bool>(out var panels) ? panels : null,
            Thumbnail = ValidThumbnail(thumbnail) ? thumbnail : null
        };
    }

    /// <summary>A short code: version, 7 RGB colors, cutoff, flags, alpha floor, then the UTF-8 name.</summary>
    internal static string ToShareCode(ThemeDocument document)
    {
        var s = document.Settings;
        var name = Encoding.UTF8.GetBytes(CleanName(document.Name));
        if (name.Length > 48) name = Encoding.UTF8.GetBytes(TrimUtf8(CleanName(document.Name), 48));
        var bytes = new List<byte> { 1 };
        foreach (var color in Colors(s)) bytes.AddRange([color.R, color.G, color.B]);
        bytes.Add((byte)Math.Round(Math.Clamp(s.TextCutoff, .2f, .8f) * 100));
        bytes.Add((byte)(s.ExactAccents ? 1 : 0));
        bytes.Add((byte)Math.Round(Math.Clamp(s.ForegroundAlphaFloor, 0f, 1f) * 100));
        bytes.AddRange(name);
        return SharePrefix + Convert.ToBase64String(bytes.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static ThemeDocument FromShareCode(string code)
    {
        var trimmed = new string((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (!trimmed.StartsWith(SharePrefix, StringComparison.OrdinalIgnoreCase)) throw Invalid("Share codes start with AT1-.");
        var body = trimmed[SharePrefix.Length..].Replace('-', '+').Replace('_', '/');
        byte[] bytes;
        try { bytes = Convert.FromBase64String(body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=')); }
        catch (FormatException) { throw Invalid("The share code is damaged."); }
        if (bytes.Length < 25 || bytes[0] != 1 || bytes.Length > 25 + 48) throw Invalid("The share code is damaged.");
        var c = Enumerable.Range(0, 7).Select(i => Color.FromArgb(bytes[1 + i * 3], bytes[2 + i * 3], bytes[3 + i * 3])).ToArray();
        var name = Encoding.UTF8.GetString(bytes, 25, bytes.Length - 25);
        return new ThemeDocument(CleanName(name.Length == 0 ? "Shared theme" : name), new ThemeSettings(
            c[0], c[1], c[2], c[3], c[4], c[5], c[6], Math.Clamp(bytes[22] / 100f, .2f, .8f),
            (bytes[23] & 1) == 1, Math.Clamp(bytes[24] / 100f, 0f, 1f)));
    }

    internal static Color[] Colors(ThemeSettings s) =>
        [s.Background, s.Panel, s.Raised, s.Text, s.Primary, s.Secondary, s.Danger];

    internal static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool TryHex(string? value, out Color color)
    {
        color = Color.Empty;
        if (value is not { Length: 7 } || value[0] != '#' ||
            !int.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return false;
        color = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        return true;
    }

    private static string CleanName(string name)
    {
        var clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0) clean = "Untitled theme";
        return clean.Length <= MaxNameLength ? clean : clean[..MaxNameLength];
    }

    private static string TrimUtf8(string value, int maxBytes)
    {
        while (Encoding.UTF8.GetByteCount(value) > maxBytes) value = value[..^1];
        return value;
    }

    private static bool ValidThumbnail(string? value) =>
        value is { Length: > 22 and <= MaxThumbnailLength } && value.StartsWith("data:image/png;base64,", StringComparison.Ordinal);

    private static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static float? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? (float)number : null;

    private static InvalidDataException Invalid(string message) => new(message);
}
