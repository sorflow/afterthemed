using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace DvauiThemeEditor;

/// <summary>
/// A compact, modal product card modeled after the classic Adobe About panel.
/// The supplied AfterThemed SVG mark is used for both brand placements.
/// </summary>
internal sealed class AboutAfterThemedForm : Form
{
    private static readonly Color Surface = Color.FromArgb(2, 4, 5);
    private static readonly Color Outline = Color.FromArgb(24, 49, 57);
    private static readonly Color Primary = Color.FromArgb(248, 250, 251);
    private static readonly Color Secondary = Color.FromArgb(181, 194, 200);
    private static readonly Color Accent = Color.FromArgb(92, 198, 250);

    internal AboutAfterThemedForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Surface;
        ClientSize = new Size(575, 493);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "About AfterThemed";

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        BuildContent();
        Resize += (_, _) => UpdateRoundedRegion();
        Shown += (_, _) => UpdateRoundedRegion();
        Deactivate += (_, _) => Close();
        Click += (_, _) => Close();
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
        if (keyData is Keys.Escape or Keys.Enter or Keys.Space)
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
        var headerMark = new AfterThemedMark
        {
            Bounds = new Rectangle(22, 22, 46, 46),
            AccessibleName = "AfterThemed SVG logo"
        };
        AddDismissControl(headerMark);

        AddDismissLabel("AfterThemed", new Rectangle(74, 27, 430, 35), 19f, FontStyle.Bold, Primary);
        AddDismissLabel($"Version {Application.ProductVersion}  ·  {DateTime.Now.Year}",
            new Rectangle(74, 69, 320, 20), 8.2f, FontStyle.Bold, Primary);

        AddDismissLabel(
            $"© {DateTime.Now.Year} AfterThemed. Created by Drerachi.\r\n" +
            "AfterThemed is an independent community tool and is not affiliated with Adobe.",
            new Rectangle(30, 128, 510, 42), 8.1f, FontStyle.Regular, Primary);

        AddDismissLabel(
            "Thanks to my family in the Blank server!\r\n\r\n" +
            "Special thanks to\r\n" +
            "Dallas  ·  Jaidon  ·  Ito  ·  Star\r\n" +
            "and especially Tewzy for pushing me to do this fun project!\r\n" +
            "You're the best loser :D",
            new Rectangle(30, 177, 510, 105), 8.5f, FontStyle.Regular, Primary);

        AddDismissLabel("#Blank2026   #bringbackrealprogramming",
            new Rectangle(30, 289, 430, 21), 8.2f, FontStyle.Bold, Accent);

        AddDismissLabel("CONNECT", new Rectangle(30, 376, 120, 18), 7.2f, FontStyle.Bold, Secondary);
        AddLink("X / @shonenvii", "https://x.com/shonenvii", new Rectangle(30, 402, 170, 22));
        AddLink("YouTube / shonenshwty", "https://youtube.com/shonenshwty", new Rectangle(216, 402, 190, 22));
        AddLink("Instagram / @ripshonen", "https://instagram.com/ripshonen", new Rectangle(30, 430, 180, 22));
        AddLink("Discord / Blank", "https://discord.gg/blank", new Rectangle(216, 430, 150, 22));
        AddLegalLink(new Rectangle(30, 458, 180, 22));

        var cornerMark = new AfterThemedMark
        {
            Bounds = new Rectangle(474, 386, 78, 78),
            AccessibleName = "AfterThemed SVG logo"
        };
        AddDismissControl(cornerMark);
        var cornerCaption = AddDismissLabel("AFTERTHEMED", new Rectangle(450, 463, 100, 17), 6.8f,
            FontStyle.Bold, Secondary, ContentAlignment.MiddleRight);
        cornerCaption.Font = UiFonts.Mono(6.8f, FontStyle.Bold);
    }

    private Label AddDismissLabel(string text, Rectangle bounds, float size, FontStyle style, Color color,
        ContentAlignment alignment = ContentAlignment.TopLeft)
    {
        var label = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Bounds = bounds,
            Font = UiFonts.Sans(size, style),
            ForeColor = color,
            Text = text,
            TextAlign = alignment,
            UseCompatibleTextRendering = false
        };
        AddDismissControl(label);
        return label;
    }

    private void AddDismissControl(Control control)
    {
        control.Click += (_, _) => Close();
        Controls.Add(control);
    }

    private void AddLink(string text, string url, Rectangle bounds)
    {
        var link = new LinkLabel
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Bounds = bounds,
            Cursor = Cursors.Hand,
            Font = UiFonts.Sans(8.1f, FontStyle.Regular),
            ForeColor = Primary,
            LinkColor = Primary,
            ActiveLinkColor = Accent,
            VisitedLinkColor = Primary,
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = $"Open {text}"
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Unable to open link", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Controls.Add(link);
    }

    private void AddLegalLink(Rectangle bounds)
    {
        var link = new LinkLabel
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Bounds = bounds,
            Cursor = Cursors.Hand,
            Font = UiFonts.Sans(8.1f, FontStyle.Regular),
            ForeColor = Primary,
            LinkColor = Primary,
            ActiveLinkColor = Accent,
            VisitedLinkColor = Primary,
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            Text = "EULA / Legal Notices",
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Open the AfterThemed EULA and legal notices"
        };
        link.LinkClicked += (_, _) =>
        {
            var eula = Path.Combine(Application.StartupPath, "EULA.txt");
            if (!File.Exists(eula))
            {
                MessageBox.Show(this, "EULA.txt was not found beside AfterThemed.exe.", "Legal Notices",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(eula) { UseShellExecute = true });
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Unable to open legal notices",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Controls.Add(link);
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
