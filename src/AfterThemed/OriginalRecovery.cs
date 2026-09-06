namespace DvauiThemeEditor;

internal sealed record RecoveryCandidate(string Path, string Hash)
{
    public override string ToString() => Path;
}

internal static class OriginalRecovery
{
    internal static string[] DefaultSearchRoots(string originalsRoot) => new[]
    {
        originalsRoot,
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(originalsRoot))!, "Backups"),
        Path.Combine(AppContext.BaseDirectory, "Backups"),
        Path.Combine(Environment.CurrentDirectory, "Backups"),
        // Earlier portable releases kept their snapshots alongside the editor in Downloads.
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "DVAUI Theme Editor", "Backups")
    }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static IReadOnlyList<RecoveryCandidate> Find(string target, IEnumerable<string> roots,
        CancellationToken cancellation = default)
    {
        var candidates = new Dictionary<string, RecoveryCandidate>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            foreach (var file in CandidateFiles(root))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!visited.Add(file) || string.Equals(file, target, StringComparison.OrdinalIgnoreCase)) continue;
                if (visited.Count > 2000) return candidates.Values.ToArray();
                if (!OriginalDllStore.IsVerifiedMatchingBackup(file, target)) continue;
                var hash = OriginalDllStore.Sha256(file);
                candidates.TryAdd(hash, new(file, hash));
            }
        }
        return candidates.Values.ToArray();
    }

    private static IEnumerable<string> CandidateFiles(string root)
    {
        if (!Directory.Exists(root)) return [];
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true, MaxRecursionDepth = 3,
                IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint
            };
            return Directory.EnumerateFiles(root, "*", options)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".adobe-original", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".recovery", StringComparison.OrdinalIgnoreCase))
                .Take(2001).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }
}
