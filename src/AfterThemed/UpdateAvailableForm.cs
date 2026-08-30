using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace DvauiThemeEditor;

internal sealed class UpdateAvailableForm : Form
{
    private static readonly Color Surface = Color.FromArgb(2, 4, 5);
    private static readonly Color Outline = Color.FromArgb(24, 49, 57);
    private static readonly Color Primary = Color.FromArgb(248, 250, 251);
    private static readonly Color Secondary = Color.FromArgb(181, 194, 200);

    private readonly UpdateInfo update;

    internal UpdateAvailableForm(UpdateInfo update)
    {
        this.update = update;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Surface;
        ClientSize = new Size(520, 246);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "AfterThemed update available";

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
            Bounds = new Rectangle(24, 24, 42, 42),
            AccessibleName = "AfterThemed SVG logo"
        });

        AddLabel("Update available", new Rectangle(78, 28, 380, 30), 15f, FontStyle.Bold, Primary);
        AddLabel($"AfterThemed {update.TagName} is ready. You are running {ApplicationLifetime.DisplayVersion()}.",
            new Rectangle(78, 62, 410, 24), 8.5f, FontStyle.Regular, Secondary);
        AddLabel("Download the latest installer from GitHub, then close AfterThemed before running it.",
            new Rectangle(24, 112, 472, 44), 9f, FontStyle.Regular, Primary);

        AddButton("DOWNLOAD UPDATE", new Rectangle(24, 188, 178, 34), OpenDownload, accent: true);
        AddButton("VIEW RELEASE", new Rectangle(214, 188, 132, 34), OpenRelease);
        AddButton("LATER", new Rectangle(358, 188, 138, 34), Close);
    }

    private void OpenDownload()
    {
        OpenUrl(update.DownloadUrl);
        Close();
    }

    private void OpenRelease()
    {
        OpenUrl(update.ReleasePageUrl);
        Close();
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Unable to open GitHub",
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
