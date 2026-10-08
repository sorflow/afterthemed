using System.Runtime.InteropServices;

namespace DvauiThemeEditor;

static class Program
{
    /// <summary>
    /// AfterThemed is a WinExe, so it owns no console. Console-mode diagnostics have to borrow the
    /// console of whichever shell launched them or their output goes nowhere.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    private static void UseParentConsole()
    {
        const int attachParentProcess = -1;
        if (!AttachConsole(attachParentProcess)) return;
        var standardOutput = Console.OpenStandardOutput();
        var writer = new StreamWriter(standardOutput) { AutoFlush = true };
        Console.SetOut(writer);
    }

    [STAThread]
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppDiagnostics.Write("Unhandled failure: " + e.ExceptionObject);
        Application.ThreadException += (_, e) =>
        {
            AppDiagnostics.Write("UI failure: " + e.Exception);
            MessageBox.Show("AfterThemed encountered an unexpected error. Close and reopen it before applying further changes. " +
                "The error details were saved in the AfterThemed Logs folder. " + e.Exception.Message,
                "AfterThemed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        };
        using var upgradeMutex = ApplicationLifetime.HoldUpgradeMutex();

        if (args.Length == 4 && args[0] == "--recover-original")
        {
            UseParentConsole();
            try
            {
                Console.WriteLine(OriginalDllStore.ImportVerifiedOriginal(args[1], args[2], args[3]));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 12;
            }
        }

        if (args.Length == 2 && args[0] == "--organize-originals")
        {
            UseParentConsole();
            if (OriginalLibraryLayout.OtherAppIsOpen())
            {
                Console.WriteLine("Close other AfterThemed windows before organizing originals.");
                return 10;
            }
            var result = OriginalLibraryLayout.Organize(args[1]);
            Console.WriteLine($"Organized {result.Moved} snapshots by After Effects release.");
            foreach (var warning in result.Warnings) Console.WriteLine(warning);
            return result.Warnings.Count == 0 ? 0 : 11;
        }

        if (args.Length == 6 && args[0] == "--install-theme-set-with-panel-apply")
        {
            var installed = ThemeFileSetInstaller.Run(args[1], args[2]);
            if (installed != 0) return installed;
            var manifest = ThemeFileSetStore.ReadManifest(args[1]);
            // Panels are shared by every version; they are themed once, against the first installation listed.
            var nativeTarget = manifest.Files.First(file =>
                string.Equals(Path.GetFileName(file.TargetPath), "dvaui.dll", StringComparison.OrdinalIgnoreCase));
            return PanelThemeManager.ApplyFromConfiguration(
                nativeTarget.TargetPath, args[3], args[4], args[5]);
        }

        if (args.Length == 5 && args[0] == "--install-theme-set-with-panel-restore")
        {
            var installed = ThemeFileSetInstaller.Run(args[1], args[2]);
            if (installed != 0) return installed;
            return PanelThemeManager.RestoreFromBackups(args[3], args[4]);
        }

        if (args.Length == 3 && args[0] == "--install-theme-set")
            return ThemeFileSetInstaller.Run(args[1], args[2]);

        if (args.Length is 7 or 8 && args[0] == "--install-with-panel-apply")
        {
            var installed = RunNativeInstall(args[1], args[2], args[3],
                args.Length == 8 ? args[7] : null);
            return installed == 0
                ? PanelThemeManager.ApplyFromConfiguration(args[2], args[4], args[5], args[6])
                : installed;
        }

        if (args.Length is 6 or 7 && args[0] == "--install-with-panel-restore")
        {
            var installed = RunNativeInstall(args[1], args[2], args[3],
                args.Length == 7 ? args[6] : null);
            return installed == 0 ? PanelThemeManager.RestoreFromBackups(args[4], args[5]) : installed;
        }

        if (args.Length == 5 && args[0] == "--apply-panels")
            return PanelThemeManager.ApplyFromConfiguration(args[1], args[2], args[3], args[4]);

        if (args.Length == 3 && args[0] == "--restore-panels")
            return PanelThemeManager.RestoreFromBackups(args[1], args[2]);

        if (args.Length == 1 && args[0] == "--panel-smoke")
            return PanelThemeManager.RunSmokeTest() ? 0 : 9;

        if (args.Length == 4 && args[0] == "--downgrade-aep" && int.TryParse(args[3], out var aepTarget))
        {
            UseParentConsole();
            try
            {
                var result = AepDowngrader.Downgrade(args[1], args[2], aepTarget);
                Console.WriteLine($"{result.Source} → {result.Target}: {result.OutputPath}");
                foreach (var change in result.Changes) Console.WriteLine("  " + change);
                return 0;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        // Support aid: prints what the detection engine sees, so a user can report their real
        // layout without screenshots when an installation is missed.
        if (args.Length == 1 && args[0] == "--list-installs")
        {
            UseParentConsole();
            var installs = AfterEffectsCatalog.Discover();
            Console.WriteLine($"{installs.Count} After Effects installation(s) detected.");
            foreach (var install in installs)
                Console.WriteLine(
                    $"  {install.DisplayName}\n" +
                    $"    dvaui.dll : {install.DllPath}\n" +
                    $"    version   : {install.Version}\n" +
                    $"    companion : {(install.HasNativeCompanion ? install.CompanionPath : "none")}\n" +
                    $"    found via : {install.DiscoverySource}");
            return installs.Count > 0 ? 0 : 1;
        }

        if (args.Length is 2 or 3 && args[0] == "--ui-snapshot")
        {
            ApplicationConfiguration.Initialize();
            if (args.Length == 3 && args[2].Equals("RECOVER ORIGINAL", StringComparison.OrdinalIgnoreCase))
                return SnapshotDialog(new OriginalRecoveryForm("dvaui.dll", "Originals", scanOnShow: false), args[1]);
            if (args.Length == 3 && args[2].Equals("ABOUT AFTERTHEMED", StringComparison.OrdinalIgnoreCase))
            {
                using var about = new AboutAfterThemedForm
                {
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000)
                };
                about.Show();
                Application.DoEvents();
                about.PerformLayout();
                using var aboutBitmap = new Bitmap(about.ClientSize.Width, about.ClientSize.Height);
                about.DrawToBitmap(aboutBitmap, new Rectangle(Point.Empty, about.ClientSize));
                aboutBitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                about.Hide();
                return 0;
            }

            if (args.Length == 3 && args[2].Equals("SELECT AFTER EFFECTS", StringComparison.OrdinalIgnoreCase))
                return SnapshotDialog(new AfterEffectsPickerForm(AfterEffectsCatalog.Discover(), null), args[1]);

            if (args.Length == 3 && args[2].Equals("REPORT BUG", StringComparison.OrdinalIgnoreCase))
            {
                var previewRoot = Path.Combine(Path.GetTempPath(), $"afterthemed-bug-preview-{Guid.NewGuid():N}");
                var bundle = BugReportBuilder.Create(new BugReportContext(
                    AfterEffectsCatalog.Discover().FirstOrDefault()?.DllPath, null, previewRoot,
                    Path.Combine(previewRoot, "Reports"), "Preview-Theme", "Nord",
                    "[00:00:00]  preview log line"));
                return SnapshotDialog(new BugReportForm(bundle), args[1]);
            }

            if (args.Length == 3 && args[2].Equals("UPDATE AVAILABLE", StringComparison.OrdinalIgnoreCase))
            {
                var current = UpdateChecker.CurrentVersion();
                var next = new Version(current.Major, current.Minor, Math.Max(0, current.Build) + 1);
                return SnapshotDialog(new UpdateAvailableForm(new UpdateInfo(
                    next, $"v{next}",
                    $"https://github.com/sorflow/afterthemed/releases/tag/v{next}",
                    $"https://github.com/sorflow/afterthemed/releases/download/v{next}/AfterThemed-Setup-{next}.exe")),
                    args[1]);
            }

            using var form = new Form1(suppressStartupPrompts: true);
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.Show();
            Application.DoEvents();
            if (args.Length == 3)
            {
                var pageButton = FindControlByText(form, args[2]) as Button;
                pageButton?.PerformClick();
                Application.DoEvents();
            }
            form.PerformLayout();
            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            form.Hide();
            return 0;
        }

        if (args.Length == 1 && args[0] == "--web-ui-smoke")
        {
            UseParentConsole();
            ApplicationConfiguration.Initialize();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            using var form = new Form1(suppressStartupPrompts: true, forceWebUi: true)
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000)
            };
            form.Show();
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!form.WebEditorBridgeReady && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            if (!form.WebEditorBridgeReady)
            {
                Console.WriteLine("Web editor did not establish its native bridge.");
                return 13;
            }
            const string probeName = "Web bridge smoke";
            var injection = form.RunWebSmokeScript(
                "window.chrome.webview.postMessage({type:'name',value:'Web bridge smoke'})");
            deadline = DateTime.UtcNow.AddSeconds(15);
            while ((!injection.IsCompleted || !form.WebSmokeNameMatches(probeName)) && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            var connected = injection.IsCompletedSuccessfully && form.WebSmokeNameMatches(probeName);
            if (connected)
            {
                var reading = form.RunWebSmokeScript("document.getElementById('theme-name')?.value");
                while (!reading.IsCompleted && DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(25);
                }
                connected = reading.IsCompletedSuccessfully &&
                    System.Text.Json.JsonSerializer.Deserialize<string>(reading.Result) == probeName;
            }
            if (connected)
            {
                bool ScriptEquals(string script, string expected)
                {
                    var task = form.RunWebSmokeScript(script);
                    var until = DateTime.UtcNow.AddSeconds(10);
                    while (!task.IsCompleted && DateTime.UtcNow < until)
                    {
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }
                    return task.IsCompletedSuccessfully &&
                        System.Text.Json.JsonSerializer.Deserialize<string>(task.Result) == expected;
                }

                bool OpenDialogAndCheck(string buttonText, string title)
                {
                    var clicked = ScriptEquals(
                        $"(() => {{ const button = [...document.querySelectorAll('button')].find(b => b.textContent?.trim() === '{buttonText}'); button?.click(); return button?.textContent?.trim(); }})()",
                        buttonText);
                    Application.DoEvents();
                    Thread.Sleep(100);
                    return clicked && ScriptEquals("document.querySelector('[role=dialog] h2')?.textContent", title);
                }

                var installDialogPassed = OpenDialogAndCheck("Choose installation", "Choose installation");
                ScriptEquals("(() => { document.querySelector('button[aria-label=\"Close dialog\"]')?.click(); return 'closed'; })()", "closed");
                Application.DoEvents();
                Thread.Sleep(100);
                var aboutDialogPassed = OpenDialogAndCheck("About", "About AfterThemed");
                ScriptEquals("(() => { document.querySelector('button[aria-label=\"Close dialog\"]')?.click(); return 'closed'; })()", "closed");
                Application.DoEvents();
                Thread.Sleep(100);
                var appearanceOpened = ScriptEquals(
                    "(() => { document.querySelector('button[aria-haspopup=\"menu\"]')?.click(); return 'opened'; })()", "opened");
                Application.DoEvents();
                Thread.Sleep(100);
                var appearanceSelected = ScriptEquals(
                    "(() => { const button = [...document.querySelectorAll('[role=menuitemradio]')].find(b => b.textContent?.includes('Light')); button?.click(); return button?.textContent?.trim(); })()", "Light");
                Application.DoEvents();
                Thread.Sleep(100);
                var themePassed = appearanceOpened && appearanceSelected &&
                    ScriptEquals("document.documentElement.dataset.uiTheme", "light");
                connected &= installDialogPassed && aboutDialogPassed && themePassed;
                Console.WriteLine($"Dialogs {(installDialogPassed && aboutDialogPassed ? "passed" : "failed")} · appearance {(themePassed ? "passed" : "failed")}.");

                bool ClickWindowControl(string label, Func<bool> result)
                {
                    var click = form.RunWebSmokeScript(
                        $"(() => {{ const b = document.querySelector('button[aria-label=\"{label}\"]'); const r = b.getBoundingClientRect(); document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2)?.closest('button')?.click(); }})()");
                    var until = DateTime.UtcNow.AddSeconds(5);
                    while ((!click.IsCompleted || !result()) && DateTime.UtcNow < until)
                    {
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }
                    return result();
                }
                var windowControlsPassed = ClickWindowControl("Minimize", () => form.WindowState == FormWindowState.Minimized);
                form.WindowState = FormWindowState.Normal;
                windowControlsPassed &= ClickWindowControl("Maximize", () => form.WindowState == FormWindowState.Maximized);
                windowControlsPassed &= ClickWindowControl("Maximize", () => form.WindowState == FormWindowState.Normal);
                windowControlsPassed &= ClickWindowControl("Close", () => form.IsDisposed);
                connected &= windowControlsPassed;
                Console.WriteLine(windowControlsPassed ? "Window controls passed." : "Window controls failed.");
            }
            Console.WriteLine(connected
                ? "Web editor loaded and exchanged state with the native bridge."
                : "Web editor loaded but did not exchange state with the native bridge.");
            return connected ? 0 : 13;
        }

        if (args.Length is 2 or 3 && args[0] == "--web-ui-snapshot")
        {
            ApplicationConfiguration.Initialize();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            using var form = new Form1(suppressStartupPrompts: true, forceWebUi: true)
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000)
            };
            form.Show();
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!form.WebEditorBridgeReady && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            if (!form.WebEditorBridgeReady) return 14;

            if (args.Length == 3)
            {
                var view = args[2].Trim().ToLowerInvariant();
                var script = view switch
                {
                    "dark" or "light" or "mocha" =>
                        "document.querySelector('button[aria-haspopup=\"menu\"]')?.click()",
                    "install" => "[...document.querySelectorAll('button')].find(b => b.textContent?.includes('Choose installation'))?.click()",
                    "about" => "[...document.querySelectorAll('button')].find(b => b.textContent?.trim() === 'About')?.click()",
                    "report" => "[...document.querySelectorAll('button')].find(b => b.textContent?.includes('Report bug'))?.click()",
                    "activity" => "document.querySelector('.activity-trigger')?.click()",
                    "color-picker" => "document.querySelector('.apple-color-trigger')?.click()",
                    _ => string.Empty
                };
                if (script.Length > 0)
                {
                    var setup = form.RunWebSmokeScript(script);
                    var setupDeadline = DateTime.UtcNow.AddSeconds(15);
                    while (!setup.IsCompleted && DateTime.UtcNow < setupDeadline)
                    {
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }
                    if (!setup.IsCompletedSuccessfully) return 14;
                    if (view is "dark" or "light" or "mocha")
                    {
                        Application.DoEvents();
                        Thread.Sleep(100);
                        var chooseTheme = form.RunWebSmokeScript(
                            $"[...document.querySelectorAll('[role=menuitemradio]')].find(b => b.textContent?.toLowerCase().includes('{view}'))?.click()");
                        while (!chooseTheme.IsCompleted && DateTime.UtcNow < setupDeadline)
                        {
                            Application.DoEvents();
                            Thread.Sleep(25);
                        }
                        if (!chooseTheme.IsCompletedSuccessfully) return 14;
                        Application.DoEvents();
                        Thread.Sleep(100);
                    }
                    if (view == "report")
                    {
                        while (DateTime.UtcNow < setupDeadline)
                        {
                            Application.DoEvents();
                            var ready = form.RunWebSmokeScript("Boolean(document.querySelector('.report-preview'))");
                            while (!ready.IsCompleted && DateTime.UtcNow < setupDeadline)
                            {
                                Application.DoEvents();
                                Thread.Sleep(25);
                            }
                            if (ready.IsCompletedSuccessfully && ready.Result == "true") break;
                            Thread.Sleep(25);
                        }
                    }
                }
            }

            var imagePath = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            using var image = File.Create(imagePath);
            var capture = form.CaptureWebPreviewAsync(image);
            deadline = DateTime.UtcNow.AddSeconds(15);
            while (!capture.IsCompleted && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }
            if (!capture.IsCompletedSuccessfully) return 14;
            image.Flush();
            return 0;
        }

