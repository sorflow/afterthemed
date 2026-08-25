namespace DvauiThemeEditor;

internal static class ApplicationLifetime
{
    internal const string UpgradeMutexName = "AfterThemed.App";

    internal static Mutex HoldUpgradeMutex() => new(false, UpgradeMutexName);
}
