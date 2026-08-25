using System.Drawing.Drawing2D;

namespace DvauiThemeEditor;

internal sealed class VerbatimColorPickerForm : Form
{
    private static readonly Color SurfaceColor = UiPalette.Panel;
    private readonly VerbatimColorPickerSurface surface;

    internal event Action<Color>? ColorChanged;

    internal VerbatimColorPickerForm(Color initial)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SurfaceColor;
        ClientSize = new Size(420, 393);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "AfterThemed Color Picker";

        surface = new VerbatimColorPickerSurface(initial) { Dock = DockStyle.Fill };
        surface.ColorChanged += color => ColorChanged?.Invoke(color);
        Controls.Add(surface);

        Deactivate += (_, _) => Close();
        Shown += (_, _) => surface.Focus();
        Resize += (_, _) => UpdateRoundedRegion();
    }

    internal void ShowNear(Form owner, Control anchor)
    {
        var anchorPoint = anchor.PointToScreen(new Point(anchor.Width / 2, anchor.Height));
        var workArea = Screen.FromPoint(anchorPoint).WorkingArea;
        var x = anchorPoint.X - Width / 2;
        var y = anchorPoint.Y + 10;
        if (y + Height > workArea.Bottom) y = anchorPoint.Y - Height - 10;
        x = Math.Clamp(x, workArea.Left + 10, Math.Max(workArea.Left + 10, workArea.Right - Width - 10));
        y = Math.Clamp(y, workArea.Top + 10, Math.Max(workArea.Top + 10, workArea.Bottom - Height - 10));
        Location = new Point(x, y);
        UpdateRoundedRegion();
        Show(owner);
        Activate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
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

    private void UpdateRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = RoundedPanel.RoundRect(ClientRectangle, 26);
        var next = new Region(path);
        var previous = Region;
        Region = next;
        previous?.Dispose();
    }
}

internal sealed class VerbatimColorPickerSurface : Control
{
    private const float DesignWidth = 560f;
    private const float DesignHeight = 524f;
    private static readonly Color Surface = UiPalette.Panel;
    private static readonly Color SurfaceDark = UiPalette.Input;
    private static readonly Color Outline = UiPalette.Border;
    private static readonly Color Purple = UiPalette.Accent;
    private static readonly Color Muted = UiPalette.Muted;
    private static readonly Color White = UiPalette.Text;

    private static readonly Color[] Presets =
    [
        Color.FromArgb(14, 14, 18),
        Color.FromArgb(244, 82, 41),
        Color.FromArgb(255, 116, 0),
        Color.FromArgb(255, 204, 45),
        Color.FromArgb(91, 214, 82),
        Color.FromArgb(69, 174, 232),
        Color.FromArgb(104, 53, 246),
        Color.FromArgb(236, 236, 236)
    ];

    private readonly Font hexFont = UiFonts.Mono(14f, FontStyle.Bold);
    private double hue;
    private double saturation;
    private double brightness;
    private int alpha;
    private DragTarget dragTarget;
    private bool editingHex;
    private string hexBuffer = string.Empty;

    internal event Action<Color>? ColorChanged;

    private enum DragTarget { None, SaturationValue, Hue, Alpha }

    private static readonly RectangleF SaturationValue = new(18, 98, 524, 326);
    private static readonly RectangleF HueTrack = new(18, 443, 366, 21);
    private static readonly RectangleF AlphaTrack = new(18, 483, 366, 21);
    private static readonly RectangleF HexField = new(398, 434, 144, 80);