        if (args.Length is 4 or 5 && args[0] == "--install")
            return RunNativeInstall(args[1], args[2], args[3], args.Length == 5 ? args[4] : null);

        if (args.Length is 4 or 5 && args[0] == "--install-smoke")
            return NativeDllInstallCommand.Run(args[1], args[2], args[3],
                args.Length == 5 ? args[4] : null, requireAfterEffectsClosed: false);

        if (args.Length == 3 && args[0] == "--smoke")
        {
            ThemePatcher.Generate(args[1], args[2], ThemeSettings.Cyberpunk, true);
            return 0;
        }

        if (args.Length == 3 && args[0] == "--material-lavender")
        {
            ThemePatcher.Generate(args[1], args[2], ThemeSettings.MaterialLavender, true);
            return 0;
        }

        if (args.Length == 3 && args[0] == "--material-lavender-rich")
        {
            ThemePatcher.Generate(args[1], args[2], ThemeSettings.MaterialLavenderRich, true);
            return 0;
        }

        if (args.Length == 3 && args[0] == "--hatsune-miku")
        {
            ThemePatcher.Generate(args[1], args[2], ThemeSettings.HatsuneMikuAccessible, true);
            return 0;
        }

        if (args.Length == 3 && args[0] == "--legacy-ae-hatsune")
        {
            LegacyAeThemePatcher.Generate(args[1], args[2], ThemeSettings.HatsuneMikuAccessible);
            return 0;
        }

