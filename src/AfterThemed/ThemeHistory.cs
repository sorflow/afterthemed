using System.Text.Json;

namespace DvauiThemeEditor;

internal sealed record HistoryEntry(string Id, DateTime InstalledAt, string[] Targets, string Theme);

/// <summary>What AfterThemed last installed into a target, so an After Effects update that replaces it can be noticed.</summary>
internal sealed record InstalledRecord(string Target, string Sha256, string EntryId, string Theme);

internal sealed class ThemeHistoryFile
{
    public List<HistoryEntry> History { get; set; } = [];
    public List<InstalledRecord> Installed { get; set; } = [];
}

internal static class ThemeHistory
{
    internal const int Limit = 10;

    internal static ThemeHistoryFile Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ThemeHistoryFile>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            AppDiagnostics.Write("Theme history unreadable; starting fresh · " + ex.Message);
            return new();
        }
    }

    /// <summary>Adds one installed target to the entry <paramref name="id"/>, creating it at the top of the list.</summary>
    internal static void RecordInstall(string path, string id, ThemeDocument document, string target, string sha256)
    {
        var file = Load(path);
        var theme = ThemeDocuments.Serialize(document, includeThumbnail: false);
        var existing = file.History.FindIndex(entry => entry.Id == id);
        if (existing >= 0)
            file.History[existing] = file.History[existing] with { Targets = [.. file.History[existing].Targets, target] };
        else
            file.History.Insert(0, new HistoryEntry(id, DateTime.UtcNow, [target], theme));
        if (file.History.Count > Limit) file.History.RemoveRange(Limit, file.History.Count - Limit);
        file.Installed.RemoveAll(item => SamePath(item.Target, target));
        file.Installed.Add(new InstalledRecord(target, sha256, id, theme));
        Save(path, file);
    }

    /// <summary>Stops watching a target, after a restore or when the user dismisses a re-apply offer.</summary>
    internal static void Forget(string path, string target)
    {
        var file = Load(path);
        if (file.Installed.RemoveAll(item => SamePath(item.Target, target)) > 0) Save(path, file);
    }

    /// <summary>Targets whose installed DLL no longer matches what AfterThemed wrote, typically after an Adobe update.</summary>
    internal static List<InstalledRecord> FindReplaced(ThemeHistoryFile file, Func<string, string?> hashOf) =>
        file.Installed.Where(item => File.Exists(item.Target) &&
            hashOf(item.Target) is { } hash && !string.Equals(hash, item.Sha256, StringComparison.OrdinalIgnoreCase)).ToList();

    private static void Save(string path, ThemeHistoryFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
