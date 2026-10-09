using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DvauiThemeEditor;

public partial class Form1
{
    private const string EditorOrigin = "https://afterthemed.local/";
    private WebView2? webEditor;
    private bool webEditorReady;
    private BugReportBundle? webBugReport;
    private string webInstallStatus = "idle";
    private string webInstallDetail = string.Empty;
    // The engine stage the install rail shows: original, generate, install, or done. A failure keeps the stage it failed in.
    private string webInstallStage = string.Empty;
    private bool WebInstallBusy => webInstallStatus is "preparing" or "installing";
    private bool installAll;
    private string? pendingOpenFile;
    private object? navigate;
    private int navigateId;
    private List<InstalledRecord> replacedThemes = [];
    private List<ThemeDocument> gallery = [];
    private string galleryStatus = "idle";
    private string galleryError = string.Empty;
    // Discovery scans every fixed drive and the uninstall registry, so it runs at startup and when the
    // target changes, never on each published state.
    private IReadOnlyList<AfterEffectsInstall> knownInstalls = [];
    // The last command sequence number handled; the editor keeps its own edits until this catches up.
    private int lastSeq;
    private bool publishQueued;
    private readonly System.Windows.Forms.Timer sessionSaveTimer = new() { Interval = 500 };

    private void SetWebInstallStatus(string status, string detail, string? stage = null)
    {
        webInstallStatus = status;
        if (stage is not null) webInstallStage = stage;
        webInstallDetail = detail;
        // Installs block the UI thread, so the status must go out now rather than on the next idle.
        PublishWebStateNow();
    }

    private void ScheduleSaveSession()
    {
        sessionSaveTimer.Stop();
        sessionSaveTimer.Start();
    }
    internal bool WebEditorBridgeReady;

