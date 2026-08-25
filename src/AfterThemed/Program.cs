namespace DvauiThemeEditor;

static class Program
{
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

            using var form = new Form1();
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
