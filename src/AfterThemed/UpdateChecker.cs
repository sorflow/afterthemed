using System.Net.Http.Headers;
using System.Text.Json;

namespace DvauiThemeEditor;

internal sealed record UpdateInfo(Version LatestVersion, string TagName, string ReleasePageUrl, string DownloadUrl);

internal static class UpdateChecker
{
    internal const string LatestReleaseApiUrl = "https://api.github.com/repos/sorflow/afterthemed/releases/latest";
    internal const string LatestReleasePageUrl = "https://github.com/sorflow/afterthemed/releases/latest";

    internal static Version CurrentVersion() => ParseVersion(Application.ProductVersion) ?? new Version(0, 0);

    internal static async Task<UpdateInfo?> CheckLatestAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AfterThemed", CurrentVersion().ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var json = await client.GetStringAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        return ParseLatestRelease(json, currentVersion);
    }

    internal static UpdateInfo? ParseLatestRelease(string json, Version currentVersion)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True) return null;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var latestVersion = ParseVersion(tag);
        if (latestVersion is null || latestVersion.CompareTo(currentVersion) <= 0) return null;

        var pageUrl = root.TryGetProperty("html_url", out var pageElement) && !string.IsNullOrWhiteSpace(pageElement.GetString())
            ? pageElement.GetString()!
            : LatestReleasePageUrl;

        return new UpdateInfo(latestVersion, tag ?? latestVersion.ToString(), pageUrl, FindInstallerUrl(root) ?? pageUrl);
    }

    internal static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V')) trimmed = trimmed[1..];
        var metadata = trimmed.IndexOfAny(['+', '-']);
        if (metadata >= 0) trimmed = trimmed[..metadata];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    private static string? FindInstallerUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) ||
                !name.Contains("Setup", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;

            var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
            if (!string.IsNullOrWhiteSpace(url)) return url;
        }
        return null;
    }
}
