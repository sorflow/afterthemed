using System.ComponentModel;

namespace DvauiThemeEditor;

/// <summary>A missing original is a recoverable workflow state, with search and verified import.</summary>
internal sealed class OriginalRecoveryForm : Form
{
    private readonly string target;
    private readonly string originals;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Label status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(190, 216, 228) };
    private readonly ListBox candidates = new()
    {
        Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false,
        BackColor = Color.FromArgb(17, 35, 43), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button recover = new() { Text = "Use selected backup", AutoSize = true, Enabled = false };
    private readonly Button browseFolder = new() { Text = "Search a folder…", AutoSize = true };
    private readonly Button browseFile = new() { Text = "Choose a DLL…", AutoSize = true };

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string? RecoveredOriginal { get; private set; }

    internal OriginalRecoveryForm(string targetPath, string originalsRoot, bool scanOnShow = true)
    {
        target = targetPath;
        originals = originalsRoot;
        Text = "Recover an After Effects original";
        BackColor = Color.FromArgb(8, 25, 33);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 490);
        MinimumSize = new Size(680, 440);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
        foreach (var height in new[] { 42, 67, 46 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Let’s find your original DLL", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "The installed DLL or saved snapshot could not be verified. A clean backup can recover your original without reinstalling After Effects.\nOnly Adobe-signed copies matching this exact DLL build are listed below.",
            Dock = DockStyle.Fill
        }, 0, 1);
        status.Text = scanOnShow ? "Searching your AfterThemed originals and backup folders…" : "Ready to search for a matching Adobe original.";
        layout.Controls.Add(status, 0, 2);
        layout.Controls.Add(candidates, 0, 3);
        layout.Controls.Add(new Label
        {
            Text = "Choose a DLL or search the Backups folder from an older portable copy of AfterThemed.\nIf no matching original survives, reinstall this AE version through Creative Cloud, then retry.",
            ForeColor = Color.FromArgb(190, 216, 228), Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0)
        }, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        layout.Controls.Add(buttons, 0, 5);
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([recover, browseFolder, browseFile, cancel]);
        foreach (Button button in buttons.Controls)
        {
            button.BackColor = Color.FromArgb(196, 233, 250);
            button.ForeColor = Color.FromArgb(8, 25, 33);
            button.FlatStyle = FlatStyle.Flat;
            button.Padding = new Padding(5, 2, 5, 2);
        }
        CancelButton = cancel;
        AcceptButton = recover;
        candidates.SelectedIndexChanged += (_, _) => recover.Enabled = candidates.SelectedItem is RecoveryCandidate;
        recover.Click += (_, _) => RecoverSelected();
        browseFolder.Click += async (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Select an older AfterThemed folder or backup folder", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) await Search([dialog.SelectedPath]);
        };
        browseFile.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "Choose a clean original DLL or backup", Filter = "DLLs and originals|*.dll;*.adobe-original;*.recovery|All files|*.*" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            SetBusy(true);
            try
            {
                var valid = await Task.Run(() => OriginalDllStore.IsVerifiedMatchingBackup(dialog.FileName, target));
                if (IsDisposed) return;
                if (valid)
                {
                    candidates.Items.Clear();
                    candidates.Items.Add(new RecoveryCandidate(dialog.FileName, OriginalDllStore.Sha256(dialog.FileName)));
                    candidates.SelectedIndex = 0;
                    status.Text = "Matching Adobe original found. Use selected backup to preserve it and continue.";
                }
                else status.Text = "That file failed Adobe verification or does not match the selected DLL build. Choose another backup.";
            }
            catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
            finally { if (!IsDisposed) SetBusy(false); }
        };
        FormClosed += (_, _) => cancellation.Cancel();
        if (scanOnShow) Shown += async (_, _) => await Search(OriginalRecovery.DefaultSearchRoots(originals));
    }

    private async Task Search(IEnumerable<string> roots)
    {
        SetBusy(true);
        candidates.Items.Clear();
        status.Text = "Searching and checking Adobe signatures…";
        try
        {
            var found = await Task.Run(() => OriginalRecovery.Find(target, roots, cancellation.Token));
            if (IsDisposed) return;
            foreach (var candidate in found) candidates.Items.Add(candidate);
            if (found.Count > 0) candidates.SelectedIndex = 0;
            status.Text = found.Count == 0
                ? "No matching Adobe original found in these locations. Try an older backup folder or choose a DLL."
                : $"{found.Count} matching original(s) found. Select a backup to preserve and continue.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        browseFile.Enabled = browseFolder.Enabled = candidates.Enabled = !busy;
        recover.Enabled = !busy && candidates.SelectedItem is RecoveryCandidate;
        UseWaitCursor = busy;
    }

    private void RecoverSelected()
    {
        if (candidates.SelectedItem is not RecoveryCandidate selected) return;
        SetBusy(true);
        try
        {
            // Recheck the selected file at import time; search results never authorize an unchecked copy.
            if (!string.Equals(OriginalDllStore.Sha256(selected.Path), selected.Hash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("That backup changed after the search. Search again before recovery.");
            RecoveredOriginal = OriginalDllStore.ImportVerifiedOriginal(target, originals, selected.Path);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { if (!IsDisposed) SetBusy(false); }
    }
}
