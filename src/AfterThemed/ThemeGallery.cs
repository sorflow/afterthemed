using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace DvauiThemeEditor;

/// <summary>Community themes are .afterthemed documents listed in gallery/index.json of the AfterThemed repository.</summary>
internal static class ThemeGallery
{
    internal const string Repository = "sorflow/afterthemed";
    internal const string IndexUrl = $"https://raw.githubusercontent.com/{Repository}/main/gallery/index.json";
    private const int MaxThemes = 200;

    internal static async Task<List<ThemeDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AfterThemed", UpdateChecker.CurrentVersion().ToString()));
        return Parse(await client.GetStringAsync(IndexUrl, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Downloaded entries are untrusted: anything that is not a complete theme is skipped.</summary>
    internal static List<ThemeDocument> Parse(string json)
    {
        var themes = new List<ThemeDocument>();
        if (JsonNode.Parse(json)?["themes"] is not JsonArray items) throw new InvalidDataException("The gallery index has no themes list.");
        foreach (var item in items.Take(MaxThemes))
        {
            if (item is null) continue;
            try { themes.Add(ThemeDocuments.Read(item)); }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or FormatException) { }
        }
        return themes;
    }

    internal static string SubmitUrl(ThemeDocument document) =>
        $"https://github.com/{Repository}/issues/new" +
        $"?labels={Uri.EscapeDataString("gallery")}" +
        $"&title={Uri.EscapeDataString("[Gallery] " + document.Name)}" +
        $"&body={Uri.EscapeDataString("Please add this theme to the AfterThemed gallery.\n\n```json\n" +
            ThemeDocuments.Serialize(document, includeThumbnail: false) + "\n```")}";
}
