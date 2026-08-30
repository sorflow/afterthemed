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
        using var upgradeMutex = ApplicationLifetime.HoldUpgradeMutex();

        if (args.Length == 6 && args[0] == "--install-theme-set-with-panel-apply")
        {
            var installed = ThemeFileSetInstaller.Run(args[1], args[2]);
            if (installed != 0) return installed;
            var manifest = ThemeFileSetStore.ReadManifest(args[1]);
            var nativeTarget = manifest.Files.Single(file =>
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
                return SnapshotDialog(new UpdateAvailableForm(new UpdateInfo(
                    new Version(1, 3, 13), "v1.3.13",
                    "https://github.com/sorflow/afterthemed/releases/tag/v1.3.13",
                    "https://github.com/sorflow/afterthemed/releases/download/v1.3.13/AfterThemed-Setup-1.3.13.exe")),
                    args[1]);

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
        Application.Run(new Form1());
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
            using var bitmap = new Bitmap(dialog.ClientSize.Width, dialog.ClientSize.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.ClientSize));
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