    internal VerbatimColorPickerSurface(Color initial)
    {
        BackColor = Surface;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        SetFromColor(initial, false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hexFont.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var scale = Math.Min(Width / DesignWidth, Height / DesignHeight);
        var offsetX = (Width - DesignWidth * scale) / 2f;
        var offsetY = (Height - DesignHeight * scale) / 2f;
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform(offsetX, offsetY);
        e.Graphics.ScaleTransform(scale, scale);

        PaintOuterSurface(e.Graphics);
        PaintSwatches(e.Graphics);
        PaintSaturationValue(e.Graphics);
        PaintHue(e.Graphics);
        PaintAlpha(e.Graphics);
        PaintHexField(e.Graphics);

        e.Graphics.Restore(state);
    }

    private void PaintOuterSurface(Graphics graphics)
    {
        using var borderPath = RoundRect(new RectangleF(0.75f, 0.75f, DesignWidth - 1.5f, DesignHeight - 1.5f), 34f);
        using var border = new Pen(Outline, 1.5f);
        graphics.DrawPath(border, borderPath);
        using var separator = new Pen(Outline, 1.2f);
        graphics.DrawLine(separator, 0, 78, DesignWidth, 78);
    }

    private void PaintSwatches(Graphics graphics)
    {
        const float startX = 18;
        const float y = 20;
        const float width = 50;
        const float height = 43;
        const float gap = 8.25f;
        var selected = SelectedColor;

        for (var index = 0; index < Presets.Length; index++)
        {
            var rect = new RectangleF(startX + index * (width + gap), y, width, height);
            var preset = Presets[index];
            var borderColor = index switch
            {
                1 => Color.FromArgb(237, 54, 29),
                2 => Color.FromArgb(248, 89, 0),
                3 => Color.FromArgb(239, 175, 31),
                4 => Color.FromArgb(45, 168, 53),
                5 => Color.FromArgb(36, 132, 212),
                6 => Color.FromArgb(75, 31, 219),
                7 => Color.FromArgb(194, 194, 194),
                _ => Color.FromArgb(16, 15, 20)
            };
            FillRounded(graphics, rect, 8, borderColor);
            FillRounded(graphics, RectangleF.Inflate(rect, -4, -4), 5, preset);

            if (preset.R == selected.R && preset.G == selected.G && preset.B == selected.B)
            {
                using var dot = new SolidBrush(IsLight(preset) ? SurfaceDark : White);
                graphics.FillEllipse(dot, rect.X + rect.Width / 2f - 5, rect.Y + rect.Height / 2f - 5, 10, 10);
            }
        }

        var rainbow = new RectangleF(startX + Presets.Length * (width + gap), y, width, height);
        using var clip = RoundRect(rainbow, 8);
        var saved = graphics.Save();
        graphics.SetClip(clip);
        using (var rainbowBrush = CreateHueBrush(rainbow)) graphics.FillRectangle(rainbowBrush, rainbow);
        using (var shade = new LinearGradientBrush(rainbow, Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 0, 0, 0), 90f))
            graphics.FillRectangle(shade, rainbow);
        graphics.Restore(saved);
        using (var plus = new Pen(White, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(plus, rainbow.X + 17, rainbow.Y + 21.5f, rainbow.Right - 17, rainbow.Y + 21.5f);
            graphics.DrawLine(plus, rainbow.X + 25, rainbow.Y + 13.5f, rainbow.X + 25, rainbow.Bottom - 13.5f);
        }
    }

    private void PaintSaturationValue(Graphics graphics)
    {
        using var clip = RoundRect(SaturationValue, 9);
        var saved = graphics.Save();
        graphics.SetClip(clip);
        using (var baseBrush = new SolidBrush(HsvToColor(hue, 1, 1)))
            graphics.FillRectangle(baseBrush, SaturationValue);

        using (var whiteLayer = new LinearGradientBrush(SaturationValue, Color.White, Color.White, 0f))
        {
            whiteLayer.InterpolationColors = new ColorBlend
            {
                Colors = [Color.White, Color.FromArgb(0, 255, 255, 255)],
                Positions = [0f, 1f]
            };
            graphics.FillRectangle(whiteLayer, SaturationValue);
        }

        using (var blackLayer = new LinearGradientBrush(SaturationValue, Color.Transparent, Color.Black, 90f))
        {
            blackLayer.InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(0, 0, 0, 0), Color.Black],
                Positions = [0f, 1f]
            };
            graphics.FillRectangle(blackLayer, SaturationValue);
        }
        graphics.Restore(saved);

