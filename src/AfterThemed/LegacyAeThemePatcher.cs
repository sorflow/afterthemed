using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DvauiThemeEditor;

internal sealed record LegacyAeThemeCompanion(string InputPath, string TargetPath, string Sha256);

internal static class LegacyAeThemePatcher
{
    private const int ThemedPaddingRun = 200;
    private const int MinimumPairedPrefix = 15;

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
                "This After Effects release stores its native color themes in AfterFXLib.dll, but that companion file was not found.",
                targetPath);

        var originalPath = OriginalDllStore.CaptureIfMissing(targetPath, originalsRoot, out _,
            InspectCompanionOriginal);
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
        var restorePath = OriginalDllStore.CreateRestoreDll(targetPath, originalsRoot, outputPath,
            InspectCompanionOriginal);
        return new LegacyAeThemeCompanion(restorePath, targetPath, OriginalDllStore.Sha256(restorePath));
    }

    /// <summary>
    /// Companion originals are accepted on their embedded Adobe signer alone, so a companion that
    /// AfterThemed already themed would otherwise be snapshotted as if it were Adobe's original.
    /// Theming rewrites each color resource as minified XML and pads the remainder with spaces, so a
    /// long padding run marks a file that must never become a preserved original.
    /// </summary>
    internal static bool IsAlreadyThemed(string companionPath)
    {
        try
        {
            var data = File.ReadAllBytes(companionPath);
            foreach (var resource in new DvauiPeImage(data).Resources())
            {
                if (!string.Equals(resource.Type, "XML", StringComparison.OrdinalIgnoreCase) ||
                    !RequiredResourceNames.Contains(resource.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                var run = 0;
                for (var index = resource.Offset; index < resource.Offset + resource.Size; index++)
                {
                    if (data[index] == (byte)' ')
                    {
                        if (++run >= ThemedPaddingRun) return true;
                    }
                    else run = 0;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static OriginalDllStore.AdobeSignature InspectCompanionOriginal(string path)
    {
        if (IsAlreadyThemed(path))
            throw new InvalidDataException(
                "This AfterFXLib.dll has already been themed by AfterThemed, so it cannot be preserved as " +
                "an Adobe original. Restore this After Effects version first, or repair it in Creative Cloud.");
        return OriginalDllStore.EnsureAdobeCompanionSigner(path);
    }

    internal static bool RequiresCompanion(string dvauiPath)
    {
        try
        {
            return HasNativeThemeResources(CompanionTarget(dvauiPath));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// After Effects releases do not report a single comparable version number: CC 2019 and 2023
    /// stamp the application version onto dvaui.dll while 2020 and 2021 stamp the DVA version, so
    /// the companion file is selected by the resources it actually carries instead of by version.
    /// </summary>
    internal static bool HasNativeThemeResources(string companionPath)
    {
        if (!File.Exists(companionPath)) return false;

        PeResource[] resources;
        try
        {
            resources = new DvauiPeImage(File.ReadAllBytes(companionPath))
                .Resources()
                .Where(resource => string.Equals(resource.Type, "XML", StringComparison.OrdinalIgnoreCase) &&
                                   RequiredResourceNames.Contains(resource.Name, StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return false;
        }

        return RequiredResourceNames.All(name =>
            resources.Any(resource => string.Equals(resource.Name, name, StringComparison.OrdinalIgnoreCase)));
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
                $"The After Effects native theme resources are incomplete ({string.Join(", ", missing)} missing). No changes were made.");

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
        var surfaces = CollectSurfaces(xml, settings);
        var rewritten = KeyFrame.Replace(xml, match =>
        {
            var attributes = ParseAttributes(match.Groups["attributes"].Value);
            var name = attributes.FirstOrDefault(attribute =>
                string.Equals(attribute.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
            var target = TargetFor(name, settings, surfaces);
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

    private static Color? TargetFor(string rawKey, ThemeSettings settings) =>
        TargetFor(rawKey, settings, null);

    private static Color? TargetFor(string rawKey, ThemeSettings settings,
        IReadOnlyDictionary<string, Color>? surfaces)
    {
        var key = NormalizeKey(rawKey);
        if (key.Length == 0 || IsDocumentColor(key)) return null;

        // State is not a semantic role. A selected/focused text key must remain
        // foreground even when its paired surface uses the primary accent.
        if (IsForegroundKey(key))
        {
            if (key.Contains("error") || key.Contains("danger") || key.Contains("negative") ||
                key.Contains("alert") || key.Contains("invalid"))
                return settings.Danger;

            // Foreground roles are readable only against the surface that actually sits behind
            // them, and the surface a role sits on cannot be inferred from its own name: focused
            // button text sits on the button's own background, not on the accent. Prefer the
            // surface role declared alongside it, and fall back to the name when there is none.
            var surface = PairedSurfaceFor(TrimKey(rawKey), surfaces)
                          ?? (IsAccentStateKey(key) ? settings.Primary : (Color?)null)
                          ?? SurfaceFor(key, settings)
                          ?? settings.Panel;
            var foreground = ContrastingForeground(surface, settings);
            return key.Contains("disabled") || key.Contains("muted") || key.Contains("shadow")
                ? Mix(surface, foreground, .55)
                : foreground;
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

        return SurfaceFor(key, settings);
    }

    private static string TrimKey(string rawKey) => rawKey.Trim('&', ';');

    private static string NormalizeKey(string rawKey) => TrimKey(rawKey).ToLowerInvariant();

    /// <summary>
    /// Color names concatenate words, so a shared prefix only identifies the same control when it
    /// ends where a word ends in both names.
    /// </summary>
    private static bool IsWordBoundary(string name, int index) =>
        index >= name.Length || !char.IsLetter(name[index]) || char.IsUpper(name[index]);

    private static bool IsDecorativeSurface(string key) =>
        key.Contains("shadow", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("glow", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("outline", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("border", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("separator", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("divider", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Collects the surface color every non-foreground role in the document paints, so paired
    /// foreground roles can be contrasted against the surface they actually sit on.
    /// </summary>
    private static Dictionary<string, Color> CollectSurfaces(string xml, ThemeSettings settings)
    {
        var surfaces = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (Match match in KeyFrame.Matches(xml))
        {
            var name = ParseAttributes(match.Groups["attributes"].Value)
                .FirstOrDefault(attribute => attribute.Name == "name")?.Value;
            if (name is null) continue;

            var key = NormalizeKey(name);
            var original = TrimKey(name);
            if (key.Length == 0 || surfaces.ContainsKey(original) ||
                IsDocumentColor(key) || IsForegroundKey(key) ||
                (!key.Contains("background") && !key.Contains("fill") &&
                 !key.Contains("gradient"))) continue;

            var color = TargetFor(name, settings, null);
            if (color is not null) surfaces[original] = color.Value;
        }

        return surfaces;
    }

    /// <summary>
    /// Finds the surface role declared for the same control as <paramref name="key"/>, matching on
    /// the longest shared prefix so a control variant pairs with its own surface.
    /// </summary>
    private static Color? PairedSurfaceFor(string key, IReadOnlyDictionary<string, Color>? surfaces)
    {
        if (surfaces is null) return null;

        Color? paired = null;
        var best = (Shared: 0, Face: 0);
        foreach (var (candidate, color) in surfaces.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var shared = 0;
            var limit = Math.Min(key.Length, candidate.Length);
            while (shared < limit && key[shared] == candidate[shared]) shared++;

            // A prefix that stops mid-word is a coincidence: "...DownText" and "...DownTopShadow"
            // share a "T" that means nothing. Retreat to where both names last ended a word.
            while (shared > 0 && !(IsWordBoundary(key, shared) && IsWordBoundary(candidate, shared)))
                shared--;

            if (shared < MinimumPairedPrefix) continue;

            // A control's readable face is its fill or background, not the shadow, glow, or
            // outline drawn around it.
            var face = IsDecorativeSurface(candidate) ? 0 : 1;
            if (shared < best.Shared || (shared == best.Shared && face <= best.Face)) continue;
            best = (shared, face);
            paired = color;
        }

        return paired;
    }

    /// <summary>
    /// Resolves the surface color a key paints, which is also the surface a paired foreground role
    /// has to stay readable against.
    /// </summary>
    private static Color? SurfaceFor(string key, ThemeSettings settings)
    {
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
