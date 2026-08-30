using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.ComponentModel;

namespace DvauiThemeEditor;

internal static class UiPalette
{
    // Material 3 Expressive-inspired dark scheme generated from a #1DACF3
    // electric-blue seed. Large areas use calm, related blue-slate tones while
    // the high-chroma seed is reserved for actions, focus, and selected state.
    internal static readonly Color Window = Color.FromArgb(8, 24, 32);              // surface dim
    internal static readonly Color Panel = Color.FromArgb(16, 38, 48);              // surface container
    internal static readonly Color PanelRaised = Color.FromArgb(25, 54, 65);        // surface container high
    internal static readonly Color PanelHover = Color.FromArgb(35, 70, 82);         // surface container highest
    internal static readonly Color Input = Color.FromArgb(13, 32, 41);              // surface container low
    internal static readonly Color Border = Color.FromArgb(69, 101, 115);           // outline variant
    internal static readonly Color Text = Color.FromArgb(233, 246, 252);            // on surface
    internal static readonly Color Muted = Color.FromArgb(172, 198, 210);           // on surface variant
    internal static readonly Color Canvas = Color.FromArgb(241, 247, 250);          // light surface
    internal static readonly Color CanvasRaised = Color.FromArgb(227, 239, 244);    // light surface container
    internal static readonly Color CanvasText = Color.FromArgb(23, 42, 51);         // light on surface
    internal static readonly Color CanvasMuted = Color.FromArgb(83, 106, 117);      // light on surface variant
    internal static readonly Color Accent = Color.FromArgb(29, 172, 243);           // #1DACF3 primary
    internal static readonly Color AccentHover = Color.FromArgb(92, 198, 250);      // primary hover
    internal static readonly Color OnAccent = Color.FromArgb(0, 44, 58);            // on primary
    internal static readonly Color AccentContainer = Color.FromArgb(0, 75, 101);    // primary container
    internal static readonly Color OnAccentContainer = Color.FromArgb(190, 234, 255);// on primary container
    internal static readonly Color LightAction = Color.FromArgb(199, 234, 252);     // light primary container
    internal static readonly Color LightActionHover = Color.FromArgb(167, 220, 246);
    internal static readonly Color LightActionText = Color.FromArgb(0, 53, 72);
    internal static readonly Color Error = Color.FromArgb(255, 180, 171);
    internal static readonly Color ErrorContainer = Color.FromArgb(73, 29, 30);
}

internal static class UiFonts
{
    internal static readonly string SansFamily =
        Resolve("Inter", "Inter Variable", "Segoe UI Variable Text", "Segoe UI");
    internal static readonly string MonoFamily =
        Resolve("Cascadia Mono", "Consolas", "Courier New");

    internal static Font Sans(float size, FontStyle style = FontStyle.Regular) => new(SansFamily, size, style);
    internal static Font Mono(float size, FontStyle style = FontStyle.Regular) => new(MonoFamily, size, style);

    private static string Resolve(params string[] candidates)
    {
        using var installed = new InstalledFontCollection();
        var families = installed.Families;
        foreach (var candidate in candidates)
            foreach (var family in families)
                if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    return family.Name;
        return FontFamily.GenericSansSerif.Name;
    }
}

/// <summary>
/// Tiled dust-and-flake texture for the dark chrome. Tiles are generated once per
/// base colour and cached, and every surface offsets the pattern by its own position
/// inside the form so the field stays continuous across nested panel seams.
/// </summary>
internal static class SpeckleField
{
    private const int TileSize = 256;
    private const int SpecksPerTile = 105;

    private static readonly Dictionary<int, TextureBrush> Tiles = new();

    /// <summary>
    /// The raised card a surface belongs to, in form coordinates. Children paint their
    /// own slice of the owner's bevel, so the gradient runs unbroken underneath them
    /// instead of being sliced off at the card's padding edge.
    /// </summary>
    internal readonly record struct BevelSpec(Rectangle Bounds, int Radius);

    internal static Point OriginIn(Control control)
    {
        if (!control.IsHandleCreated) return Point.Empty;
        var form = control.FindForm();
        if (form is null || !form.IsHandleCreated) return Point.Empty;
        return form.PointToClient(control.PointToScreen(Point.Empty));
    }

    internal static BevelSpec? BevelOwnerFor(Control control)
    {
        for (var parent = control.Parent; parent is not null and not Form; parent = parent.Parent)
            if (parent is RoundedPanel { Speckle: true } card)
                return new BevelSpec(new Rectangle(OriginIn(card), card.ClientSize), card.Radius);
        return null;
    }

    internal static void Paint(Graphics g, Rectangle bounds, Point origin, Color baseColor,
                               GraphicsPath? shape = null, BevelSpec? bevel = null)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        if (baseColor.A == 0) baseColor = UiPalette.Window;

        var brush = TileFor(baseColor);
        brush.ResetTransform();
        brush.TranslateTransform(Wrap(-origin.X), Wrap(-origin.Y));
        if (shape is null) g.FillRectangle(brush, bounds);
        else g.FillPath(brush, shape);

