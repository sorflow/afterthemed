using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DvauiThemeEditor;

/// <summary>
/// The update prompt in the editor's reference style: icy surface, electric-blue ink, light heading,
/// flat pill buttons. Corners come from Windows 11's DWM so they stay antialiased (no clipping Region).
/// </summary>
internal sealed class UpdateAvailableForm : Form
{
    internal static readonly Color Ice = Color.FromArgb(0xED, 0xF5, 0xFF);
    internal static readonly Color Ink = Color.FromArgb(0x10, 0x0B, 0xEA);
    private static readonly Color Muted = Color.FromArgb(0x45, 0x46, 0xA2);
    private static readonly Color Ring = Color.FromArgb(0xA8, 0xB6, 0xEB);
    private static readonly Color Border = Color.FromArgb(0x87, 0x94, 0xDC);
    private static readonly Color Raised = Color.FromArgb(0xD9, 0xE3, 0xFF);

    internal const string SkipText = "Skip this version";
    private const int Pad = 28;
    private readonly UpdateInfo update;

    internal UpdateAvailableForm(UpdateInfo update)
    {
        this.update = update;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Ice;
        ClientSize = new Size(480, 262);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "AfterThemed update available";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BuildContent();
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

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Windows 11: smooth rounded corners and a thin blue border. Older Windows keeps square corners.
        var round = 2;
        var border = Border.R | (Border.G << 8) | (Border.B << 16);
        try
        {
            DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
            DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
        }
        catch (DllNotFoundException) { }
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
        using var divider = new Pen(Ring, 1f);
        e.Graphics.DrawLine(divider, Pad, 186, ClientSize.Width - Pad, 186);
    }

    private void BuildContent()
    {
        Controls.Add(new AfterThemedMark { Bounds = new Rectangle(Pad - 4, 24, 56, 56), AccessibleName = "AfterThemed logo" });
        AddLabel("Update available", new Rectangle(92, 26, 300, 32), 17f, Ink);
        AddLabel($"{update.TagName} is ready  ·  You have {ApplicationLifetime.DisplayVersion()}",
            new Rectangle(93, 60, ClientSize.Width - 93 - Pad, 20), 9f, Muted);
        AddLabel("Download the installer from GitHub, then close AfterThemed before you run it.",
            new Rectangle(Pad, 102, ClientSize.Width - Pad * 2, 42), 10f, Ink);
        AddLabel($"Skip this version to hear nothing more about {update.TagName}. You will still be told about newer releases.",
            new Rectangle(Pad, 146, ClientSize.Width - Pad * 2, 34), 8.5f, Muted);

        var close = new PillButton(PillButton.Kind.Ghost) { Text = "✕", AccessibleName = "Close", Font = UiFonts.Sans(10f) };
        close.Bounds = new Rectangle(ClientSize.Width - Pad - 32 + 8, 20, 32, 32);
        close.Click += (_, _) => Close();
        Controls.Add(close);

        const int top = 204, height = 36;
        var download = AddButton("Download update", PillButton.Kind.Filled, OpenDownload);
        var release = AddButton("View release", PillButton.Kind.Outline, OpenRelease);
        var skip = AddButton(SkipText, PillButton.Kind.Ghost, () =>
        {
            DialogResult = DialogResult.Ignore;
            Close();
        });
        var right = ClientSize.Width - Pad;
        download.Bounds = new Rectangle(right - download.Width, top, download.Width, height);
        release.Bounds = new Rectangle(download.Left - 8 - release.Width, top, release.Width, height);
        skip.Bounds = new Rectangle(Pad - 14, top, skip.Width, height);
        AcceptButton = download;
        CancelButton = close;
        ActiveControl = download;
    }

    private PillButton AddButton(string text, PillButton.Kind kind, Action action)
    {
        var button = new PillButton(kind) { Text = text, Font = UiFonts.Sans(9f, kind == PillButton.Kind.Filled ? FontStyle.Bold : FontStyle.Regular) };
        button.Width = UiText.Measure(text, button.Font) + 36;
        button.Click += (_, _) => action();
        Controls.Add(button);
        return button;
    }

    private void OpenDownload()
    {
        if (OpenUrl(update.DownloadUrl)) Close();
    }

    private void OpenRelease()
    {
        if (OpenUrl(update.ReleasePageUrl)) Close();
    }

    private bool OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Unable to open GitHub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void AddLabel(string text, Rectangle bounds, float size, Color color) => Controls.Add(new Label
    {
        AutoSize = false,
        BackColor = Color.Transparent,
        Bounds = bounds,
        Font = UiFonts.Sans(size),
        ForeColor = color,
        Text = text,
        UseCompatibleTextRendering = true
    });

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Flat pill: filled blue, thin blue outline, or ghost (outline only on hover).</summary>
    private sealed class PillButton : Button
    {
        internal enum Kind { Filled, Outline, Ghost }
        private readonly Kind kind;
        private bool hovering;

        internal PillButton(Kind kind)
        {
            this.kind = kind;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ice); // the dialog surface, so the pill's antialiased edge blends into it
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var body = new RectangleF(.5f, .5f, Width - 1.5f, Height - 1.5f);
            using var path = RoundedPanel.RoundRect(Rectangle.Round(body), (int)(Height / 2f));
            var fill = kind == Kind.Filled ? (hovering ? Color.FromArgb(0x2B, 0x26, 0xEE) : Ink) : hovering ? Raised : Ice;
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            if (kind == Kind.Outline || (kind == Kind.Ghost && hovering))
                using (var pen = new Pen(Border, 1f)) g.DrawPath(pen, path);
            if (Focused && ShowFocusCues)
            {
                using var focus = RoundedPanel.RoundRect(new Rectangle(2, 2, Width - 5, Height - 5), (Height - 4) / 2);
                using var pen = new Pen(kind == Kind.Filled ? Ice : Ink, 1.5f);
                g.DrawPath(pen, focus);
            }
            UiText.Draw(g, Text, Font, ClientRectangle, kind == Kind.Filled ? Ice : Ink, ellipsis: false);
        }
    }
}
