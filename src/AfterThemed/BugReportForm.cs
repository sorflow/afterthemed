using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace DvauiThemeEditor;

/// <summary>
/// Shows the exact diagnostics before any of it leaves the machine, then hands the report to the
/// user's own browser and GitHub account. AfterThemed never posts on the user's behalf: that would
/// require shipping a credential inside a public download, and the user should see and edit a report
/// that carries their file paths before it becomes a public issue.
/// </summary>
internal sealed class BugReportForm : Form
{
    private static readonly Color Surface = Color.FromArgb(2, 4, 5);
    private static readonly Color Outline = Color.FromArgb(24, 49, 57);
    private static readonly Color Primary = Color.FromArgb(248, 250, 251);
    private static readonly Color Secondary = Color.FromArgb(181, 194, 200);

    private readonly BugReportBundle bundle;

    internal BugReportForm(BugReportBundle bundle)
    {
        this.bundle = bundle;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Surface;
        ClientSize = new Size(660, 520);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Report a bug";

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        BuildContent();
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
        if (keyData == Keys.Escape)
        {
            Close();
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

    private void BuildContent()
    {
        Controls.Add(new AfterThemedMark
        {
            Bounds = new Rectangle(24, 24, 40, 40),
            AccessibleName = "AfterThemed SVG logo"
        });

        AddLabel("Report a bug", new Rectangle(76, 26, 460, 28), 15f, FontStyle.Bold, Primary);
        AddLabel("Review what will be shared. Nothing is sent until you post the issue yourself.",
            new Rectangle(76, 55, 560, 20), 8.2f, FontStyle.Regular, Secondary);

        AddLabel("DIAGNOSTICS", new Rectangle(24, 92, 200, 16), 7.2f, FontStyle.Bold, Secondary);

        var preview = new TextBox
        {
            Bounds = new Rectangle(24, 112, 612, 306),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(13, 32, 41),
            ForeColor = Primary,
            BorderStyle = BorderStyle.None,
            Font = UiFonts.Mono(7.8f),
            Text = bundle.Summary,
            AccessibleName = "Diagnostics that will be shared"
        };
        preview.Select(0, 0);
        Controls.Add(preview);

        AddLabel("No Adobe binaries are included. dvaui.dll stays on your PC.",
            new Rectangle(24, 426, 612, 18), 7.6f, FontStyle.Bold, Color.FromArgb(92, 198, 250));
        // A file path is one unbreakable word, so a plain Label wraps it onto a clipped second line.
        var bundleLabel = AddLabel($"Bundle saved: {bundle.BundlePath}",
            new Rectangle(24, 446, 612, 18), 7.2f, FontStyle.Regular, Color.FromArgb(120, 138, 148));
        bundleLabel.AutoEllipsis = true;
        bundleLabel.TextAlign = ContentAlignment.MiddleLeft;

        AddButton("OPEN GITHUB ISSUE", new Rectangle(24, 472, 184, 34), OpenIssue, accent: true);
        AddButton("SHOW BUNDLE", new Rectangle(218, 472, 140, 34), ShowBundle);
        AddButton("COPY REPORT", new Rectangle(368, 472, 136, 34), CopyReport);
        AddButton("CLOSE", new Rectangle(514, 472, 122, 34), Close);
    }

    private void OpenIssue()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BugReportBuilder.IssueUrl(bundle.Summary))
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Unable to open GitHub",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowBundle()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{bundle.BundlePath}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Unable to open the bundle folder",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyReport()
    {
        try
        {
            Clipboard.SetText(bundle.Summary);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Unable to copy",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Label AddLabel(string text, Rectangle bounds, float size, FontStyle style, Color color)
    {
        var label = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Bounds = bounds,
            Font = UiFonts.Sans(size, style),
            ForeColor = color,
            Text = text,
            UseCompatibleTextRendering = true
        };
        Controls.Add(label);
        return label;
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