    private async void StartWebUi()
    {
        var assets = Path.Combine(AppContext.BaseDirectory, "WebUi");
        if (!File.Exists(Path.Combine(assets, "index.html")))
        {
            Log("Web editor assets are unavailable; using the native editor.");
            return;
        }

        // The native editor is retained as a recovery fallback, but must not flash during the
        // asynchronous WebView2 startup. Keep it hidden until navigation succeeds.
        foreach (Control control in Controls)
            control.Visible = false;

        installAll = File.Exists(InstallAllFile) && File.ReadAllText(InstallAllFile).Trim() == "true";
        replacedThemes = ThemeHistory.FindReplaced(ThemeHistory.Load(HistoryFile), path =>
        {
            try { return OriginalDllStore.Sha256(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        });
        foreach (var item in replacedThemes)
            Log($"After Effects changed since your theme was installed · {item.Target}");

        // The caption buttons switch between Maximize and Restore, so publish when that state flips.
        var wasMaximized = WindowState == FormWindowState.Maximized;
        SizeChanged += (_, _) =>
        {
            if (wasMaximized == (WindowState == FormWindowState.Maximized)) return;
            wasMaximized = !wasMaximized;
            PublishWebState();
        };
        var editor = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = UiPalette.Window };
        webEditor = editor;
        editor.Visible = true;
        Controls.Add(editor);
        editor.BringToFront();
        try
        {
            await editor.EnsureCoreWebView2Async();
            if (IsDisposed || editor.IsDisposed) return;
            editor.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "afterthemed.local", assets, CoreWebView2HostResourceAccessKind.DenyCors);
            editor.CoreWebView2.Settings.IsStatusBarEnabled = false;
            // Disable Inspect and developer-tool shortcuts in the desktop editor.
            editor.CoreWebView2.Settings.AreDevToolsEnabled = false;
            editor.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            editor.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!e.Uri.StartsWith(EditorOrigin, StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
            };
            editor.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (!e.Source.StartsWith(EditorOrigin, StringComparison.OrdinalIgnoreCase)) return;
                // Dropped or picked .aep files arrive as CoreWebView2File objects carrying their real paths.
                var files = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(file => file.Path).ToList() ?? [];
                HandleWebMessage(e.WebMessageAsJson, files);
            };
            editor.CoreWebView2.ProcessFailed += (_, _) =>
            {
                if (IsDisposed || Disposing) return;
                webEditorReady = false;
                editor.Visible = false;
                foreach (Control control in Controls)
                    if (!ReferenceEquals(control, editor)) control.Visible = true;
                Log("Web editor stopped; the native editor is available.");
            };
            editor.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess)
                {
                    Log($"Web editor navigation failed ({e.WebErrorStatus}); using the native editor.");
                    editor.Visible = false;
                    foreach (Control control in Controls)
                        if (!ReferenceEquals(control, editor)) control.Visible = true;
                    return;
                }
                webEditorReady = true;
                foreach (Control control in Controls)
                    if (!ReferenceEquals(control, editor)) control.Visible = false;
                editor.BringToFront();
                PublishWebState();
            };
            editor.Source = new Uri(EditorOrigin + "index.html");
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Web editor unavailable: " + ex);
            if (!editor.IsDisposed) editor.Visible = false;
            foreach (Control control in Controls)
                if (!ReferenceEquals(control, editor)) control.Visible = true;
            Log("Web editor unavailable; using the native editor. " + ex.Message);
        }
    }

    private void HandleWebMessage(string json, IReadOnlyList<string>? files = null)
    {
        try
        {
            using var message = JsonDocument.Parse(json);
            var root = message.RootElement;
            var type = root.GetProperty("type").GetString();
            var value = root.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;
            if (root.TryGetProperty("seq", out var seq) && seq.TryGetInt32(out var seqValue)) lastSeq = seqValue;
            if (type?.StartsWith("aep", StringComparison.Ordinal) == true)
            {
                HandleAepMessage(type, value, files ?? []);
                PublishWebState();
                return;
            }
            if (WebInstallBusy && type is not ("ready" or "minimize" or "maximize" or "drag")) return;
            if (type is "preset" or "name" or "color" or "cutoff" or "text" or "font" or "themePanels" or "reset" or "import" or "chooseInstall" or "browseInstall" or "restore" or "applyShareCode" or "historyLoad" or "galleryUse")
            {
                webInstallStatus = "idle";
                webInstallStage = string.Empty;
                webInstallDetail = string.Empty;
            }
            switch (type)
            {
                case "ready":
                    WebEditorBridgeReady = true;
                    OpenPendingFile();
                    break;
                case "preset":
                    if (int.TryParse(value, out var selected) && selected >= 0 && selected < BuiltInPresets.Length)
                        preset.SelectedIndex = selected;
                    break;
                case "name": themeName.Text = value; break;
                case "color":
                    var name = root.GetProperty("key").GetString() ?? string.Empty;
                    if (colorBoxes.TryGetValue(name, out var box))
                    {
                        box.Text = value;
                        preset.SelectedIndex = BuiltInPresets.Length;
                    }
                    break;
                case "cutoff":
                    if (int.TryParse(value, out var cutoffNumber))
                        cutoff.Value = Math.Clamp(cutoffNumber, cutoff.Minimum, cutoff.Maximum);
                    break;
                case "text": textReplacements.Text = value; break;
                case "font":
                    var fontIndex = fontChoice.Items.FindIndex(item => item == value);
                    if (fontIndex >= 0) fontChoice.SelectedIndex = fontIndex;
                    break;
                case "themePanels": themePanels.Checked = value == "true"; break;
                case "reset": ApplyPreset(); break;
                case "import": ImportTheme(); break;
                case "generate": GenerateVariant(); break;
                case "install": GenerateAndInstall(); break;
                case "restore": Restore(); break;
                case "inventory": Inventory(); break;
                case "selectInstall": break;
                case "chooseInstall":
                    if (AfterEffectsCatalog.Discover().Any(item =>
                            string.Equals(item.DllPath, value, StringComparison.OrdinalIgnoreCase)))
                        ApplySelectedInstallation(value);
                    else
                        throw new InvalidOperationException("That After Effects installation is no longer available.");
                    break;
                case "browseInstall": BrowseForWebInstall(); break;
                case "scanPanels": RefreshPanelDiscovery(); break;
                case "applyPanels": ApplyPanelsOnly(); break;
                case "openOutput": Try(() => OpenFolder(Variants)); break;
                case "openOriginals": Try(() => OpenFolder(Originals)); break;
                case "openData": Try(() => OpenFolder(DataRoot)); break;
                case "about": break;
                case "reportBug": PrepareWebBugReport(); break;
                case "openBugIssue": OpenWebBugIssue(); break;
                case "showBugBundle": ShowWebBugBundle(); break;
                case "copyBugReport": CopyWebBugReport(); break;
                case "openLink": OpenWebLink(value); break;
                case "copyShareCode":
                    Clipboard.SetText(ThemeDocuments.ToShareCode(CurrentThemeDocument()));
                    Log("Share code copied to the clipboard.");
                    break;
                case "applyShareCode":
                    Try(() => ApplyThemeDocument(ThemeDocuments.FromShareCode(value), "SHARE CODE  ·  LIVE PREVIEW"));
                    break;
                case "exportTheme": Try(() => ExportTheme(value)); break;
                case "historyLoad": Try(() => ApplyHistory(value)); break;
                case "historyInstall":
                    Try(() => ApplyHistory(value));
                    GenerateAndInstall();
                    break;
                case "reapply":
                    var replaced = replacedThemes.Find(item => item.Target == value)
                        ?? throw new InvalidOperationException("That installation no longer needs re-applying.");
                    ApplyThemeDocument(ThemeDocuments.Parse(replaced.Theme), "RE-APPLIED AFTER AN AFTER EFFECTS UPDATE");
                    GenerateAndInstall([replaced.Target]);
                    break;
                case "dismissReapply":
                    ThemeHistory.Forget(HistoryFile, value);
                    replacedThemes.RemoveAll(item => item.Target == value);
                    break;
                case "installAll":
                    installAll = value == "true";
                    File.WriteAllText(InstallAllFile, installAll ? "true" : "false");
                    break;
                case "galleryLoad": LoadGallery(); break;
                case "themeDll": Try(ThemeDllFiles); break;
                case "galleryUse":
                    if (int.TryParse(value, out var galleryIndex) && galleryIndex >= 0 && galleryIndex < gallery.Count)
                        // Community text replacements are not applied: they could rewrite interface strings.
                        ApplyThemeDocument(gallery[galleryIndex] with { TextReplacements = null }, "COMMUNITY GALLERY  ·  LIVE PREVIEW");
                    break;
                case "gallerySubmit":
                    Process.Start(new ProcessStartInfo(ThemeGallery.SubmitUrl(CurrentThemeDocument())) { UseShellExecute = true });
                    break;
                case "minimize": Minimize(); break;
                case "maximize": ToggleMaximize(); break;
                case "close": Close(); break;
                case "drag":
                    if (WindowState == FormWindowState.Maximized) RestoreUnderCursor();
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                    break;
                default: return;
            }
            if (type is "preset" or "name" or "color" or "cutoff" or "text" or "font" or "themePanels" or "reset" or "import" or
                "applyShareCode" or "historyLoad" or "galleryUse" or "reapply")
                ScheduleSaveSession();
            PublishWebState();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Web editor message failed: " + ex);
            Log("ERROR · " + ex.Message);
        }
    }

    /// <summary>
    /// One color edit changes several native controls, each of which asks to publish; coalesce them into
    /// a single state message sent when the current UI message finishes.
    /// </summary>
    private void PublishWebState()
    {
        if (publishQueued || !webEditorReady || IsDisposed || Disposing) return;
        publishQueued = true;
        BeginInvoke(PublishWebStateNow);
    }

    private void PublishWebStateNow()
    {
        publishQueued = false;
        if (!webEditorReady || IsDisposed || Disposing || webEditor is null || webEditor.IsDisposed) return;
        var installations = knownInstalls;
        var state = new
        {
            type = "state",
            ack = lastSeq,
            maximized = WindowState == FormWindowState.Maximized,
            installStatus = webInstallStatus,
            installDetail = webInstallDetail,
            installStage = webInstallStage,
            themeName = themeName.Text,
            presetIndex = preset.SelectedIndex,
            presets = BuiltInPresets.Select(item => item.Label).ToArray(),
            presetPreviews = BuiltInPresets.Select(item => new
            {
                label = item.Label,
                colors = new[] { item.Settings.Background, item.Settings.Panel, item.Settings.Raised,
                    item.Settings.Text, item.Settings.Primary, item.Settings.Secondary, item.Settings.Danger }
                    .Select(color => $"#{color.R:X2}{color.G:X2}{color.B:X2}").ToArray()
            }).ToArray(),
            colors = colorBoxes.ToDictionary(item => item.Key, item => item.Value.Text),
            cutoff = cutoff.Value,
            source = source.Text,
            target = target.Text,
            fonts = fontChoice.Items.ToArray(),
            font = fontChoice.Text,
            themePanels = themePanels.Checked,
            textReplacements = textReplacements.Text,
            importStatus = importStatus.Text,
            panelStatus = panelStatus.Text,
            panelDetails = panelDetails.Text,
            log = log.Text,
            version = ApplicationLifetime.DisplayVersion(),
            installations = installations.Select(item => new
            {
                path = item.DllPath,
                name = item.DisplayName,
                version = item.Version.Major == 0 ? "Unknown" : item.Version.ToString(),
                hasCompanion = item.HasNativeCompanion,
                source = item.DiscoverySource
            }).ToArray(),
            shareCode = TryShareCode(),
            navigate,
            aep = AepState(),
            installAll,
            history = ThemeHistory.Load(HistoryFile).History.Select(HistoryView).OfType<object>().ToArray(),
            replaced = replacedThemes.Select(item => new
            {
                target = item.Target,
                install = AfterEffectsCatalog.Describe(item.Target)?.DisplayName ?? item.Target,
                name = TryParse(item.Theme)?.Name ?? "Your theme"
            }).ToArray(),
            gallery = new
            {
                status = galleryStatus,
                error = galleryError,
                items = gallery.Select(item => new { name = item.Name, author = item.Author ?? string.Empty, colors = HexColors(item.Settings) }).ToArray()
            },
            bugReport = webBugReport is null ? null : new
            {
                summary = webBugReport.Summary,
                bundlePath = webBugReport.BundlePath
            }
        };
        try { webEditor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(state)); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            AppDiagnostics.Write("Web editor state update skipped: " + ex.Message);
        }
    }

    /// <summary>
    /// Themes standalone DLL files (for example a dvaui.dll copied from another PC's After Effects) into a new
    /// folder beside them. Modified files whose Adobe signature no longer validates are allowed after a warning;
    /// the selected files are never changed, so they remain the backup for wherever the themed copies go.
    /// </summary>
    private void ThemeDllFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose dvaui.dll, and AfterFXLib.dll if you have it, to theme",
            Filter = "After Effects interface DLLs|*.dll",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        static bool IsCompanion(string path) => Path.GetFileName(path).StartsWith("AfterFXLib", StringComparison.OrdinalIgnoreCase);
        var files = dialog.FileNames;
        if (files.Count(path => !IsCompanion(path)) > 1 || files.Count(IsCompanion) > 1)
            throw new InvalidOperationException("Choose one dvaui.dll and, optionally, one AfterFXLib.dll from the same After Effects version.");

        var unverified = files.Where(path => !OriginalDllStore.IsAdobeOriginal(path)).Select(Path.GetFileName).ToList();
        if (unverified.Count > 0 && MessageBox.Show(this,
                $"{string.Join(" and ", unverified)} {(unverified.Count == 1 ? "is not" : "are not")} an untouched Adobe original: " +
                "the Adobe signature check failed, so the file was probably modified or themed before.\n\n" +
                "AfterThemed can theme it, but cannot restore Adobe's original for it. The file you chose is not changed; " +
                "keep it as the backup for the After Effects you put the themed copy into.\n\nTheme it anyway?",
                "Unverified DLL", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var parent = Path.GetDirectoryName(Path.GetFullPath(files[0]))!;
        var folder = Path.Combine(parent, $"AfterThemed - {SafeName()}");
        for (var n = 2; Directory.Exists(folder); n++) folder = Path.Combine(parent, $"AfterThemed - {SafeName()} {n}");
        Directory.CreateDirectory(folder);
        var settings = ReadSettings();
        foreach (var file in files.OrderBy(IsCompanion))
        {
            var output = Path.Combine(folder, IsCompanion(file) ? "AfterFXLib.dll" : "dvaui.dll");
            var hash = IsCompanion(file)
                ? LegacyAeThemePatcher.Generate(file, output, settings)
                : ThemePatcher.Generate(file, output, settings, ReadFontFamily(), ReadTextReplacements());
            Log($"Themed {Path.GetFileName(file)} · {output}\r\nSHA-256: {hash}");
        }
        File.WriteAllText(Path.Combine(folder, "HOW TO INSTALL.txt"), string.Join(Environment.NewLine,
            $"AfterThemed theme \"{themeName.Text}\"",
            "",
            "1. On the PC where these files go, close After Effects.",
            "2. Open that After Effects version's Support Files folder, for example",
            @"   C:\Program Files\Adobe\Adobe After Effects 2021\Support Files",
            "3. Back up its dvaui.dll" + (files.Any(IsCompanion) ? " and AfterFXLib.dll" : string.Empty) + " (copy them somewhere safe).",
            "4. Copy the files from this folder over them (Windows will ask for administrator permission).",
            "5. Open After Effects. To undo, copy the backups back.",
            "",
            "Only use these files with the same After Effects build the originals came from.",
            ""));
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    /// <summary>Opens the file Explorer launched AfterThemed with, once the editor is listening.</summary>
    private void OpenPendingFile()
    {
        if (pendingOpenFile is not { } file) return;
        pendingOpenFile = null;
        if (Path.GetExtension(file).Equals(ThemeDocuments.Extension, StringComparison.OrdinalIgnoreCase))
        {
            Try(() =>
            {
                ApplyThemeDocument(ThemeDocuments.Parse(File.ReadAllText(file)),
                    $"{Path.GetFileName(file).ToUpperInvariant()}  ·  AFTERTHEMED THEME");
                SaveSession();
            });
            Navigate("themes");
        }
        else
        {
            AddAepFiles([file]);
            Navigate("downgrader");
        }
    }

    /// <summary>Asks the web editor to switch tabs; the id lets it act on each request once.</summary>
    private void Navigate(string tab) => navigate = new { tab, id = ++navigateId };

    /// <summary>Remembers the theme being edited so the next launch continues where the user left off.</summary>
    private void SaveSession()
    {
        if (suppressStartupPrompts) return;
        try
        {
            var root = new JsonObject
            {
                ["presetIndex"] = preset.SelectedIndex,
                ["theme"] = JsonNode.Parse(ThemeDocuments.Serialize(CurrentThemeDocument(), includeThumbnail: false))
            };
            Directory.CreateDirectory(DataRoot);
            File.WriteAllText(SessionFile + ".tmp", root.ToJsonString());
            File.Move(SessionFile + ".tmp", SessionFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            AppDiagnostics.Write("Session not saved: " + ex.Message);
        }
    }

    private void RestoreSession()
    {
        preset.SelectedIndex = 5;
        if (!File.Exists(SessionFile)) return;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(SessionFile)) as JsonObject
                       ?? throw new InvalidDataException("The saved session is empty.");
            var document = ThemeDocuments.Read(root["theme"] ?? throw new InvalidDataException("The saved session has no theme."));
            var presetIndex = root["presetIndex"] is JsonValue value && value.TryGetValue<int>(out var index) ? index : -1;
            if (presetIndex >= 0 && presetIndex < BuiltInPresets.Length)
            {
                // An unedited built-in palette stays selected, so Reset palette keeps working.
                preset.SelectedIndex = presetIndex;
                themeName.Text = document.Name;
                cutoff.Value = Math.Clamp((int)Math.Round(document.Settings.TextCutoff * 100), cutoff.Minimum, cutoff.Maximum);
                if (document.Font is { } font && fontChoice.Items.FindIndex(item => item == font) is var fontIndex and >= 0)
                    fontChoice.SelectedIndex = fontIndex;
                if (document.TextReplacements is { } replacements) textReplacements.Text = replacements;
                if (document.ThemePanels is { } panels) themePanels.Checked = panels;
            }
            else ApplyThemeDocument(document, "RESTORED FROM YOUR LAST SESSION");
            Log($"Restored your last session · {document.Name}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Log("Your last session could not be restored; starting from a built-in palette. " + ex.Message);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSession();
        base.OnFormClosing(e);
    }

    private ThemeDocument CurrentThemeDocument() => new(themeName.Text, ReadSettings())
    {
        Font = fontChoice.Text == AdobeDefaultFont ? null : fontChoice.Text,
        TextReplacements = textReplacements.Text,
        ThemePanels = themePanels.Checked
    };

    private void ApplyThemeDocument(ThemeDocument document, string status)
    {
        preset.SelectedIndex = BuiltInPresets.Length;
        themeName.Text = document.Name;
        importedColors = Array.Empty<Color>();
        importedSettings = document.Settings;
        SetColors(document.Settings);
        importStatus.Text = status;
        if (document.Font is { } font && fontChoice.Items.FindIndex(item => item == font) is var fontIndex and >= 0)
            fontChoice.SelectedIndex = fontIndex;
        if (document.TextReplacements is { } replacements) textReplacements.Text = replacements;
        if (document.ThemePanels is { } panels) themePanels.Checked = panels;
        Log($"Loaded theme · {document.Name}");
    }

    private void ApplyHistory(string id)
    {
        var entry = ThemeHistory.Load(HistoryFile).History.Find(item => item.Id == id)
            ?? throw new InvalidOperationException("That theme is no longer in the history.");
        ApplyThemeDocument(ThemeDocuments.Parse(entry.Theme), $"FROM HISTORY  ·  {entry.InstalledAt.ToLocalTime():g}");
    }

    private void ExportTheme(string thumbnail)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export theme",
            Filter = "AfterThemed theme (*.afterthemed)|*.afterthemed",
            FileName = SafeName() + ThemeDocuments.Extension
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dialog.FileName, ThemeDocuments.Serialize(CurrentThemeDocument() with { Thumbnail = thumbnail }));
        Log($"Exported {dialog.FileName}");
    }

    private async void LoadGallery()
    {
        if (galleryStatus == "loading") return;
        galleryStatus = "loading";
        PublishWebState();
        try
        {
            gallery = await ThemeGallery.LoadAsync();
            galleryStatus = "ready";
            galleryError = string.Empty;
        }
        catch (Exception ex)
        {
            galleryStatus = "error";
            galleryError = ex is HttpRequestException or TaskCanceledException
                ? "The gallery could not be reached. Check your connection and try again."
                : ex.Message;
            AppDiagnostics.Write("Gallery load failed: " + ex);
        }
        if (!IsDisposed && !Disposing) PublishWebState();
    }

    private string? TryShareCode()
    {
        try { return ThemeDocuments.ToShareCode(CurrentThemeDocument()); }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException) { return null; }
    }

    private static ThemeDocument? TryParse(string json)
    {
        try { return ThemeDocuments.Parse(json); }
        catch (Exception ex) when (ex is InvalidDataException or JsonException) { return null; }
    }

    private static object? HistoryView(HistoryEntry entry) => TryParse(entry.Theme) is { } theme ? new
    {
        id = entry.Id,
        name = theme.Name,
        installedAt = entry.InstalledAt.ToString("o"),
        targets = entry.Targets.Select(path => AfterEffectsCatalog.Describe(path)?.DisplayName ?? Path.GetFileName(path)).ToArray(),
        colors = HexColors(theme.Settings)
    } : null;

    private static string[] HexColors(ThemeSettings settings) => ThemeDocuments.Colors(settings).Select(ThemeDocuments.Hex).ToArray();

    private void BrowseForWebInstall()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select the installed dvaui.dll",
            Filter = "DVAUI DLL (dvaui.dll)|dvaui.dll|DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            FileName = target.Text.Trim()
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) ApplySelectedInstallation(dialog.FileName);
    }

    private void PrepareWebBugReport()
    {
        webBugReport = BugReportBuilder.Create(new BugReportContext(
            TargetDllPath: target.Text.Trim(),
            PreservedOriginalPath: source.Text.Trim(),
            DataRoot: DataRoot,
            ReportsDirectory: Reports,
            ThemeName: SafeName(),
            PresetName: preset.Text,
            LogText: log.Text));
        Log($"Diagnostics bundle written: {webBugReport.BundlePath}");
    }

    private void OpenWebBugIssue()
    {
        if (webBugReport is null) PrepareWebBugReport();
        Process.Start(new ProcessStartInfo(BugReportBuilder.IssueUrl(webBugReport!.Summary))
        {
            UseShellExecute = true
        });
    }

    private void ShowWebBugBundle()
    {
        if (webBugReport is null) PrepareWebBugReport();
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{webBugReport!.BundlePath}\"")
        {
            UseShellExecute = true
        });
    }

    private void CopyWebBugReport()
    {
        if (webBugReport is null) PrepareWebBugReport();
        Clipboard.SetText(webBugReport!.Summary);
    }

    private static void OpenWebLink(string link)
    {
        var target = link switch
        {
            "x" => "https://x.com/shonenvii",
            "youtube" => "https://youtube.com/shonenshwty",
            "instagram" => "https://instagram.com/ripshonen",
            "discord" => "https://discord.gg/blank",
            "legal" => Path.Combine(Application.StartupPath, "EULA.txt"),
            _ => throw new InvalidOperationException("Unknown AfterThemed link.")
        };
        if (link == "legal" && !File.Exists(target))
            throw new FileNotFoundException("EULA.txt was not found beside AfterThemed.exe.", target);
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    internal Task<string> RunWebSmokeScript(string script) =>
        webEditor!.CoreWebView2.ExecuteScriptAsync(script);

    internal bool WebSmokeNameMatches(string name) => themeName.Text == name;

    internal Task CaptureWebPreviewAsync(Stream destination) =>
        webEditor!.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, destination);
}