        var markerX = SaturationValue.X + (float)saturation * SaturationValue.Width;
        var markerY = SaturationValue.Y + (float)(1 - brightness) * SaturationValue.Height;
        DrawMarker(graphics, markerX, markerY);
    }

    private void PaintHue(Graphics graphics)
    {
        using var path = RoundRect(HueTrack, HueTrack.Height / 2f);
        var saved = graphics.Save();
        graphics.SetClip(path);
        using (var hueBrush = CreateHueBrush(HueTrack)) graphics.FillRectangle(hueBrush, HueTrack);
        graphics.Restore(saved);
        var markerX = HueTrack.X + (float)(1d - hue / 360d) * HueTrack.Width;
        DrawMarker(graphics, markerX, HueTrack.Y + HueTrack.Height / 2f, 9f);
    }

    private void PaintAlpha(Graphics graphics)
    {
        using var path = RoundRect(AlphaTrack, AlphaTrack.Height / 2f);
        var saved = graphics.Save();
        graphics.SetClip(path);
        PaintCheckerboard(graphics, AlphaTrack, 8f);
        var rgb = HsvToColor(hue, saturation, brightness);
        using (var alphaBrush = new LinearGradientBrush(AlphaTrack,
                   Color.FromArgb(0, rgb.R, rgb.G, rgb.B), Color.FromArgb(255, rgb.R, rgb.G, rgb.B), 0f))
            graphics.FillRectangle(alphaBrush, AlphaTrack);
        graphics.Restore(saved);
        var markerX = AlphaTrack.X + alpha / 255f * AlphaTrack.Width;
        DrawMarker(graphics, markerX, AlphaTrack.Y + AlphaTrack.Height / 2f, 9f);
    }

    private void PaintHexField(Graphics graphics)
    {
        FillRounded(graphics, HexField, 9, SurfaceDark);
        if (editingHex)
        {
            using var focus = new Pen(Purple, 2f);
            using var path = RoundRect(RectangleF.Inflate(HexField, -1, -1), 8);
            graphics.DrawPath(focus, path);
        }
        var text = editingHex ? hexBuffer : ToRgbaHex(SelectedColor);
        DrawCenteredText(graphics, text, hexFont, HexField, Muted);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        var point = ToDesignPoint(e.Location);

        for (var index = 0; index < Presets.Length; index++)
        {
            if (SwatchRectangle(index).Contains(point))
            {
                SetFromColor(Color.FromArgb(alpha, Presets[index]), true);
                editingHex = false;
                return;
            }
        }
        if (SwatchRectangle(Presets.Length).Contains(point))
        {
            editingHex = true;
            hexBuffer = ToRgbaHex(SelectedColor);
            Invalidate();
            return;
        }
        if (SaturationValue.Contains(point)) { dragTarget = DragTarget.SaturationValue; UpdateSaturationValue(point); return; }
        if (HueTrack.Contains(point)) { dragTarget = DragTarget.Hue; UpdateHue(point.X); return; }
        if (AlphaTrack.Contains(point)) { dragTarget = DragTarget.Alpha; UpdateAlpha(point.X); return; }
        if (HexField.Contains(point))
        {
            editingHex = true;
            hexBuffer = ToRgbaHex(SelectedColor);
            Invalidate();
            return;
        }
        editingHex = false;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = ToDesignPoint(e.Location);
        Cursor = HexField.Contains(point) ? Cursors.IBeam : SaturationValue.Contains(point) ? Cursors.Cross : Cursors.Hand;
        if (e.Button != MouseButtons.Left) return;
        switch (dragTarget)
        {
            case DragTarget.SaturationValue: UpdateSaturationValue(point); break;
            case DragTarget.Hue: UpdateHue(point.X); break;
            case DragTarget.Alpha: UpdateAlpha(point.X); break;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragTarget = DragTarget.None;
        base.OnMouseUp(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!editingHex)
        {
            base.OnKeyPress(e);
            return;
        }

        if (e.KeyChar == '\b')
        {
            if (hexBuffer.Length > 0) hexBuffer = hexBuffer[..^1];
            e.Handled = true;
            Invalidate();
            return;
        }
        if (e.KeyChar is '\r' or '\n')
        {
            TryApplyHexBuffer();
            e.Handled = true;
            return;
        }
        if ((Uri.IsHexDigit(e.KeyChar) || e.KeyChar == '#') && hexBuffer.Length < 9)
        {
            if (e.KeyChar == '#') hexBuffer = "#" + hexBuffer.TrimStart('#');
            else hexBuffer += char.ToUpperInvariant(e.KeyChar);
            TryApplyHexBuffer();
            e.Handled = true;
            Invalidate();
            return;
        }
        e.Handled = true;
    }

    private void TryApplyHexBuffer()
    {
        var raw = hexBuffer.Trim().TrimStart('#');
        if (raw.Length is not (6 or 8)) return;
        try
        {
            var r = Convert.ToInt32(raw[..2], 16);
            var g = Convert.ToInt32(raw.Substring(2, 2), 16);
            var b = Convert.ToInt32(raw.Substring(4, 2), 16);
            var a = raw.Length == 8 ? Convert.ToInt32(raw.Substring(6, 2), 16) : 255;
            SetFromColor(Color.FromArgb(a, r, g, b), true);
            hexBuffer = "#" + raw.ToUpperInvariant();
        }
        catch { }
    }

    private void UpdateSaturationValue(PointF point)
    {
        saturation = Math.Clamp((point.X - SaturationValue.X) / SaturationValue.Width, 0, 1);
        brightness = 1 - Math.Clamp((point.Y - SaturationValue.Y) / SaturationValue.Height, 0, 1);
        editingHex = false;
        EmitColor();
    }

    private void UpdateHue(float x)
    {
        hue = (1d - Math.Clamp((x - HueTrack.X) / HueTrack.Width, 0, 1)) * 360d;
        editingHex = false;
        EmitColor();
    }

    private void UpdateAlpha(float x)
    {
        alpha = Math.Clamp((int)Math.Round((x - AlphaTrack.X) / AlphaTrack.Width * 255), 0, 255);
        editingHex = false;
        EmitColor();
    }

    private void SetFromColor(Color color, bool emit)
    {
        alpha = color.A;
        RgbToHsv(color, out hue, out saturation, out brightness);
        if (emit) EmitColor();
        else Invalidate();
    }

    private void EmitColor()
    {
        var color = SelectedColor;
        hexBuffer = ToRgbaHex(color);
        Invalidate();
        ColorChanged?.Invoke(color);
    }

    private Color SelectedColor
    {
        get
        {
            var rgb = HsvToColor(hue, saturation, brightness);
            return Color.FromArgb(alpha, rgb.R, rgb.G, rgb.B);
        }
    }

    private PointF ToDesignPoint(Point point)
    {
        var scale = Math.Min(Width / DesignWidth, Height / DesignHeight);
        if (scale <= 0) return PointF.Empty;
        var offsetX = (Width - DesignWidth * scale) / 2f;
        var offsetY = (Height - DesignHeight * scale) / 2f;
        return new PointF((point.X - offsetX) / scale, (point.Y - offsetY) / scale);
    }

    private static RectangleF SwatchRectangle(int index) => new(18 + index * 58.25f, 20, 50, 43);

    private static void DrawMarker(Graphics graphics, float x, float y, float radius = 10f)
    {
        using var shadow = new Pen(Color.FromArgb(95, 0, 0, 0), 5f);
        using var ring = new Pen(White, 4f);
        graphics.DrawEllipse(shadow, x - radius, y - radius, radius * 2, radius * 2);
        graphics.DrawEllipse(ring, x - radius, y - radius, radius * 2, radius * 2);
    }

    private static void PaintCheckerboard(Graphics graphics, RectangleF bounds, float cell)
    {
        using var light = new SolidBrush(Color.FromArgb(117, 112, 132));
        using var dark = new SolidBrush(Color.FromArgb(64, 61, 73));
        var row = 0;
        for (var y = bounds.Top; y < bounds.Bottom; y += cell, row++)
        {
            var column = 0;
            for (var x = bounds.Left; x < bounds.Right; x += cell, column++)
            {
                var width = Math.Min(cell, bounds.Right - x);
                var height = Math.Min(cell, bounds.Bottom - y);
                graphics.FillRectangle((row + column) % 2 == 0 ? light : dark, x, y, width, height);
            }
        }
    }

    private static LinearGradientBrush CreateHueBrush(RectangleF bounds)
    {
        var brush = new LinearGradientBrush(bounds, Color.Red, Color.Red, 0f);
        brush.InterpolationColors = new ColorBlend
        {
            Colors = [Color.Red, Color.Magenta, Color.Blue, Color.Cyan, Color.Lime, Color.Yellow, Color.Red],
            Positions = [0f, 1f / 6f, 2f / 6f, 3f / 6f, 4f / 6f, 5f / 6f, 1f]
        };
        return brush;
    }

    private static void FillRounded(Graphics graphics, RectangleF bounds, float radius, Color color)
    {
        using var path = RoundRect(bounds, radius);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2f);
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawCenteredText(Graphics graphics, string text, Font font, RectangleF bounds, Color color)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(text, font, brush, bounds, format);
    }

    private static bool IsLight(Color color) => color.R * .299 + color.G * .587 + color.B * .114 > 165;

    private static string ToRgbaHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    private static void RgbToHsv(Color color, out double h, out double s, out double v)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
        }
        if (h < 0) h += 360;
        s = max <= 0 ? 0 : delta / max;
        v = max;
    }

    private static Color HsvToColor(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60d) % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };
        return Color.FromArgb(
            Math.Clamp((int)Math.Round((r + m) * 255), 0, 255),
            Math.Clamp((int)Math.Round((g + m) * 255), 0, 255),
            Math.Clamp((int)Math.Round((b + m) * 255), 0, 255));
    }
}
