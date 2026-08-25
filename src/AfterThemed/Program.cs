namespace DvauiThemeEditor;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 7 && args[0] == "--install-with-panel-apply")
        {
            var installed = Install(args[1], args[2], args[3]);
            return installed == 0
                ? PanelThemeManager.ApplyFromConfiguration(args[2], args[4], args[5], args[6])
                : installed;
        }

        if (args.Length == 6 && args[0] == "--install-with-panel-restore")
        {
            var installed = Install(args[1], args[2], args[3]);
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

        if (args.Length == 4 && args[0] == "--install")
            return Install(args[1], args[2], args[3]);

        if (args.Length == 4 && args[0] == "--install-smoke")
            return Install(args[1], args[2], args[3], requireAfterEffectsClosed: false);

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

    private static int Install(string source, string target, string backupDirectory, bool requireAfterEffectsClosed = true)
    {
        string? temporaryPath = null;
        string? backupPath = null;
        try
        {
            if (requireAfterEffectsClosed && System.Diagnostics.Process.GetProcessesByName("AfterFX").Length > 0)
                return 3;

            source = Path.GetFullPath(source);
            target = Path.GetFullPath(target);
            if (!File.Exists(source) || !File.Exists(target))
                return 2;

            var expectedHash = OriginalDllStore.Sha256(source);
            Directory.CreateDirectory(backupDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            backupPath = Path.Combine(backupDirectory, $"dvaui-{stamp}.dll");
            File.Copy(target, backupPath, false);
            if (!string.Equals(OriginalDllStore.Sha256(target), OriginalDllStore.Sha256(backupPath), StringComparison.Ordinal))
                return 2;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            temporaryPath = Path.Combine(Path.GetDirectoryName(target)!, $"dvaui.afterthemed-{Guid.NewGuid():N}.tmp");
            File.Copy(source, temporaryPath, false);
            if (!string.Equals(expectedHash, OriginalDllStore.Sha256(temporaryPath), StringComparison.Ordinal))
                return 2;

            File.Move(temporaryPath, target, true);
            temporaryPath = null;
            if (!string.Equals(expectedHash, OriginalDllStore.Sha256(target), StringComparison.Ordinal))
            {
                File.Copy(backupPath, target, true);
                return 2;
            }
            return 0;
        }
        catch
        {
            if (backupPath is not null && File.Exists(backupPath) && File.Exists(target))
            {
                try { File.Copy(backupPath, target, true); } catch { /* Preserve the original failure code. */ }
            }
            return 2;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { /* Best-effort cleanup of a staging file. */ }
            }
        }
    }
}