        if (bevel is not { } spec) return;
        var owner = new Rectangle(
            spec.Bounds.X - origin.X, spec.Bounds.Y - origin.Y,
            spec.Bounds.Width, spec.Bounds.Height);
        PaintBevel(g, owner, spec.Radius);
    }

    /// <summary>Repaints the speckled parent surface behind a control that draws its own shape.</summary>
    internal static void PaintHost(Graphics g, Control control)
    {
        var color = control.Parent?.BackColor ?? UiPalette.Window;
        Paint(g, control.ClientRectangle, OriginIn(control), color, bevel: BevelOwnerFor(control));
    }

    // Light gathers along the top edge of the owning card and shade pools at its base.
    // Everything here is expressed in the owner's geometry, so any surface inside the
    // card draws the same ramp and the seams disappear.
    private static void PaintBevel(Graphics g, Rectangle owner, int radius)
    {
        if (owner.Width <= 0 || owner.Height <= 0) return;

        var state = g.Save();
        using var ownerPath = RoundedPanel.RoundRect(owner, radius);
        g.SetClip(ownerPath, CombineMode.Intersect);

        var depth = Math.Clamp(owner.Height / 7, 5, 22);
        var top = new Rectangle(owner.X, owner.Y, owner.Width, depth);
        using (var light = new LinearGradientBrush(
            Rectangle.Inflate(top, 1, 1),
            Color.FromArgb(26, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
            g.FillRectangle(light, top);

        var bottom = new Rectangle(owner.X, owner.Bottom - depth, owner.Width, depth);
        using (var shade = new LinearGradientBrush(
            Rectangle.Inflate(bottom, 1, 1),
            Color.FromArgb(0, 0, 0, 0), Color.FromArgb(48, 0, 0, 0),
            LinearGradientMode.Vertical))
            g.FillRectangle(shade, bottom);

        // Fresnel rim: the outline only catches light across the upper arc.
        g.SetClip(new Rectangle(owner.X, owner.Y, owner.Width, owner.Height / 2), CombineMode.Intersect);
        using (var rim = new Pen(Color.FromArgb(34, 255, 255, 255), 1f))
            g.DrawPath(rim, ownerPath);

        g.Restore(state);
    }

    private static float Wrap(int value) => ((value % TileSize) + TileSize) % TileSize;

    private static TextureBrush TileFor(Color baseColor)
    {
        var key = baseColor.ToArgb();
        if (Tiles.TryGetValue(key, out var cached)) return cached;
        var brush = new TextureBrush(CreateTile(baseColor)) { WrapMode = WrapMode.Tile };
        Tiles[key] = brush;
        return brush;
    }

    private static Bitmap CreateTile(Color baseColor)
    {
        var tile = new Bitmap(TileSize, TileSize, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(tile);
        g.Clear(baseColor);

        // A fixed seed keeps the field identical across repaints, resizes and tiles.
        var random = new Random(baseColor.ToArgb() ^ 0x5EED17);
        var direction = baseColor.GetBrightness() > 0.5f ? -1 : 1;

        for (var i = 0; i < SpecksPerTile; i++)
        {
            var x = random.Next(TileSize);
            var y = random.Next(TileSize);

            if (random.Next(100) < 10)
            {
                var alpha = random.Next(18, 48);
                var tint = Tint(random);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var halo = new SolidBrush(Shift(baseColor, direction, 46, tint, alpha / 5)))
                    g.FillEllipse(halo, x - 1.6f, y - 1.6f, 3.4f, 3.4f);
                g.SmoothingMode = SmoothingMode.None;
                using var core = new SolidBrush(Shift(baseColor, direction, 96, tint, alpha));
                g.FillRectangle(core, x, y, 1, 1);
            }
            else
            {
                g.SmoothingMode = SmoothingMode.None;
                using var dust = new SolidBrush(Shift(baseColor, direction, 54, default, random.Next(3, 12)));
                g.FillRectangle(dust, x, y, 1, 1);
            }
        }
        return tile;
    }

    // Keep the remaining texture cool so it supports the periwinkle seed colour.
    private static (int R, int G, int B) Tint(Random random) => random.Next(3) switch
    {
        0 => (-10, 2, 24),
        1 => (8, -2, 28),
        _ => (0, 0, 0)
    };

    private static Color Shift(Color baseColor, int direction, int amount, (int R, int G, int B) tint, int alpha) =>
        Color.FromArgb(alpha,
            Math.Clamp(baseColor.R + direction * amount + tint.R, 0, 255),
            Math.Clamp(baseColor.G + direction * amount + tint.G, 0, 255),
            Math.Clamp(baseColor.B + direction * amount + tint.B, 0, 255));
}

internal class SpeckledPanel : Panel
{
    public SpeckledPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SpeckleField.Paint(e.Graphics, ClientRectangle, SpeckleField.OriginIn(this), BackColor,
            bevel: SpeckleField.BevelOwnerFor(this));
}

internal class SpeckledTable : TableLayoutPanel
{
    public SpeckledTable()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SpeckleField.Paint(e.Graphics, ClientRectangle, SpeckleField.OriginIn(this), BackColor,
            bevel: SpeckleField.BevelOwnerFor(this));
}

internal class SpeckledFlow : FlowLayoutPanel
{
    public SpeckledFlow()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SpeckleField.Paint(e.Graphics, ClientRectangle, SpeckleField.OriginIn(this), BackColor,
            bevel: SpeckleField.BevelOwnerFor(this));
}

internal sealed class RoundedPanel : Panel
{
    private int radius = 12;
    private Color borderColor = Color.Transparent;
    private int borderWidth = 1;
    private bool speckle;
    private bool clipToRadius = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius
    {
        get => radius;
        set
        {
            radius = Math.Max(0, value);
            UpdateRoundedRegion();
            Invalidate();
        }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor
    {
        get => borderColor;
        set { borderColor = value; Invalidate(); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderWidth
    {
        get => borderWidth;
        set { borderWidth = Math.Max(0, value); Invalidate(); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Speckle
    {
        get => speckle;
        set { speckle = value; Invalidate(); }
    }

    /// <summary>
    /// Whether the rounded outline is enforced by a clipping region. A region is one bit per pixel,
    /// so it saws the antialiased curve into a hard step and a tightly rounded panel ends up with
    /// visible flat notches where its ends should close. Turn this off for a panel whose children
    /// stay inside the curve: the host surface is repainted first and the rounded body is then drawn
    /// antialiased over it, which is how <see cref="MacButton"/> keeps its own pill edges smooth.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ClipToRadius
    {
        get => clipToRadius;
        set
        {
            clipToRadius = value;
            UpdateRoundedRegion();
            Invalidate();
        }
    }

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRoundedRegion();
    }

    /// <summary>
    /// An auto-sized panel reaches its final width during layout rather than through a resize, which
    /// left the clipping region sized for the panel's earlier, narrower bounds: the rounded end was
    /// cut off by a straight edge while the border still painted at full width.
    /// </summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        UpdateRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // Without a clipping region the control's own rectangle is still painted behind this, so the
        // host surface has to be laid back down before the rounded body is drawn over it.
        if (!clipToRadius) SpeckleField.PaintHost(e.Graphics, this);
        using var path = RoundRect(ClientRectangle, Radius);
        if (Speckle)
        {
            var origin = SpeckleField.OriginIn(this);
            SpeckleField.Paint(e.Graphics, ClientRectangle, origin, BackColor, path,
                new SpeckleField.BevelSpec(new Rectangle(origin, ClientSize), Radius));
        }
        else
        {
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
        }
        if (BorderColor != Color.Transparent && BorderWidth > 0)
        {
            // Kept in float space and shrunk by the same amount the radius loses, so the stroke stays
            // concentric with the fill and a fully rounded panel keeps true semicircular ends.
            var inset = Math.Max(1, BorderWidth) / 2f;
            using var borderPath = RoundRect(
                RectangleF.Inflate(ClientRectangle, -inset, -inset),
                Math.Max(0f, Radius - inset));
            using var pen = new Pen(BorderColor, BorderWidth);
            e.Graphics.DrawPath(pen, borderPath);
        }
        base.OnPaint(e);
    }

    private void UpdateRoundedRegion()
    {
        if (!clipToRadius || Width <= 0 || Height <= 0 || Radius <= 0)
        {
            var stale = Region;
            Region = null;
            stale?.Dispose();
            return;
        }

        using var path = RoundRect(ClientRectangle, Radius);
        var next = new Region(path);
        var previous = Region;
        Region = next;
        previous?.Dispose();
    }

    /// <summary>
    /// Float-precision rounded rectangle. The integer overload has to round its radius, so a border
    /// inset by half a pixel from a stadium-shaped panel ends up with a radius shorter than half its
    /// own height and closes its ends with a small flat cut instead of a true semicircle.
    /// </summary>
    internal static GraphicsPath RoundRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        var effectiveRadius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2f);
        if (effectiveRadius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var d = effectiveRadius * 2f;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static GraphicsPath RoundRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        var effectiveRadius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        if (effectiveRadius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var d = effectiveRadius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class AfterThemedMark : Control
{
    public AfterThemedMark()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        MinimumSize = new Size(32, 32);
        AccessibleName = "AfterThemed vector logo";
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var side = Math.Max(48f, Math.Min(ClientSize.Width, ClientSize.Height) - 8f);
        var bounds = new RectangleF((ClientSize.Width - side) / 2f, (ClientSize.Height - side) / 2f, side, side);
        using var plate = RoundedRectangle(bounds, side * .17f);
        // These are the exact view-box geometry and paint values from the supplied
        // Assets/AfterThemed-Mark.svg, rendered through GDI+ so it stays crisp in WinForms.
        using var plateBrush = new LinearGradientBrush(bounds,
            Color.FromArgb(25, 54, 65), Color.FromArgb(8, 24, 32), 135f);
        graphics.FillPath(plateBrush, plate);
        using var border = new Pen(Color.FromArgb(69, 101, 115), Math.Max(1f, side / 120f));
        graphics.DrawPath(border, plate);

        PointF Point(float x, float y) => new(bounds.X + x / 160f * bounds.Width, bounds.Y + y / 160f * bounds.Height);
        PointF[] Polygon(params float[] values)
        {
            var result = new PointF[values.Length / 2];
            for (var index = 0; index < result.Length; index++)
                result[index] = Point(values[index * 2], values[index * 2 + 1]);
            return result;
        }

        using var glow = new LinearGradientBrush(bounds,
            Color.FromArgb(92, 198, 250), Color.FromArgb(176, 125, 255), 0f);
        glow.InterpolationColors = new ColorBlend
        {
            Colors = [Color.FromArgb(92, 198, 250), Color.FromArgb(29, 172, 243), Color.FromArgb(176, 125, 255)],
            Positions = [0f, .52f, 1f]
        };
        graphics.FillPolygon(glow, Polygon(25, 125, 67, 35, 84, 35, 52, 125));
        graphics.FillPolygon(glow, Polygon(77, 35, 94, 35, 137, 125, 109, 125));
        graphics.FillPolygon(glow, Polygon(51, 84, 109, 84, 120, 106, 41, 106));

        var dotSize = side * .085f;
        var dot = new RectangleF(bounds.Right - side * .21f, bounds.Top + side * .14f, dotSize, dotSize);
        using var dotBrush = new SolidBrush(Color.FromArgb(29, 172, 243));
        graphics.FillEllipse(dotBrush, dot);

        using var sheen = new Pen(Color.FromArgb(95, Color.White), Math.Max(1f, side / 150f));
        graphics.DrawLine(sheen, Point(32, 120), Point(72, 38));
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2f, Math.Min(rectangle.Width, rectangle.Height));
        var arc = new RectangleF(rectangle.Location, new SizeF(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// The outlined glyph a pill button carries on its trailing edge. Each one states what the action
/// does, so the badges stay meaningful rather than decorative.
/// </summary>
internal enum PillBadge { None, Plus, Minus, Info, Alert, Download }

internal class MacButton : Button
{
    private bool hovering;
    private bool pressed;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 14;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = UiPalette.PanelHover;
    /// <summary>
    /// When set, the label moves to the leading edge and this glyph is drawn in a thin circle on the
    /// trailing edge, giving the pill navigation look.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PillBadge Badge { get; set; } = PillBadge.None;
    /// <summary>
    /// Drops the drop shadow and the outline, so an unselected item disappears into the surface it
    /// sits on and only the selected one reads as a filled control.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Flat { get; set; }

    public MacButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = UiPalette.PanelRaised;
        ForeColor = UiPalette.Text;
        Cursor = Cursors.Hand;
        Font = UiFonts.Sans(8.5f, FontStyle.Bold);
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        // ButtonBase paints an opaque rectangle before custom content. Repaint the
        // host surface first so the pixels outside the rounded path cannot leak as
        // dark corner blocks or a one-pixel strip below the button.
        SpeckleField.PaintHost(e.Graphics, this);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // The body sits one pixel proud of the bottom edge so the soft elevation
        // shadow has room. Interaction uses Material state layers rather than a
        // high-gloss bevel, keeping the brand colour stable and recognizable.
        var body = new Rectangle(0, 0, Width - 1, Height - 2);
        if (body.Width <= 0 || body.Height <= 0) return;

        if (!pressed && !Flat)
        {
            using var shadowPath = RoundedPanel.RoundRect(new Rectangle(0, 2, body.Width, body.Height), Radius);
            using var shadow = new SolidBrush(Color.FromArgb(42, 0, 0, 0));
            g.FillPath(shadow, shadowPath);
        }

        using var path = RoundedPanel.RoundRect(body, Radius);
        var face = hovering ? HoverColor : BackColor;
        if (pressed) face = ColorFx.Blend(face, ForeColor, 0.12f);
        using (var fill = new SolidBrush(face))
            g.FillPath(fill, path);

        if (!Flat)
        {
            using var pen = new Pen(Color.FromArgb(38, ForeColor), 1f);
            g.DrawPath(pen, path);
        }

        var label = body;
        if (pressed) label.Offset(0, 1);

        if (Badge == PillBadge.None)
        {
            UiText.Draw(g, Text, Font, label, ForeColor);
            return;
        }

        var diameter = Math.Max(12, Math.Min(18, label.Height - 12));
        var badge = new Rectangle(
            label.Right - diameter - 11,
            label.Y + (label.Height - diameter) / 2,
            diameter, diameter);
        var textArea = Rectangle.FromLTRB(label.X + 14, label.Y, badge.Left - 7, label.Bottom);
        UiText.Draw(g, Text, Font, textArea, ForeColor, StringAlignment.Near);
        DrawBadge(g, badge, ForeColor);
    }

    private void DrawBadge(Graphics g, Rectangle circle, Color color)
    {
        // The ring is deliberately lighter than the label so it reads as a quiet affordance rather
        // than competing with the button text.
        using var pen = new Pen(Color.FromArgb(185, color), 1.25f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var brush = new SolidBrush(Color.FromArgb(185, color));
        g.DrawEllipse(pen, circle);

        var cx = circle.X + circle.Width / 2f;
        var cy = circle.Y + circle.Height / 2f;
        var arm = circle.Width * .24f;

        switch (Badge)
        {
            case PillBadge.Plus:
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
                break;
            case PillBadge.Minus:
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                break;
            case PillBadge.Download:
                g.DrawLine(pen, cx, cy - arm * 1.1f, cx, cy + arm * .45f);
                g.DrawLine(pen, cx - arm * .58f, cy - arm * .1f, cx, cy + arm * .5f);
                g.DrawLine(pen, cx + arm * .58f, cy - arm * .1f, cx, cy + arm * .5f);
                g.DrawLine(pen, cx - arm * .72f, cy + arm * 1.15f, cx + arm * .72f, cy + arm * 1.15f);
                break;
            case PillBadge.Info:
                g.FillEllipse(brush, cx - .9f, cy - arm * 1.25f, 1.8f, 1.8f);
                g.DrawLine(pen, cx, cy - arm * .25f, cx, cy + arm * 1.1f);
                break;
            case PillBadge.Alert:
                g.DrawLine(pen, cx, cy - arm * 1.25f, cx, cy + arm * .35f);
                g.FillEllipse(brush, cx - .9f, cy + arm * .95f, 1.8f, 1.8f);
                break;
        }
    }
}

/// <summary>
/// Draws interface text through GDI+ so it is antialiased.
///
/// TextRenderer.DrawText goes through GDI, which drops antialiasing when it draws into the
/// alpha-capable buffer these double-buffered, transparency-supporting controls paint into: the
/// glyphs come out hard-edged, with no intermediate pixels along a stem. GDI also ignores
/// Graphics.TextRenderingHint, so the ClearTypeGridFit hint these controls set never applied to
/// their own labels. Grayscale antialiasing is used rather than ClearType because subpixel
/// rendering fringes noticeably on light-on-dark text.
/// </summary>
internal static class UiText
{
    internal static void Draw(Graphics g, string text, Font font, Rectangle bounds, Color color,
        StringAlignment horizontal = StringAlignment.Center,
        StringAlignment vertical = StringAlignment.Center,
        bool ellipsis = true)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;

        var previousHint = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var format = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = horizontal,
            LineAlignment = vertical,
            Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None
        };
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, bounds, format);
        g.TextRenderingHint = previousHint;
    }

    /// <summary>
    /// Width this text occupies when drawn by <see cref="Draw"/>, so callers can size a control to
    /// its label instead of guessing a fixed width and clipping it.
    /// </summary>
    internal static int Measure(string text, Font font)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return (int)Math.Ceiling(g.MeasureString(text, font).Width);
    }
}

internal static class ColorFx
{
    internal static Color Lighten(Color color, float amount) => Blend(color, Color.White, amount);
    internal static Color Darken(Color color, float amount) => Blend(color, Color.Black, amount);

    internal static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(from.A,
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }
}

internal enum WindowDotGlyph { Close, Minimize, Maximize }

internal sealed class WindowDotButton : Button
{
    private bool pressed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DotColor { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WindowDotGlyph Glyph { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<bool> ShowGlyph { get; set; } = () => false;

    public WindowDotButton(Color color, WindowDotGlyph glyph)
    {
        DotColor = color;
        Glyph = glyph;
        Size = new Size(18, 28);
        Margin = new Padding(0, 0, 2, 0);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = UiPalette.Window;
        Cursor = Cursors.Hand;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        // ButtonBase is opaque, so nothing erases the surface for us.
        SpeckleField.PaintHost(e.Graphics, this);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var dot = new RectangleF(3f, (Height - 12) / 2f, 12f, 12f);
        var lit = ShowGlyph();

        // Contact shadow, dropped while held so the dot reads as pushed in.
        if (!pressed)
        {
            using var shadow = new SolidBrush(Color.FromArgb(78, 0, 0, 0));
            g.FillEllipse(shadow, dot.X + 0.4f, dot.Y + 1.4f, dot.Width, dot.Height);
        }

        var top = pressed ? ColorFx.Darken(DotColor, 0.26f) : ColorFx.Lighten(DotColor, lit ? 0.44f : 0.32f);
        var bottom = pressed ? ColorFx.Darken(DotColor, 0.04f) : ColorFx.Darken(DotColor, lit ? 0.08f : 0.18f);
        using (var body = new LinearGradientBrush(RectangleF.Inflate(dot, 1f, 1f), top, bottom, LinearGradientMode.Vertical))
            g.FillEllipse(body, dot);

        var inner = RectangleF.Inflate(dot, -0.5f, -0.5f);

        // Fresnel rim: light gathers across the top arc and dies off at the equator.
        using (var rimFade = new LinearGradientBrush(RectangleF.Inflate(dot, 1f, 1f),
                   Color.FromArgb(lit ? 205 : 165, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                   LinearGradientMode.Vertical))
        using (var rim = new Pen(rimFade, 1f))
            g.DrawArc(rim, inner, 160f, 220f);

        using (var underFade = new LinearGradientBrush(RectangleF.Inflate(dot, 1f, 1f),
                   Color.FromArgb(0, 0, 0, 0), Color.FromArgb(96, 0, 0, 0),
                   LinearGradientMode.Vertical))
        using (var under = new Pen(underFade, 1f))
            g.DrawArc(under, inner, 18f, 144f);

        // Specular bloom in the upper third.
        if (!pressed)
        {
            var spec = new RectangleF(dot.X + dot.Width * 0.2f, dot.Y + dot.Height * 0.1f,
                                      dot.Width * 0.6f, dot.Height * 0.44f);
            using var specPath = new GraphicsPath();
            specPath.AddEllipse(spec);
            using var bloom = new PathGradientBrush(specPath)
            {
                CenterColor = Color.FromArgb(lit ? 180 : 140, 255, 255, 255),
                SurroundColors = [Color.FromArgb(0, 255, 255, 255)]
            };
            g.FillEllipse(bloom, spec);
        }

        if (!lit) return;

        using var pen = new Pen(Color.FromArgb(215, ColorFx.Darken(DotColor, 0.62f)), 1.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var cx = dot.Left + dot.Width / 2f;
        var cy = dot.Top + dot.Height / 2f;
        const float arm = 2.6f;
        switch (Glyph)
        {
            case WindowDotGlyph.Close:
                g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
                g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
                break;
            case WindowDotGlyph.Minimize:
                g.DrawLine(pen, cx - arm - 0.6f, cy, cx + arm + 0.6f, cy);
                break;
            case WindowDotGlyph.Maximize:
                g.DrawLine(pen, cx - arm - 0.6f, cy, cx + arm + 0.6f, cy);
                g.DrawLine(pen, cx, cy - arm - 0.6f, cx, cy + arm + 0.6f);
                break;
        }
    }
}

internal sealed class WindowDotGroup : SpeckledFlow
{
    private bool hovered;

    public WindowDotGroup()
    {
        WrapContents = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = UiPalette.Window;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
    }

    public WindowDotButton AddDot(Color color, WindowDotGlyph glyph, Action onClick)
    {
        var button = new WindowDotButton(color, glyph) { ShowGlyph = () => hovered };
        button.Click += (_, _) => onClick();
        button.MouseEnter += (_, _) => SyncHover();
        button.MouseLeave += (_, _) => SyncHover();
        Controls.Add(button);
        return button;
    }

    protected override void OnMouseEnter(EventArgs e) { SyncHover(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { SyncHover(); base.OnMouseLeave(e); }

    // Sibling MouseLeave fires before the next MouseEnter, so re-test the pointer
    // against the whole group to keep the glyphs from flickering between dots.
    private void SyncHover()
    {
        var next = ClientRectangle.Contains(PointToClient(MousePosition));
        if (hovered == next) return;
        hovered = next;
        foreach (Control control in Controls) control.Invalidate();
    }
}

internal sealed class MacSlider : Control
{
    private int value = 43;
    private bool dragging;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; set; } = 20;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; set; } = 80;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            var next = Math.Clamp(value, Minimum, Maximum);
            if (this.value == next) return;
            this.value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? ValueChanged;

    public MacSlider()
    {
        Height = 28;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseDown(MouseEventArgs e) { dragging = true; SetFromMouse(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromMouse(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }
    private void SetFromMouse(int x) => Value = Minimum + (int)Math.Round(Math.Clamp((x - 8f) / Math.Max(1, Width - 16), 0, 1) * (Maximum - Minimum));

    protected override void OnPaint(PaintEventArgs e)
    {
        SpeckleField.PaintHost(e.Graphics, this);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var y = Height / 2;
        var usable = Math.Max(1, Width - 16);
        var px = 8 + (int)(usable * ((Value - Minimum) / (float)Math.Max(1, Maximum - Minimum)));
        using var rest = new Pen(UiPalette.Border, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var active = new Pen(UiPalette.Accent, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var knob = new SolidBrush(UiPalette.OnAccentContainer);
        e.Graphics.DrawLine(rest, 8, y, Width - 8, y);
        e.Graphics.DrawLine(active, 8, y, px, y);
        e.Graphics.FillEllipse(knob, px - 7, y - 7, 14, 14);
    }
}

/// <summary>
/// Colour swatch on a card. The fill is left flat and unblended so the value reads
/// exactly as entered; only the rim is shaded, and the card's speckle and bevel show
/// through the rounded corner cut-outs.
/// </summary>
internal sealed class ColorChip : Control
{
    private bool hovering;

    public ColorChip()
    {
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        SpeckleField.PaintHost(e.Graphics, this);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var body = new Rectangle(0, 0, Width - 1, Height - 1);
        if (body.Width <= 2 || body.Height <= 2) return;
        var radius = Math.Clamp(Math.Min(body.Width, body.Height) / 4, 3, 8);
        using var path = RoundedPanel.RoundRect(body, radius);

        using (var fill = new SolidBrush(BackColor))
            g.FillPath(fill, path);

        // Rim only. A gradient across the face would misreport the colour.
        using (var edge = new LinearGradientBrush(
                   new Rectangle(body.X, body.Y - 1, body.Width, body.Height + 2),
                   Color.FromArgb(hovering ? 138 : 92, 255, 255, 255),
                   Color.FromArgb(hovering ? 44 : 74, 0, 0, 0),
                   LinearGradientMode.Vertical))
        using (var pen = new Pen(edge, 1f))
            g.DrawPath(pen, path);
    }
}

/// <summary>
/// Pop-up button replacing the system combo: a recessed well with a raised chevron
/// tab, opening a rounded speckled list instead of the white Win32 drop-down.
/// </summary>
internal sealed class MacComboBox : Control
{
    private readonly List<string> items = [];
    private int selectedIndex = -1;
    private bool hovering;
    private ComboPopup? popup;
    private DateTime closedAt = DateTime.MinValue;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 6;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<string> Items => items;

    public event EventHandler? SelectedIndexChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            var next = items.Count == 0 ? -1 : Math.Clamp(value, -1, items.Count - 1);
            if (selectedIndex == next) return;
            selectedIndex = next;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public override string Text =>
        selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : string.Empty;

    internal bool IsOpen => popup is { IsDisposed: false };

    public MacComboBox()
    {
        Height = 26;
        Cursor = Cursors.Hand;
        BackColor = UiPalette.Input;
        ForeColor = UiPalette.Text;
        Font = UiFonts.Sans(8.5f);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        Toggle();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space or Keys.Enter or Keys.F4:
                Toggle();
                break;
            case Keys.Down:
                SelectedIndex = Math.Min(selectedIndex + 1, items.Count - 1);
                break;
            case Keys.Up:
                SelectedIndex = Math.Max(selectedIndex - 1, 0);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
    }

    internal void Commit(int index)
    {
        SelectedIndex = index;
        popup?.Close();
    }

    private void Toggle()
    {
        if (IsOpen)
        {
            popup?.Close();
            return;
        }
        // The popup closes on deactivate, so a click on this control arrives just
        // after it went away. Without a guard that click would reopen it instantly.
        if ((DateTime.UtcNow - closedAt).TotalMilliseconds < 200) return;
        if (items.Count == 0) return;

        var window = new ComboPopup(this);
        window.FormClosed += (_, _) =>
        {
            closedAt = DateTime.UtcNow;
            popup = null;
            Invalidate();
        };
        popup = window;
        window.ShowFor(this);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        SpeckleField.PaintHost(e.Graphics, this);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var body = new Rectangle(0, 0, Width - 1, Height - 1);
        if (body.Width <= 24 || body.Height <= 6) return;
        var span = new Rectangle(body.X, body.Y - 1, body.Width, body.Height + 2);
        using var path = RoundedPanel.RoundRect(body, Radius);

        // The well is dark at the top and lifts towards the bottom, the inverse of
        // the raised buttons, so the field reads as sunk into the card.
        using (var well = new LinearGradientBrush(
                   span, ColorFx.Darken(BackColor, 0.36f), ColorFx.Lighten(BackColor, 0.12f),
                   LinearGradientMode.Vertical))
            g.FillPath(well, path);

        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        using (var recess = new LinearGradientBrush(
                   new Rectangle(body.X, body.Y - 1, body.Width, 6),
                   Color.FromArgb(104, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Vertical))
            g.FillRectangle(recess, body.X, body.Y, body.Width, 5);
        g.Restore(state);

        // Inverted fresnel: shade across the top arc, light catching the bottom lip.
        using (var edge = new LinearGradientBrush(
                   span, Color.FromArgb(128, 0, 0, 0), Color.FromArgb(hovering ? 86 : 58, 255, 255, 255),
                   LinearGradientMode.Vertical))
        using (var pen = new Pen(edge, 1f))
            g.DrawPath(pen, path);

        if (Focused && !IsOpen)
        {
            using var ring = new Pen(Color.FromArgb(96, UiPalette.Accent), 1f);
            g.DrawPath(ring, path);
        }

        var tab = new Rectangle(body.Right - 23, body.Y + 3, 20, body.Height - 7);
        if (tab.Width > 0 && tab.Height > 0) DrawChevronTab(g, tab);

        var label = new Rectangle(body.X + 10, body.Y, tab.Left - body.X - 15, body.Height);
        if (label.Width > 0)
            UiText.Draw(g, Text, Font, label, ForeColor, StringAlignment.Near);
    }

    private void DrawChevronTab(Graphics g, Rectangle tab)
    {
        var span = new Rectangle(tab.X, tab.Y - 1, tab.Width, tab.Height + 2);
        using var path = RoundedPanel.RoundRect(tab, 4);
        var face = hovering || IsOpen ? UiPalette.PanelHover : UiPalette.PanelRaised;
        var top = IsOpen ? ColorFx.Darken(face, 0.20f) : ColorFx.Lighten(face, 0.24f);
        var bottom = IsOpen ? ColorFx.Lighten(face, 0.06f) : ColorFx.Darken(face, 0.18f);
        using (var fill = new LinearGradientBrush(span, top, bottom, LinearGradientMode.Vertical))
            g.FillPath(fill, path);
        using (var edge = new LinearGradientBrush(
                   span, Color.FromArgb(IsOpen ? 26 : 96, 255, 255, 255), Color.FromArgb(34, 0, 0, 0),
                   LinearGradientMode.Vertical))
        using (var pen = new Pen(edge, 1f))
            g.DrawPath(pen, path);

        var cx = tab.Left + tab.Width / 2f;
        var cy = tab.Top + tab.Height / 2f + (IsOpen ? 0.5f : 0f);
        using var chevron = new Pen(hovering || IsOpen ? UiPalette.Text : UiPalette.Muted, 1.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(chevron, [
            new PointF(cx - 3.2f, cy - 1.9f), new PointF(cx, cy - 4.7f), new PointF(cx + 3.2f, cy - 1.9f)
        ]);
        g.DrawLines(chevron, [
            new PointF(cx - 3.2f, cy + 1.9f), new PointF(cx, cy + 4.7f), new PointF(cx + 3.2f, cy + 1.9f)
        ]);
    }
}

/// <summary>The list window for <see cref="MacComboBox"/>, drawn to match the dark chrome.</summary>
internal sealed class ComboPopup : Form
{
    private const int RowHeight = 26;
    private const int EdgePad = 5;
    private const int MaxRows = 9;
    private const int Corner = 8;

    private readonly MacComboBox owner;
    private readonly Font font;
    private readonly int visibleRows;
    private int highlight;
    private int scroll;

    internal ComboPopup(MacComboBox owner)
    {
        this.owner = owner;
        font = UiFonts.Sans(8.5f);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = UiPalette.PanelRaised;
        Text = "AfterThemed Preset List";

        visibleRows = Math.Clamp(owner.Items.Count, 1, MaxRows);
        highlight = Math.Max(0, owner.SelectedIndex);
        scroll = Math.Clamp(highlight - visibleRows / 2, 0, Math.Max(0, owner.Items.Count - visibleRows));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    // CS_DROPSHADOW. The shadow follows the rounded region, lifting the list off the
    // dark chrome without a layered window.
    protected override CreateParams CreateParams
    {
        get
        {
            var created = base.CreateParams;
            created.ClassStyle |= 0x00020000;
            return created;
        }
    }

    internal void ShowFor(MacComboBox anchor)
    {
        var size = new Size(Math.Max(anchor.Width, 140), visibleRows * RowHeight + EdgePad * 2);
        var below = anchor.PointToScreen(new Point(0, anchor.Height + 3));
        var work = Screen.FromControl(anchor).WorkingArea;
        if (below.Y + size.Height > work.Bottom)
            below.Y = anchor.PointToScreen(Point.Empty).Y - size.Height - 3;
        below.X = Math.Clamp(below.X, work.Left, Math.Max(work.Left, work.Right - size.Width));
        below.Y = Math.Clamp(below.Y, work.Top, Math.Max(work.Top, work.Bottom - size.Height));

        Bounds = new Rectangle(below, size);
        using (var path = RoundedPanel.RoundRect(new Rectangle(Point.Empty, size), Corner))
            Region = new Region(path);

        Show(anchor.FindForm());
        Activate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Close();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexAt(e.Location);
        if (index < 0 || index == highlight) return;
        highlight = index;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var index = IndexAt(e.Location);
        if (index >= 0) owner.Commit(index);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var max = Math.Max(0, owner.Items.Count - visibleRows);
        if (max == 0) return;
        scroll = Math.Clamp(scroll - Math.Sign(e.Delta), 0, max);
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape:
                Close();
                return true;
            case Keys.Enter or Keys.Space:
                owner.Commit(highlight);
                return true;
            case Keys.Down:
                MoveTo(highlight + 1);
                return true;
            case Keys.Up:
                MoveTo(highlight - 1);
                return true;
            case Keys.Home:
                MoveTo(0);
                return true;
            case Keys.End:
                MoveTo(owner.Items.Count - 1);
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void MoveTo(int index)
    {
        if (owner.Items.Count == 0) return;
        highlight = Math.Clamp(index, 0, owner.Items.Count - 1);
        if (highlight < scroll) scroll = highlight;
        else if (highlight >= scroll + visibleRows) scroll = highlight - visibleRows + 1;
        scroll = Math.Clamp(scroll, 0, Math.Max(0, owner.Items.Count - visibleRows));
        Invalidate();
    }

    private int IndexAt(Point point)
    {
        if (point.Y < EdgePad) return -1;
        var row = (point.Y - EdgePad) / RowHeight;
        if (row < 0 || row >= visibleRows) return -1;
        var index = scroll + row;
        return index < owner.Items.Count ? index : -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPanel.RoundRect(bounds, Corner);
        // The popup is its own top-level window, so the field starts at its own origin.
        SpeckleField.Paint(g, ClientRectangle, Point.Empty, UiPalette.PanelRaised, path,
            new SpeckleField.BevelSpec(bounds, Corner));

        for (var row = 0; row < visibleRows; row++)
        {
            var index = scroll + row;
            if (index >= owner.Items.Count) break;
            DrawRow(g, new Rectangle(4, EdgePad + row * RowHeight, Width - 8, RowHeight), index);
        }

        using (var edge = new LinearGradientBrush(
                   new Rectangle(0, -1, Width, Height + 2),
                   Color.FromArgb(92, 255, 255, 255), Color.FromArgb(70, 0, 0, 0),
                   LinearGradientMode.Vertical))
        using (var pen = new Pen(edge, 1f))
            g.DrawPath(pen, path);
    }

    private void DrawRow(Graphics g, Rectangle rect, int index)
    {
        var selected = index == owner.SelectedIndex;
        var hot = index == highlight;

        if (hot)
        {
            using var rowPath = RoundedPanel.RoundRect(rect, 5);
            var face = ColorFx.Lighten(UiPalette.PanelRaised, 0.18f);
            using (var fill = new LinearGradientBrush(
                       new Rectangle(rect.X, rect.Y - 1, rect.Width, rect.Height + 2),
                       ColorFx.Lighten(face, 0.16f), ColorFx.Darken(face, 0.12f), LinearGradientMode.Vertical))
                g.FillPath(fill, rowPath);
            using var lip = new Pen(Color.FromArgb(58, 255, 255, 255), 1f);
            g.DrawPath(lip, rowPath);
        }
        else if (selected)
        {
            using var rowPath = RoundedPanel.RoundRect(rect, 5);
            using var fill = new SolidBrush(Color.FromArgb(26, 255, 255, 255));
            g.FillPath(fill, rowPath);
        }

        if (selected)
        {
            using var barPath = RoundedPanel.RoundRect(
                new Rectangle(rect.X + 4, rect.Y + 6, 3, rect.Height - 12), 1);
            using var bar = new SolidBrush(UiPalette.Accent);
            g.FillPath(bar, barPath);
        }

        var label = new Rectangle(rect.X + 14, rect.Y, rect.Width - 34, rect.Height);
        UiText.Draw(g, owner.Items[index], font, label,
            selected || hot ? UiPalette.Text : UiPalette.Muted, StringAlignment.Near);

        if (!selected) return;
        using var check = new Pen(UiPalette.Accent, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var cx = rect.Right - 13f;
        var cy = rect.Y + rect.Height / 2f;
        g.DrawLines(check, [
            new PointF(cx - 3.6f, cy + 0.2f), new PointF(cx - 1.2f, cy + 2.8f), new PointF(cx + 3.8f, cy - 2.9f)
        ]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) font.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class InspectorTabs : TabControl
{
    private readonly Font tabFont = UiFonts.Sans(8f, FontStyle.Bold);

    public InspectorTabs()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        ItemSize = new Size(120, 34);
        SizeMode = TabSizeMode.Fixed;
        Padding = new Point(0, 0);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tabFont.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = e.Index == SelectedIndex;
        var rect = GetTabRect(e.Index);
        using var back = new SolidBrush(selected ? UiPalette.PanelRaised : UiPalette.Panel);
        e.Graphics.FillRectangle(back, rect);
        UiText.Draw(e.Graphics, TabPages[e.Index].Text.ToUpperInvariant(), tabFont, rect,
            selected ? UiPalette.Text : UiPalette.Muted, ellipsis: false);
        if (selected)
        {
            using var accent = new SolidBrush(UiPalette.Accent);
            e.Graphics.FillRectangle(accent, rect.Left + 20, rect.Bottom - 2, rect.Width - 40, 2);
        }
    }
}
