using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DvauiThemeEditor;

/// <summary>
/// The startup chooser. AfterThemed edits one installation at a time, and a machine with several
/// After Effects releases gives no safe way to guess which one the user means, so the target is
/// confirmed before anything is preserved or patched.
/// </summary>
internal sealed class AfterEffectsPickerForm : Form
{
    private static readonly Color Surface = Color.FromArgb(2, 4, 5);
    private static readonly Color Outline = Color.FromArgb(24, 49, 57);
    private static readonly Color Primary = Color.FromArgb(248, 250, 251);
    private static readonly Color Secondary = Color.FromArgb(181, 194, 200);
    private static readonly Color Accent = Color.FromArgb(92, 198, 250);

    private readonly List<AfterEffectsInstall> installs;
    private readonly MacComboBox chooser = new() { Dock = DockStyle.Fill };
    private readonly Label details = new();

    /// <summary>The dvaui.dll the user confirmed, or null when they cancelled.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string? SelectedDllPath { get; private set; }

    internal AfterEffectsPickerForm(IReadOnlyList<AfterEffectsInstall> discovered, string? preferredDllPath)
    {
        installs = [.. discovered];

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Surface;
        ClientSize = new Size(560, 326);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Select After Effects";

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        BuildContent(preferredDllPath);
        Resize += (_, _) => UpdateRoundedRegion();
        Shown += (_, _) => UpdateRoundedRegion();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        // Enter confirms, Escape cancels. The combo consumes Enter while its list is open.
        if (keyData == Keys.Escape)
        {
            Cancel();
            return true;
        }
        if (keyData == Keys.Enter && !chooser.IsOpen)
        {
            Confirm();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var outline = RoundedPanel.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 18);
        using var pen = new Pen(Outline, 1f);
        e.Graphics.DrawPath(pen, outline);
    }

    private void BuildContent(string? preferredDllPath)
    {
        Controls.Add(new AfterThemedMark
        {
            Bounds = new Rectangle(24, 24, 40, 40),
            AccessibleName = "AfterThemed SVG logo"
        });

        AddLabel("Choose an After Effects install", new Rectangle(76, 26, 440, 28), 15f, FontStyle.Bold, Primary);
        AddLabel(installs.Count == 0
                ? "No installation was detected on this PC."
                : $"{installs.Count} detected on this PC. AfterThemed themes one at a time.",
            new Rectangle(76, 55, 440, 20), 8.2f, FontStyle.Regular, Secondary);

        AddLabel("INSTALLATION", new Rectangle(24, 100, 200, 16), 7.2f, FontStyle.Bold, Secondary);

        var chooserHost = new Panel
        {
            Bounds = new Rectangle(24, 120, 512, 28),
            BackColor = Color.Transparent
        };
        chooser.BackColor = Color.FromArgb(13, 32, 41);
        chooser.ForeColor = Primary;
        chooser.AccessibleName = "Detected After Effects installations";
        foreach (var install in installs) chooser.Items.Add(LabelFor(install));
        chooser.SelectedIndexChanged += (_, _) => UpdateDetails();
        chooserHost.Controls.Add(chooser);
        Controls.Add(chooserHost);

        details.AutoSize = false;
        details.BackColor = Color.Transparent;
        details.Bounds = new Rectangle(24, 160, 512, 78);
        details.Font = UiFonts.Mono(7.8f);
        details.ForeColor = Secondary;
        details.UseCompatibleTextRendering = true;
        Controls.Add(details);

        AddButton("USE THIS INSTALL", new Rectangle(24, 258, 178, 34), Confirm, accent: true);
        AddButton("BROWSE FOR dvaui.dll…", new Rectangle(212, 258, 190, 34), Browse);
        AddButton("CANCEL", new Rectangle(412, 258, 124, 34), Cancel);

        AddLabel("You can change this later from INSTALLED TARGET.",
            new Rectangle(24, 300, 512, 18), 7.4f, FontStyle.Regular, Color.FromArgb(120, 138, 148));

        chooser.SelectedIndex = PreferredIndex(preferredDllPath);
        UpdateDetails();
    }

    private int PreferredIndex(string? preferredDllPath)
    {
        if (installs.Count == 0) return -1;
        if (!string.IsNullOrWhiteSpace(preferredDllPath))
        {
            var index = installs.FindIndex(install =>
                string.Equals(install.DllPath, preferredDllPath.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index >= 0) return index;
        }
        return 0;
    }

    private static string LabelFor(AfterEffectsInstall install)
    {
        var version = install.Version.Major == 0 ? "unknown version" : install.Version.ToString();
        return $"{install.DisplayName}   ·   dvaui {version}";
    }

    private void UpdateDetails()
    {
        var install = Selected();
        if (install is null)
        {
            details.Text = installs.Count == 0
                ? "Nothing was found in Program Files, on the other fixed drives, or in the\r\n" +
                  "uninstall registry. Use Browse to point AfterThemed at dvaui.dll directly."
                : string.Empty;
            return;
        }

        var companion = install.HasNativeCompanion
            ? "AfterFXLib.dll present · native frame colors included"
            : "No AfterFXLib.dll · this release keeps its colors in dvaui.dll";
        details.Text =
            $"{install.DllPath}\r\n\r\n" +
            $"{companion}\r\n" +
            $"Found via {install.DiscoverySource}";
    }

    private AfterEffectsInstall? Selected() =>
        chooser.SelectedIndex >= 0 && chooser.SelectedIndex < installs.Count
            ? installs[chooser.SelectedIndex]
            : null;

    private void Confirm()
    {
        var install = Selected();
        if (install is null)
        {
            Browse();
            return;
        }
        SelectedDllPath = install.DllPath;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select the installed dvaui.dll",
            Filter = "DVAUI DLL (dvaui.dll)|dvaui.dll|DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            FileName = Selected()?.DllPath ?? string.Empty
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SelectedDllPath = dialog.FileName;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel()
    {
        SelectedDllPath = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void AddLabel(string text, Rectangle bounds, float size, FontStyle style, Color color)
    {
        Controls.Add(new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Bounds = bounds,
            Font = UiFonts.Sans(size, style),
            ForeColor = color,
            Text = text,
            UseCompatibleTextRendering = true
        });
    }

    private void AddButton(string text, Rectangle bounds, Action action, bool accent = false)
    {
        var button = new MacButton
        {
            Text = text,
            Bounds = bounds,
            BackColor = accent ? UiPalette.Accent : UiPalette.PanelRaised,
            ForeColor = accent ? UiPalette.OnAccent : UiPalette.Text,
            HoverColor = accent ? UiPalette.AccentHover : UiPalette.PanelHover
        };
        button.Click += (_, _) => action();
        Controls.Add(button);
    }

    private void UpdateRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedPanel.RoundRect(ClientRectangle, 18);
        var next = new Region(path);
        var previous = Region;
        Region = next;
        previous?.Dispose();
    }
}
