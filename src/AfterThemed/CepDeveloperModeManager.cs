using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DvauiThemeEditor;

internal sealed record CepDeveloperModeStatus(
    int? RuntimeMajor,
    bool IsEnabled,
    string RegistryPath,
    string Description);

internal static class CepDeveloperModeManager
{
    private const string PlayerDebugMode = "PlayerDebugMode";
    private const string StateFileName = "cep-player-debug-mode.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    internal static CepDeveloperModeStatus Inspect(string targetDllPath)
    {
        var runtimeMajor = FindCepRuntimeMajor(targetDllPath);
        if (runtimeMajor is null)
            return new CepDeveloperModeStatus(null, false, string.Empty,
                "CEP runtime was not found beside the selected After Effects installation.");

        var registryPath = RegistryPath(runtimeMajor.Value);
        using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: false);
        var value = key?.GetValue(PlayerDebugMode, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var enabled = string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), "1", StringComparison.Ordinal);
        return new CepDeveloperModeStatus(runtimeMajor, enabled, $"HKCU\\{registryPath}",
            enabled
                ? $"CEP {runtimeMajor} developer mode is enabled."
                : $"CEP {runtimeMajor} developer mode will be enabled while signed panels are themed.");
    }

    internal static bool EnsureEnabled(string targetDllPath, string backupRoot, out bool changed, out string message)
    {
        changed = false;
        var status = Inspect(targetDllPath);
        if (status.RuntimeMajor is null)
        {
            message = status.Description;
            return false;
        }

        var statePath = Path.Combine(backupRoot, StateFileName);
        var state = LoadState(statePath);
        var registryPath = RegistryPath(status.RuntimeMajor.Value);
        var existingEntry = state.Entries.FirstOrDefault(entry => entry.RuntimeMajor == status.RuntimeMajor.Value);

        using var existingKey = Registry.CurrentUser.OpenSubKey(registryPath, writable: false);
        var keyExisted = existingKey is not null;
        var originalValue = existingKey?.GetValue(PlayerDebugMode, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var valueExisted = originalValue is not null;
        var originalKind = valueExisted ? existingKey!.GetValueKind(PlayerDebugMode) : RegistryValueKind.None;

        if (existingEntry is null)
        {
            existingEntry = DebugModeEntry.Capture(status.RuntimeMajor.Value, registryPath, keyExisted,
                valueExisted, originalKind, originalValue);
            state.Entries.Add(existingEntry);
            WriteJsonAtomic(statePath, state);
        }

        using var key = Registry.CurrentUser.CreateSubKey(registryPath, writable: true) ??
                        throw new UnauthorizedAccessException($"Could not open HKCU\\{registryPath} for writing.");
        var before = key.GetValue(PlayerDebugMode, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (!string.Equals(Convert.ToString(before, CultureInfo.InvariantCulture), "1", StringComparison.Ordinal) ||
            (before is not null && key.GetValueKind(PlayerDebugMode) != RegistryValueKind.String))
        {
            key.SetValue(PlayerDebugMode, "1", RegistryValueKind.String);
            changed = true;
        }

        var verified = key.GetValue(PlayerDebugMode, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (!string.Equals(Convert.ToString(verified, CultureInfo.InvariantCulture), "1", StringComparison.Ordinal))
        {
            message = $"Could not verify {status.RegistryPath}\\{PlayerDebugMode}=1.";
            return false;
        }

        message = changed
            ? $"Enabled CEP {status.RuntimeMajor} developer mode so modified signed extensions can load."
            : $"CEP {status.RuntimeMajor} developer mode was already enabled.";
        return true;
    }

    internal static int Restore(string backupRoot, List<string> warnings, ref int conflicts)
    {
        var statePath = Path.Combine(backupRoot, StateFileName);
        if (!File.Exists(statePath)) return 0;

        var state = LoadState(statePath);
        var restored = 0;
        var allRestored = true;
        foreach (var entry in state.Entries)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(entry.RegistryPath, writable: true);
                var current = key?.GetValue(PlayerDebugMode, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (!string.Equals(Convert.ToString(current, CultureInfo.InvariantCulture), "1", StringComparison.Ordinal))
                {
                    conflicts++;
                    allRestored = false;
                    warnings.Add($"CEP developer mode was not restored because it changed after theming: HKCU\\{entry.RegistryPath}\\{PlayerDebugMode}");
                    continue;
                }

                if (entry.ValueExisted)
                {
                    using var writable = key ?? Registry.CurrentUser.CreateSubKey(entry.RegistryPath, writable: true) ??
                                         throw new UnauthorizedAccessException($"Could not reopen HKCU\\{entry.RegistryPath}.");
                    writable.SetValue(PlayerDebugMode, entry.GetOriginalValue(), (RegistryValueKind)entry.OriginalKind);
                }
                else
                {
                    key?.DeleteValue(PlayerDebugMode, throwOnMissingValue: false);
                }

                var verified = Registry.CurrentUser.OpenSubKey(entry.RegistryPath, writable: false);
                using (verified)
                {
                    var restoredValue = verified?.GetValue(PlayerDebugMode, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (!entry.MatchesOriginal(restoredValue, verified))
                        throw new IOException($"CEP developer mode restore verification failed for HKCU\\{entry.RegistryPath}.");
                }

                if (!entry.KeyExisted)
                {
                    using var emptyKey = Registry.CurrentUser.OpenSubKey(entry.RegistryPath, writable: false);
                    if (emptyKey is { ValueCount: 0, SubKeyCount: 0 })
                        Registry.CurrentUser.DeleteSubKey(entry.RegistryPath, throwOnMissingSubKey: false);
                }

                restored++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                conflicts++;
                allRestored = false;
                warnings.Add($"Could not restore CEP developer mode for HKCU\\{entry.RegistryPath}: {exception.Message}");
            }
        }

        if (allRestored) File.Delete(statePath);
        return restored;
    }

    private static int? FindCepRuntimeMajor(string targetDllPath)
    {
        if (string.IsNullOrWhiteSpace(targetDllPath) || !File.Exists(targetDllPath)) return null;
        var supportDirectory = Path.GetDirectoryName(Path.GetFullPath(targetDllPath));
        if (supportDirectory is null) return null;

        var direct = Path.Combine(supportDirectory, "CEPHtmlEngine", "CEPHtmlEngine.exe");
        string? enginePath = File.Exists(direct) ? direct : null;
        if (enginePath is null)
        {
            try
            {
                enginePath = Directory.EnumerateFiles(supportDirectory, "CEPHtmlEngine.exe", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    MaxRecursionDepth = 4
                }).FirstOrDefault();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        if (enginePath is null) return null;
        var version = FileVersionInfo.GetVersionInfo(enginePath);
        if (version.ProductMajorPart > 0) return version.ProductMajorPart;
        var first = (version.ProductVersion ?? version.FileVersion)?.Split('.', '-', '+')[0];
        return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;
    }

    private static string RegistryPath(int runtimeMajor) => $"Software\\Adobe\\CSXS.{runtimeMajor}";

    private static DebugModeState LoadState(string path)
    {
        if (!File.Exists(path)) return new DebugModeState();
        return JsonSerializer.Deserialize<DebugModeState>(File.ReadAllText(path), JsonOptions) ??
               throw new InvalidDataException("The CEP developer-mode backup is empty.");
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".afterthemed-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed class DebugModeState
    {
        public int SchemaVersion { get; set; } = 1;
        public List<DebugModeEntry> Entries { get; set; } = [];
    }

    private sealed class DebugModeEntry
    {
        public int RuntimeMajor { get; set; }
        public string RegistryPath { get; set; } = string.Empty;
        public bool KeyExisted { get; set; }
        public bool ValueExisted { get; set; }
        public int OriginalKind { get; set; }
        public string? OriginalString { get; set; }
        public long? OriginalInteger { get; set; }
        public string[]? OriginalStrings { get; set; }
        public string? OriginalBinaryBase64 { get; set; }

        internal static DebugModeEntry Capture(int runtimeMajor, string registryPath, bool keyExisted,
            bool valueExisted, RegistryValueKind kind, object? value)
        {
            var entry = new DebugModeEntry
            {
                RuntimeMajor = runtimeMajor,
                RegistryPath = registryPath,
                KeyExisted = keyExisted,
                ValueExisted = valueExisted,
                OriginalKind = (int)kind
            };
            if (!valueExisted) return entry;

            switch (value)
            {
                case byte[] bytes:
                    entry.OriginalBinaryBase64 = Convert.ToBase64String(bytes);
                    break;
                case string[] strings:
                    entry.OriginalStrings = strings;
                    break;
                case int integer:
                    entry.OriginalInteger = integer;
                    break;
                case long integer:
                    entry.OriginalInteger = integer;
                    break;
                default:
                    entry.OriginalString = Convert.ToString(value, CultureInfo.InvariantCulture);
                    break;
            }
            return entry;
        }

        internal object GetOriginalValue() => (RegistryValueKind)OriginalKind switch
        {
            RegistryValueKind.Binary => Convert.FromBase64String(OriginalBinaryBase64 ?? string.Empty),
            RegistryValueKind.MultiString => OriginalStrings ?? [],
            RegistryValueKind.DWord => checked((int)(OriginalInteger ?? 0)),
            RegistryValueKind.QWord => OriginalInteger ?? 0L,
            _ => OriginalString ?? string.Empty
        };

        internal bool MatchesOriginal(object? value, RegistryKey? key)
        {
            if (!ValueExisted) return value is null;
            if (value is null || key is null || key.GetValueKind(PlayerDebugMode) != (RegistryValueKind)OriginalKind)
                return false;
            return GetOriginalValue() switch
            {
                byte[] expected when value is byte[] actual => expected.SequenceEqual(actual),
                string[] expected when value is string[] actual => expected.SequenceEqual(actual, StringComparer.Ordinal),
                var expected => Equals(expected, value)
            };
        }
    }
}