        if (args.Length == 4 && args[0] == "--legacy-ae-hatsune-for-dvaui")
            return LegacyAeThemePatcher.GenerateForDvaui(
                args[1], args[2], args[3], ThemeSettings.HatsuneMikuAccessible) is null ? 8 : 0;
        if (args.Length == 4 && args[0] == "--import-smoke")
        {
            var imported = ThemeImporter.Load(args[2]);
            ThemePatcher.Generate(args[1], args[3], imported.Suggested, true);
            return imported.Colors.Count >= 2 ? 0 : 4;
        }

        if (args.Length == 3 && args[0] == "--text-smoke")
        {
            ThemePatcher.Generate(args[1], args[2], ThemeSettings.GruvboxDark, false,
                [new TextReplacement("AdobeClean-Regular", "TestFont-Regular")]);
            return 0;
        }

        if (args.Length == 4 && args[0] == "--font-smoke")
        {
            ThemePatcher.Generate(args[1], args[2], ThemeSettings.GruvboxDark, args[3]);
            return 0;
        }

        if (args.Length == 3 && args[0] == "--snapshot-smoke")
        {
            try
            {
                var original = OriginalDllStore.CaptureIfMissing(args[1], args[2], out _);
                return File.Exists(original) ? 0 : 5;
            }
            catch (InvalidDataException)
            {
                return 7;
            }
        }

