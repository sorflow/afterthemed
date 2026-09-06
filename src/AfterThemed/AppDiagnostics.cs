namespace DvauiThemeEditor;

internal static class AppDiagnostics
{
    private static readonly object Gate = new();

    internal static void Write(string text)
    {
        lock (Gate)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AfterThemed", "Logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"activity-{DateTime.UtcNow:yyyyMMdd}.log");
                // Bound each day's log. Do not delete originals, backups, or diagnostics to free space.
                if (File.Exists(path) && new FileInfo(path).Length > 4 * 1024 * 1024) return;
                File.AppendAllText(path, $"[{DateTime.UtcNow:O}] {text}{Environment.NewLine}");
            }
            catch (Exception) { /* Logging must not mask the operation's failure. */ }
        }
    }
}
