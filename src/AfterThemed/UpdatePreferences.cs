namespace DvauiThemeEditor;

internal sealed class UpdatePreferences(string dataRoot)
{
    private readonly string ignoredVersionPath = Path.Combine(dataRoot, "ignored-update.txt");

    internal bool ShouldNotify(Version availableVersion)
    {
        try
        {
            var ignored = UpdateChecker.ParseVersion(File.ReadAllText(ignoredVersionPath));
            return ignored is null || UpdateChecker.CompareVersions(availableVersion, ignored) > 0;
        }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppDiagnostics.Write("Unable to read ignored update version: " + ex.Message);
            return true;
        }
    }

    internal void Ignore(Version version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ignoredVersionPath)!);
        var temporaryPath = ignoredVersionPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, version.ToString());
            File.Move(temporaryPath, ignoredVersionPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