        if (args.Length == 4 && args[0] == "--restore-smoke")
        {
            var restored = OriginalDllStore.CreateRestoreDll(args[1], args[2], args[3]);
            return File.Exists(restored) &&
                   string.Equals(OriginalDllStore.Sha256(restored),
                       OriginalDllStore.Sha256(OriginalDllStore.RequireExistingOriginal(args[1], args[2])),
                       StringComparison.Ordinal) ? 0 : 6;
        }

        ApplicationConfiguration.Initialize();
        // Explorer opens "AfterThemed.exe <theme.afterthemed>" and, from the .aep right-click verb,
        // "AfterThemed.exe --open-downgrader <project.aep>".
        var openFile = args.Length == 2 && args[0] == "--open-downgrader" ? args[1]
            : args.Length == 1 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0]
            : null;
        Application.Run(new Form1(startupFile: openFile));
        return 0;
    }

    /// <summary>Renders a modal offscreen so its layout can be reviewed without a user present.</summary>
    private static int SnapshotDialog(Form dialog, string imagePath)
    {
        using (dialog)
        {
            dialog.ShowInTaskbar = false;
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(-32000, -32000);
            dialog.Show();
            Application.DoEvents();
            dialog.PerformLayout();
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
            bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            dialog.Hide();
        }
        return 0;
    }

    private static Control? FindControlByText(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child.Text.Equals(text, StringComparison.OrdinalIgnoreCase)) return child;
            var nested = FindControlByText(child, text);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static int RunNativeInstall(string source, string target, string backupDirectory, string? reportPath)
        => NativeDllInstallCommand.Run(source, target, backupDirectory, reportPath);
}
