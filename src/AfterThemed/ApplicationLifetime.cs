namespace DvauiThemeEditor;

internal static class ApplicationLifetime
{
    internal const string UpgradeMutexName = "AfterThemed.App";

    internal static Mutex HoldUpgradeMutex() => new(false, UpgradeMutexName);

    /// <summary>
    /// "1.3.12 (build 0da47e5)". Application.ProductVersion carries the informational version, which
    /// appends the full commit SHA and reads as noise wherever a version is shown to a person. The
    /// commit still identifies the exact build, so it is kept in short form rather than dropped.
    /// </summary>
    internal static string DisplayVersion()
    {
        var product = Application.ProductVersion;
        var separator = product.IndexOf('+');
        if (separator < 0) return product;

        var version = product[..separator];
        var build = product[(separator + 1)..];
        return build.Length >= 7 ? $"{version} (build {build[..7]})" : version;
    }
}
