using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DvauiThemeEditor;

public partial class Form1 : Form
{
    private enum PanelInstallAction { None, Apply, Restore }
    private sealed record GeneratedThemeFiles(string NativePath, LegacyAeThemeCompanion? Companion);

    private readonly TextBox source = NewTextBox();
    private readonly TextBox target = NewTextBox();
    private readonly TextBox themeName = NewTextBox();
    private readonly MacComboBox preset = NewComboBox();
    private readonly MacComboBox fontChoice = NewComboBox();
    private readonly CheckBox themePanels = NewCheckBox("Theme every detected CEP panel whenever a theme is installed", true);
    private readonly Label panelStatus = NewLabel("PANELS NOT SCANNED", UiPalette.Muted, true);
    private readonly TextBox panelDetails = NewTextBox(multiline: true, readOnly: true);
    private readonly MacSlider cutoff = new() { Minimum = 20, Maximum = 80, Value = 43, Dock = DockStyle.Fill };
    private readonly Label cutoffValue = NewLabel("0.43", UiPalette.Text, true);
    private readonly Label importStatus = NewLabel("NO EXTERNAL THEME · BUILT-IN PRESET", UiPalette.Muted);
    private readonly Panel preview = new() { Dock = DockStyle.Fill, BackColor = UiPalette.Canvas };
    private readonly TextBox log = NewTextBox(multiline: true, readOnly: true);
    private readonly TextBox textReplacements = NewTextBox(multiline: true);
    private readonly Dictionary<string, TextBox> colorBoxes = new();
    private IReadOnlyList<Color> importedColors = Array.Empty<Color>();
    private VerbatimColorPickerForm? activeColorPicker;
    private Panel? titleBar;

    private const string AdobeDefaultFont = "Adobe Clean · original";

    private static readonly (string Label, ThemeSettings Settings)[] BuiltInPresets =
    [
        ("Cyberpunk Burgundy", ThemeSettings.Cyberpunk),
        ("Gruvbox Dark", ThemeSettings.GruvboxDark),
        ("Gruvbox Light", ThemeSettings.GruvboxLight),
        ("Material Lavender (M3)", ThemeSettings.MaterialLavender),
        ("Material Lavender Rich (M3)", ThemeSettings.MaterialLavenderRich),
        ("Hatsune Miku Accessible", ThemeSettings.HatsuneMikuAccessible),
        ("Catppuccin Mocha", ThemeSettings.CatppuccinMocha),
        ("Nord", ThemeSettings.Nord),
        ("Everforest", ThemeSettings.Everforest),
        ("Tokyo Night", ThemeSettings.TokyoNight),
        ("Kanagawa", ThemeSettings.Kanagawa),
        ("Rosé Pine", ThemeSettings.RosePine),
        ("Dracula", ThemeSettings.Dracula),
        ("One Dark Pro", ThemeSettings.OneDarkPro),
        ("Solarized Dark", ThemeSettings.SolarizedDark),
        ("Solarized Light", ThemeSettings.SolarizedLight),
        ("Monokai", ThemeSettings.Monokai),
        ("Ayu Dark", ThemeSettings.AyuDark),
        ("Night Owl", ThemeSettings.NightOwl),
        ("Oxocarbon", ThemeSettings.Oxocarbon),
        ("Synthwave '84", ThemeSettings.Synthwave84),
        ("Material Palenight", ThemeSettings.MaterialPalenight),
    ];

    private string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfterThemed");
    private string Originals => Path.Combine(DataRoot, "Originals");
    private string Variants => Path.Combine(DataRoot, "Variants");
    private string Backups => Path.Combine(DataRoot, "Backups");
    private string Reports => Path.Combine(DataRoot, "Reports");
    private string PanelBackups => Path.Combine(DataRoot, "PanelBackups");
    private string PanelThemeFile => Path.Combine(DataRoot, "panel-theme.json");
    private string PanelReportFile => Path.Combine(DataRoot, "panel-operation.json");
    private string LastTargetFile => Path.Combine(DataRoot, "last-target.txt");

    public Form1()
    {
        InitializeComponent();
        var executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (executableIcon is not null) Icon = executableIcon;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BuildUi();
        LoadDefaults();
    }

    private void BuildUi()
    {
        SuspendLayout();
        var shell = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Window,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        titleBar = BuildTitleBar();
        shell.Controls.Add(titleBar, 0, 0);

        var content = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Window,
            Padding = new Padding(10, 2, 10, 10),
            ColumnCount = 1,
            RowCount = 2
        };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        shell.Controls.Add(content, 0, 1);
        content.Controls.Add(BuildWorkspace(), 0, 0);
        content.Controls.Add(BuildInspectorArea(), 0, 1);
        ResumeLayout(true);
    }

    private Panel BuildTitleBar()
    {
        var bar = new SpeckledPanel { Dock = DockStyle.Fill, BackColor = UiPalette.Window, Padding = new Padding(14, 6, 10, 6) };
        var layout = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UiPalette.Window };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        bar.Controls.Add(layout);

        var left = new SpeckledFlow { Dock = DockStyle.Fill, WrapContents = false, BackColor = UiPalette.Window, Margin = Padding.Empty };
        var dots = new WindowDotGroup();
        dots.AddDot(Color.FromArgb(255, 95, 87), WindowDotGlyph.Close, Close);
        dots.AddDot(Color.FromArgb(254, 188, 46), WindowDotGlyph.Minimize, Minimize);
        dots.AddDot(Color.FromArgb(40, 200, 64), WindowDotGlyph.Maximize, ToggleMaximize);
        left.Controls.Add(dots);
        var product = NewLabel("AFTERTHEMED  /  BY DRERACHI", UiPalette.Muted, true);
        product.AutoSize = true;
        product.Margin = new Padding(12, 8, 0, 0);
        left.Controls.Add(product);
        layout.Controls.Add(left, 0, 0);

        var centerTitle = NewLabel("AfterThemed by Drerachi", UiPalette.Text);
        centerTitle.Dock = DockStyle.Fill;
        centerTitle.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(centerTitle, 1, 0);

        var actions = new SpeckledFlow
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = UiPalette.Window,
            Margin = Padding.Empty
        };
        AddButton(actions, "INSTALL", GenerateAndInstall, true, 84);
        AddButton(actions, "GENERATE", GenerateVariant, false, 88);
        AddButton(actions, "ABOUT AFTERTHEMED", ShowAboutAfterThemed, false, 144);
        layout.Controls.Add(actions, 2, 0);

        foreach (Control control in new Control[] { bar, layout, product, centerTitle })
        {
            control.MouseDown += DragWindow;
            control.DoubleClick += (_, _) => ToggleMaximize();
        }
        return bar;
    }

    private void ShowAboutAfterThemed()
    {
        using var about = new AboutAfterThemedForm();
        about.ShowDialog(this);
    }

    private Control BuildWorkspace()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Canvas,
            Radius = 10,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12, 8, 12, 7)
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = UiPalette.Canvas };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        card.Controls.Add(layout);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = UiPalette.Canvas };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = UiPalette.Canvas, Margin = Padding.Empty };
        AddButton(left, "↶", () => ApplyPreset(), false, 34, light: true);
        AddButton(left, "IMPORT THEME…", ImportTheme, false, 124, light: true);
        toolbar.Controls.Add(left, 0, 0);
        var canvasTitle = NewLabel("LIVE THEME PREVIEW", UiPalette.CanvasText, true);
        canvasTitle.Dock = DockStyle.Fill;
        canvasTitle.TextAlign = ContentAlignment.MiddleCenter;
        toolbar.Controls.Add(canvasTitle, 1, 0);
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = UiPalette.Canvas, Margin = Padding.Empty };
        AddButton(right, "OPEN OUTPUT", () => OpenFolder(Variants), false, 110, light: true);
        toolbar.Controls.Add(right, 2, 0);
        layout.Controls.Add(toolbar, 0, 0);

        preview.Paint += PaintPreview;
        preview.Resize += (_, _) => preview.Invalidate();
        layout.Controls.Add(preview, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = UiPalette.Canvas };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        importStatus.Dock = DockStyle.Fill;
        importStatus.TextAlign = ContentAlignment.MiddleLeft;
        importStatus.ForeColor = UiPalette.CanvasMuted;
        footer.Controls.Add(importStatus, 0, 0);
        var hint = NewLabel("COLORS UPDATE LIVE  ·  READY TO EXPORT", UiPalette.CanvasMuted, true);
        hint.Dock = DockStyle.Fill;
        hint.TextAlign = ContentAlignment.MiddleRight;
        footer.Controls.Add(hint, 1, 0);
        layout.Controls.Add(footer, 0, 2);
        return card;
    }

    private Control BuildInspectorArea()
    {
        var grid = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiPalette.Window };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.Controls.Add(BuildFilePanel(), 0, 0);
        grid.Controls.Add(BuildControlsPanel(), 1, 0);
        return grid;
    }

    private Control BuildFilePanel()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Panel,
            Radius = 9,
            BorderColor = UiPalette.Border,
            Speckle = true,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(13, 10, 13, 10)
        };
        var layout = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = UiPalette.Panel };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(layout);
        layout.Controls.Add(SectionLabel("PROJECT"), 0, 0);
        layout.Controls.Add(Field("THEME NAME", themeName), 0, 1);
        preset.Items.AddRange(BuiltInPresets.Select(p => p.Label).Append("Custom / Imported"));
        preset.SelectedIndexChanged += (_, _) => ApplyPreset();
        layout.Controls.Add(Field("PRESET", preset), 0, 2);
        source.ReadOnly = true;
        layout.Controls.Add(PathField("PRESERVED ORIGINAL", source, () => OpenFolder(Originals)), 0, 3);
        layout.Controls.Add(PathField("INSTALLED TARGET", target, PickTargetDll), 0, 4);
        var utilities = new SpeckledFlow { Dock = DockStyle.Fill, BackColor = UiPalette.Panel, WrapContents = false, Margin = new Padding(0, 4, 0, 3) };
        AddButton(utilities, "INVENTORY", Inventory, false, 92);
        AddButton(utilities, "RESTORE", Restore, false, 78);
        AddButton(utilities, "FOLDER", () => OpenFolder(DataRoot), false, 72);
        layout.Controls.Add(utilities, 0, 5);
        log.Font = UiFonts.Mono(7.8f);
        log.BackColor = UiPalette.Input;
        log.ForeColor = UiPalette.Muted;
        log.Margin = new Padding(0, 3, 0, 0);
        layout.Controls.Add(log, 0, 6);
        return card;
    }

    private Control BuildControlsPanel()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Panel,
            Radius = 9,
            BorderColor = UiPalette.Border,
            Speckle = true,
            Margin = Padding.Empty,
            Padding = new Padding(10, 7, 10, 9)
        };
        var shell = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = UiPalette.Panel };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(shell);

        var nav = new SpeckledFlow { Dock = DockStyle.Fill, BackColor = UiPalette.Panel, WrapContents = false, Margin = Padding.Empty };
        var colorsButton = new MacButton
        {
            Text = "COLOR CONTROLS",
            Size = new Size(132, 30),
            BackColor = UiPalette.AccentContainer,
            ForeColor = UiPalette.OnAccentContainer,
            HoverColor = ColorFx.Lighten(UiPalette.AccentContainer, .12f),
            Margin = new Padding(0, 0, 6, 0)
        };
        var textButton = new MacButton
        {
            Text = "UI TEXT REPLACEMENTS",
            Size = new Size(168, 30),
            BackColor = UiPalette.Panel,
            ForeColor = UiPalette.Muted,
            HoverColor = UiPalette.PanelRaised,
            Margin = new Padding(0, 0, 6, 0)
        };
        var panelsButton = new MacButton
        {
            Text = "PANELS & FONT",
            Size = new Size(132, 30),
            BackColor = UiPalette.Panel,
            ForeColor = UiPalette.Muted,
            HoverColor = UiPalette.PanelRaised,
            Margin = new Padding(0, 0, 6, 0)
        };
        nav.Controls.Add(colorsButton);
        nav.Controls.Add(textButton);
        nav.Controls.Add(panelsButton);
        shell.Controls.Add(nav, 0, 0);

        var pageHost = new SpeckledPanel { Dock = DockStyle.Fill, BackColor = UiPalette.Panel, Padding = new Padding(4, 4, 4, 2) };
        var colorsPage = new SpeckledPanel { Dock = DockStyle.Fill, BackColor = UiPalette.Panel };
        var textPage = new SpeckledPanel { Dock = DockStyle.Fill, BackColor = UiPalette.Panel, Visible = false, Padding = new Padding(4) };
        var panelsPage = new SpeckledPanel { Dock = DockStyle.Fill, BackColor = UiPalette.Panel, Visible = false, Padding = new Padding(4) };
        pageHost.Controls.Add(colorsPage);
        pageHost.Controls.Add(textPage);
        pageHost.Controls.Add(panelsPage);
        shell.Controls.Add(pageHost, 0, 1);

        void ShowPage(Control page, MacButton active)
        {
            foreach (var candidate in new Control[] { colorsPage, textPage, panelsPage }) candidate.Visible = candidate == page;
            page.BringToFront();
            foreach (var button in new[] { colorsButton, textButton, panelsButton })
            {
                var selected = button == active;
                button.BackColor = selected ? UiPalette.AccentContainer : UiPalette.Panel;
                button.ForeColor = selected ? UiPalette.OnAccentContainer : UiPalette.Muted;
                button.HoverColor = selected ? ColorFx.Lighten(UiPalette.AccentContainer, .12f) : UiPalette.PanelRaised;
                button.Invalidate();
            }
        }

        colorsButton.Click += (_, _) => ShowPage(colorsPage, colorsButton);
        textButton.Click += (_, _) => ShowPage(textPage, textButton);
        panelsButton.Click += (_, _) => ShowPage(panelsPage, panelsButton);

        var colorsLayout = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = UiPalette.Panel };
        colorsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        colorsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        var swatches = new SpeckledFlow { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiPalette.Panel, Padding = new Padding(0), WrapContents = true };
        AddColorCard(swatches, "App Background", "#6F0623");
        AddColorCard(swatches, "Panel Color", "#6F0623");
        AddColorCard(swatches, "Raised Surface", "#B10A3A");
        AddColorCard(swatches, "UI Text Color", "#FCEE0A");
        AddColorCard(swatches, "Primary Accent", "#FCEE0A");
        AddColorCard(swatches, "Secondary Accent", "#00F0FF");
        AddColorCard(swatches, "Danger Accent", "#FF003C");
        colorsLayout.Controls.Add(swatches, 0, 0);

        var options = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = UiPalette.Panel, Padding = new Padding(0, 6, 0, 0) };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sliderArea = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = UiPalette.Panel };
        sliderArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        sliderArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliderArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        var cutoffLabel = NewLabel("TEXT CUTOFF", UiPalette.Muted, true);
        cutoffLabel.Dock = DockStyle.Fill;
        cutoffLabel.TextAlign = ContentAlignment.MiddleLeft;
        sliderArea.Controls.Add(cutoffLabel, 0, 0);
        cutoff.ValueChanged += (_, _) => { cutoffValue.Text = (cutoff.Value / 100f).ToString("0.00"); UpdatePreview(); };
        sliderArea.Controls.Add(cutoff, 1, 0);
        cutoffValue.Dock = DockStyle.Fill;
        cutoffValue.TextAlign = ContentAlignment.MiddleRight;
        sliderArea.Controls.Add(cutoffValue, 2, 0);
        options.Controls.Add(sliderArea, 0, 0);
        colorsLayout.Controls.Add(options, 0, 1);
        colorsPage.Controls.Add(colorsLayout);

        var textLayout = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = UiPalette.Panel };
        textLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        textLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var explanation = NewLabel("ONE REPLACEMENT PER LINE  ·  ORIGINAL TEXT => NEW TEXT  ·  NEW TEXT MUST BE THE SAME LENGTH OR SHORTER", UiPalette.Muted, true);
        explanation.Dock = DockStyle.Fill;
        explanation.TextAlign = ContentAlignment.MiddleLeft;
        textLayout.Controls.Add(explanation, 0, 0);
        textReplacements.Font = UiFonts.Mono(9f);
        textReplacements.PlaceholderText = "AdobeClean-Regular => SFProDisplay-Regular\r\n# Lines beginning with # are ignored";
        textLayout.Controls.Add(textReplacements, 0, 1);
        textPage.Controls.Add(textLayout);

        var panelsLayout = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = UiPalette.Panel };
        panelsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        panelsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panelsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panelsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panelsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panelsLayout.Controls.Add(Field("DVAUI FONT · SAFE INSTALLED FAMILIES", fontChoice), 0, 0);
        panelsLayout.Controls.Add(themePanels, 0, 1);
        var panelActions = new SpeckledFlow { Dock = DockStyle.Fill, BackColor = UiPalette.Panel, WrapContents = false, Margin = Padding.Empty };
        AddButton(panelActions, "RESCAN PANELS", RefreshPanelDiscovery, false, 122);
        AddButton(panelActions, "APPLY PANELS NOW", ApplyPanelsOnly, false, 148);
        panelsLayout.Controls.Add(panelActions, 0, 2);
        panelStatus.Dock = DockStyle.Fill;
        panelStatus.TextAlign = ContentAlignment.MiddleLeft;
        panelsLayout.Controls.Add(panelStatus, 0, 3);
        panelDetails.Font = UiFonts.Mono(7.8f);
        panelDetails.BackColor = UiPalette.Input;
        panelDetails.ForeColor = UiPalette.Muted;
        panelDetails.WordWrap = false;
        panelsLayout.Controls.Add(panelDetails, 0, 4);
        panelsPage.Controls.Add(panelsLayout);
        return card;
    }

    private Control BuildAboutPage()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Input,
            BorderColor = UiPalette.Border,
            BorderWidth = 1,
            Radius = 10,
            Speckle = true,
            Padding = new Padding(24, 20, 20, 18),
            Margin = Padding.Empty
        };

        var layout = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Input,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        card.Controls.Add(layout);

        var identity = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Input,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, 0, 24, 2)
        };
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var eyebrow = NewLabel("SYSTEM APPEARANCE TOOLKIT  /  WINDOWS X64", UiPalette.AccentHover, true);
        eyebrow.Dock = DockStyle.Fill;
        eyebrow.TextAlign = ContentAlignment.MiddleLeft;
        eyebrow.Font = UiFonts.Mono(7.7f, FontStyle.Bold);
        identity.Controls.Add(eyebrow, 0, 0);

        var title = NewLabel("AFTERTHEMED", UiPalette.Text, true);
        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Font = UiFonts.Sans(23f, FontStyle.Bold);
        identity.Controls.Add(title, 0, 1);

        var version = NewLabel($"VERSION {Application.ProductVersion}  ·  © {DateTime.Now.Year}", UiPalette.Muted, true);
        version.Dock = DockStyle.Fill;
        version.TextAlign = ContentAlignment.MiddleLeft;
        version.Font = UiFonts.Mono(8f, FontStyle.Bold);
        identity.Controls.Add(version, 0, 2);

        var accentLine = new Panel
        {
            BackColor = UiPalette.Accent,
            Size = new Size(46, 3),
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };
        identity.Controls.Add(accentLine, 0, 3);

        var description = NewLabel(
            "A precision theming workspace for Adobe After Effects. Build, preview, install, and restore DVAUI and CEP palettes from one controlled Windows workflow.",
            UiPalette.Muted);
        description.Dock = DockStyle.Fill;
        description.TextAlign = ContentAlignment.MiddleLeft;
        description.Font = UiFonts.Sans(9.2f);
        identity.Controls.Add(description, 0, 4);
        layout.Controls.Add(identity, 0, 0);

        var connect = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Input,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0, 0, 16, 0),
            Padding = new Padding(6, 0, 10, 0)
        };
        connect.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        for (var index = 0; index < 4; index++) connect.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var connectTitle = NewLabel("CONNECT", UiPalette.Muted, true);
        connectTitle.Dock = DockStyle.Fill;
        connectTitle.TextAlign = ContentAlignment.MiddleLeft;
        connectTitle.Font = UiFonts.Mono(7.5f, FontStyle.Bold);
        connect.Controls.Add(connectTitle, 0, 0);
        connect.Controls.Add(NewSocialLink("X  /  @SHONENVII", "https://x.com/shonenvii"), 0, 1);
        connect.Controls.Add(NewSocialLink("YOUTUBE  /  SHONENSHWTY", "https://youtube.com/shonenshwty"), 0, 2);
        connect.Controls.Add(NewSocialLink("INSTAGRAM  /  @RIPSHONEN", "https://instagram.com/ripshonen"), 0, 3);
        connect.Controls.Add(NewSocialLink("DISCORD  /  BLANK", "https://discord.gg/blank"), 0, 4);
        layout.Controls.Add(connect, 1, 0);

        var acknowledgements = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.PanelRaised,
            BorderColor = UiPalette.AccentContainer,
            BorderWidth = 1,
            Radius = 8,
            Speckle = true,
            Padding = new Padding(14, 7, 14, 7),
            Margin = new Padding(0, 8, 16, 0)
        };
        var thanksLayout = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.PanelRaised,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty
        };
        thanksLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        thanksLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        thanksLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        thanksLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var thanksTitle = NewLabel("CREATED BY DRERACHI  ·  THANKS TO MY FAMILY IN THE BLANK SERVER!", UiPalette.Text, true);
        thanksTitle.Dock = DockStyle.Fill;
        thanksTitle.TextAlign = ContentAlignment.MiddleLeft;
        thanksTitle.Font = UiFonts.Sans(7.2f, FontStyle.Bold);
        thanksLayout.Controls.Add(thanksTitle, 0, 0);

        var thanksBody = NewLabel(
            "SPECIAL THANKS: DALLAS · JAIDON · ITO · STAR — ESPECIALLY TEWZY, FOR PUSHING ME TO DO THIS FUN PROJECT! YOU'RE THE BEST LOSER :D",
            UiPalette.Muted);
        thanksBody.Dock = DockStyle.Fill;
        thanksBody.TextAlign = ContentAlignment.MiddleLeft;
        thanksBody.Font = UiFonts.Sans(7.5f);
        thanksLayout.Controls.Add(thanksBody, 0, 1);

        var hashtags = NewLabel("#Blank2026\n#bringbackrealprogramming", UiPalette.AccentHover, true);
        hashtags.Dock = DockStyle.Fill;
        hashtags.TextAlign = ContentAlignment.MiddleRight;
        hashtags.Font = UiFonts.Sans(7.2f, FontStyle.Bold);
        thanksLayout.Controls.Add(hashtags, 1, 0);
        thanksLayout.SetRowSpan(hashtags, 2);
        acknowledgements.Controls.Add(thanksLayout);
        layout.Controls.Add(acknowledgements, 0, 1);
        layout.SetColumnSpan(acknowledgements, 2);

        var markArea = new SpeckledTable
        {
            Dock = DockStyle.Fill,
            BackColor = UiPalette.Input,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        markArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        markArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        markArea.Controls.Add(new AfterThemedMark { Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 2) }, 0, 0);
        var markCaption = NewLabel($"AFTERTHEMED  /  {DateTime.Now.Year}", UiPalette.Muted, true);
        markCaption.Dock = DockStyle.Fill;
        markCaption.TextAlign = ContentAlignment.MiddleCenter;
        markCaption.Font = UiFonts.Mono(7f, FontStyle.Bold);
        markArea.Controls.Add(markCaption, 0, 1);
        layout.Controls.Add(markArea, 2, 0);
        layout.SetRowSpan(markArea, 2);

        return card;
    }

    private LinkLabel NewSocialLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = UiPalette.Input,
            ForeColor = UiPalette.AccentHover,
            LinkColor = UiPalette.AccentHover,
            ActiveLinkColor = UiPalette.Text,
            VisitedLinkColor = UiPalette.AccentHover,
            LinkBehavior = LinkBehavior.HoverUnderline,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UiFonts.Mono(8.1f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = true,
            AccessibleName = $"Open {text}"
        };
        link.LinkClicked += (_, _) => Try(() => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
        return link;
    }

    private void LoadDefaults()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(Originals);
        Directory.CreateDirectory(Variants);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Reports);
        Directory.CreateDirectory(PanelBackups);

        var savedTarget = File.Exists(LastTargetFile) ? File.ReadAllText(LastTargetFile).Trim() : string.Empty;
        var installations = AfterEffectsLocator.FindInstalled();
        var selectedTarget = File.Exists(savedTarget)
            ? savedTarget
            : installations.FirstOrDefault()?.DllPath ?? string.Empty;
        target.Text = selectedTarget;
        if (File.Exists(selectedTarget))
        {
            try
            {
                var original = OriginalDllStore.CaptureIfMissing(selectedTarget, Originals, out var captured);
                source.Text = original;
                SaveLastTarget(selectedTarget);
                Log(captured
                    ? $"Saved immutable original: {original}"
                    : $"Using preserved original: {original}");
            }
            catch (Exception ex)
            {
                source.Text = string.Empty;
                Log("Could not preserve the detected DLL · " + ex.Message);
            }
        }
        else
        {
            source.Text = string.Empty;
            Log("No After Effects installation was detected · Select the installed dvaui.dll.");
        }
        LoadInstalledFonts();
        preset.SelectedIndex = 5;
        RefreshPanelDiscovery();
        Log($"Ready · Originals, variants, and backups are stored in {DataRoot}");
    }

    private void ImportTheme()
    {
        using var dialog = new OpenFileDialog { Filter = "Theme files (*.theme;*.css;*.json;*.xml)|*.theme;*.css;*.json;*.xml|Windows themes (*.theme)|*.theme|CSS (*.css)|*.css|JSON (*.json)|*.json|XML (*.xml)|*.xml" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        Try(() =>
        {
            var imported = ThemeImporter.Load(dialog.FileName);
            preset.SelectedIndex = BuiltInPresets.Length;
            themeName.Text = imported.Name;
            importedColors = imported.Colors;
            SetColors(imported.Suggested);
            importStatus.Text = $"{Path.GetFileName(dialog.FileName).ToUpperInvariant()}  ·  {imported.Colors.Count} COLORS";
            Log($"Imported {dialog.FileName}\r\nMapped {imported.Colors.Count} unique colors onto solid Spectrum roles.");
        });
    }

    private void ApplyPreset()
    {
        if (preset.SelectedIndex < 0 || preset.SelectedIndex >= BuiltInPresets.Length) return;
        var settings = BuiltInPresets[preset.SelectedIndex].Settings;
        importedColors = Array.Empty<Color>();
        importStatus.Text = "BUILT-IN PRESET  ·  LIVE PREVIEW";
        SetColors(settings);
        themeName.Text = preset.Text.Replace(' ', '-');
    }

    private void SetColors(ThemeSettings s)
    {
        colorBoxes["App Background"].Text = Hex(s.Background);
        colorBoxes["Panel Color"].Text = Hex(s.Panel);
        colorBoxes["Raised Surface"].Text = Hex(s.Raised);
        colorBoxes["UI Text Color"].Text = Hex(s.Text);
        colorBoxes["Primary Accent"].Text = Hex(s.Primary);
        colorBoxes["Secondary Accent"].Text = Hex(s.Secondary);
        colorBoxes["Danger Accent"].Text = Hex(s.Danger);
        cutoff.Value = (int)(s.TextCutoff * 100);
        UpdatePreview();
    }

    private ThemeSettings ReadSettings()
    {
        var selected = preset.SelectedIndex >= 0 && preset.SelectedIndex < BuiltInPresets.Length
            ? BuiltInPresets[preset.SelectedIndex].Settings
            : null;
        return new(
            ReadColor("App Background"), ReadColor("Panel Color"), ReadColor("Raised Surface"), ReadColor("UI Text Color"),
            ReadColor("Primary Accent"), ReadColor("Secondary Accent"), ReadColor("Danger Accent"), cutoff.Value / 100f,
            selected?.ExactAccents ?? false,
            selected?.ForegroundAlphaFloor ?? 0f);
    }

    private GeneratedThemeFiles GenerateTo(string fileName)
    {
        var original = EnsureOriginalSnapshot();
        var settings = ReadSettings();
        var output = Path.Combine(Variants, fileName);
        var hash = ThemePatcher.Generate(original, output, settings, ReadFontFamily(), ReadTextReplacements());
        Log($"Generated: {output}\r\nSHA-256: {hash}");
        var companionName = fileName.StartsWith("dvaui.", StringComparison.OrdinalIgnoreCase)
            ? "AfterFXLib." + fileName["dvaui.".Length..]
            : "AfterFXLib." + fileName;
        var companion = LegacyAeThemePatcher.GenerateForDvaui(
            target.Text, Originals, Path.Combine(Variants, companionName), settings);
        if (companion is not null)
            Log($"Generated native theme companion: {companion.InputPath}\r\nSHA-256: {companion.Sha256}");
        return new GeneratedThemeFiles(output, companion);
    }

    private void GenerateVariant() => Try(() => _ = GenerateTo($"dvaui.{SafeName()}.dll"));

    private void GenerateAndInstall()
    {
        Try(() =>
        {
            if (Process.GetProcessesByName("AfterFX").Length > 0) throw new InvalidOperationException("Close After Effects before installing.");
            var output = GenerateTo("dvaui.install-ready.dll");
            if (themePanels.Checked)
            {
                PanelThemeManager.SaveConfiguration(PanelThemeFile, ReadSettings(), themeName.Text, ReadFontFamily());
                InstallElevated(output.NativePath, "Installation", PanelInstallAction.Apply, PanelThemeFile,
                    output.Companion);
            }
            else InstallElevated(output.NativePath, "Installation", companion: output.Companion);
        });
    }

    private void InstallElevated(string input, string operation, PanelInstallAction panelAction = PanelInstallAction.None,
        string? panelConfiguration = null, LegacyAeThemeCompanion? companion = null)
    {
        if (companion is not null)
        {
            InstallThemeSetElevated(input, operation, panelAction, panelConfiguration, companion);
            return;
        }

        var nativeInstallReportFile = Path.Combine(Reports,
            $"native-install-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.json");
        var arguments = panelAction switch
        {
            PanelInstallAction.Apply => $"--install-with-panel-apply {Quote(input)} {Quote(target.Text)} {Quote(Backups)} {Quote(PanelBackups)} {Quote(panelConfiguration!)} {Quote(PanelReportFile)} {Quote(nativeInstallReportFile)}",
            PanelInstallAction.Restore => $"--install-with-panel-restore {Quote(input)} {Quote(target.Text)} {Quote(Backups)} {Quote(PanelBackups)} {Quote(PanelReportFile)} {Quote(nativeInstallReportFile)}",
            _ => $"--install {Quote(input)} {Quote(target.Text)} {Quote(Backups)} {Quote(nativeInstallReportFile)}"
        };
        if (panelAction != PanelInstallAction.None && File.Exists(PanelReportFile)) File.Delete(PanelReportFile);
        var psi = new ProcessStartInfo { FileName = Environment.ProcessPath!, UseShellExecute = true, Verb = "runas", Arguments = arguments };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start the elevated installer.");
        process.WaitForExit();
        var nativeReport = NativeInstallReportStore.TryRead(nativeInstallReportFile);
        NativeInstallVerifier.EnsureNativeInstallSucceeded(process.ExitCode, input, target.Text, operation,
            nativeReport, nativeInstallReportFile);
        if (panelAction != PanelInstallAction.None)
        {
            if (!File.Exists(PanelReportFile))
                throw new InvalidOperationException($"{operation} installed the DLL, but the panel operation did not return a report.");
            LogPanelReport(JsonSerializer.Deserialize<PanelOperationReport>(File.ReadAllText(PanelReportFile)));
            if (process.ExitCode == 8)
                throw new InvalidOperationException($"{operation} installed the DLL, but one or more panel files had conflicts and were left unchanged. See the activity log.");
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{operation} installed the DLL, but the panel operation failed (exit code {process.ExitCode}). See the activity log.");
        }
        else if (process.ExitCode != 0)
            throw new InvalidOperationException($"{operation} failed or was cancelled.");
        Log($"{operation} completed. A timestamped backup was saved in {Backups}.");
        try { File.Delete(nativeInstallReportFile); } catch { /* Keep a harmless success report if cleanup is blocked. */ }
    }

    private void InstallThemeSetElevated(string nativeInput, string operation, PanelInstallAction panelAction,
        string? panelConfiguration, LegacyAeThemeCompanion companion)
    {
        Directory.CreateDirectory(Reports);
        var id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        var manifestPath = Path.Combine(Reports, $"theme-file-set-{id}.json");
        var reportPath = Path.Combine(Reports, $"theme-file-set-result-{id}.json");
        var manifest = new ThemeFileSetManifest(Backups,
        [
            new ThemeFileInstall(companion.InputPath, companion.TargetPath),
            new ThemeFileInstall(nativeInput, target.Text)
        ]);
        ThemeFileSetStore.WriteManifest(manifestPath, manifest);

        var arguments = panelAction switch
        {
            PanelInstallAction.Apply =>
                $"--install-theme-set-with-panel-apply {Quote(manifestPath)} {Quote(reportPath)} {Quote(PanelBackups)} {Quote(panelConfiguration!)} {Quote(PanelReportFile)}",
            PanelInstallAction.Restore =>
                $"--install-theme-set-with-panel-restore {Quote(manifestPath)} {Quote(reportPath)} {Quote(PanelBackups)} {Quote(PanelReportFile)}",
            _ => $"--install-theme-set {Quote(manifestPath)} {Quote(reportPath)}"
        };
        if (panelAction != PanelInstallAction.None && File.Exists(PanelReportFile)) File.Delete(PanelReportFile);
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = arguments
        };
        using var process = Process.Start(psi) ??
                            throw new InvalidOperationException("Could not start the elevated theme file-set installer.");
        process.WaitForExit();
        var report = ThemeFileSetStore.TryReadReport(reportPath);
        ThemeFileSetVerifier.EnsureSucceeded(process.ExitCode, manifest, report, reportPath, operation);

        if (panelAction != PanelInstallAction.None)
        {
            if (!File.Exists(PanelReportFile))
                throw new InvalidOperationException(
                    $"{operation} installed the native theme files, but the panel operation did not return a report.");
            LogPanelReport(JsonSerializer.Deserialize<PanelOperationReport>(File.ReadAllText(PanelReportFile)));
            if (process.ExitCode == 8)
                throw new InvalidOperationException(
                    $"{operation} installed the native theme files, but one or more panel files had conflicts and were left unchanged. See the activity log.");
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"{operation} installed the native theme files, but the panel operation failed (exit code {process.ExitCode}). See the activity log.");
        }
        else if (process.ExitCode != 0)
            throw new InvalidOperationException($"{operation} failed or was cancelled.");

        Log($"{operation} completed. Both native theme files were verified and backed up in {Backups}.");
        try { File.Delete(manifestPath); } catch { /* Keep diagnostics when cleanup is blocked. */ }
        try { File.Delete(reportPath); } catch { /* Keep diagnostics when cleanup is blocked. */ }
    }

    private void Inventory()
    {
        Try(() =>
        {
            var original = EnsureOriginalSnapshot();
            Directory.CreateDirectory(Reports);
            var path = Path.Combine(Reports, $"colors-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, ThemePatcher.Inventory(original));
            Log($"DLL color inventory saved: {path}");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }

    private void Restore()
    {
        Try(() =>
        {
            if (Process.GetProcessesByName("AfterFX").Length > 0) throw new InvalidOperationException("Close After Effects before restoring.");
            var targetPath = target.Text.Trim();
            if (targetPath.Length == 0) throw new InvalidOperationException("Select the installed After Effects dvaui.dll first.");
            var restoreDll = OriginalDllStore.CreateRestoreDll(targetPath, Originals,
                Path.Combine(Variants, "dvaui.restore-original.dll"));
            var companion = LegacyAeThemePatcher.CreateRestoreForDvaui(targetPath, Originals,
                Path.Combine(Variants, "AfterFXLib.restore-original.dll"));
            source.Text = OriginalDllStore.RequireExistingOriginal(targetPath, Originals);
            var hash = OriginalDllStore.Sha256(restoreDll);
            Log($"Verified restore DLL created: {restoreDll}\r\nSHA-256: {hash}");
            var panelManifest = Path.Combine(PanelBackups, "panel-backups.json");
            InstallElevated(restoreDll, "Adobe original restore",
                File.Exists(panelManifest) ? PanelInstallAction.Restore : PanelInstallAction.None,
                companion: companion);
            Log("After Effects and all safely modified CEP panel files have been returned to their preserved originals.");
        });
    }

    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        try
        {
            var s = ReadSettings();
            var r = preview.ClientRectangle;
            e.Graphics.Clear(UiPalette.Canvas);
            if (r.Width < 200 || r.Height < 100) return;

            var window = new Rectangle(58, 8, Math.Max(200, r.Width - 116), Math.Max(100, r.Height - 18));
            using (var shadow = new SolidBrush(Color.FromArgb(24, 0, 0, 0)))
                e.Graphics.FillRoundedRectangle(shadow, new Rectangle(window.X + 4, window.Y + 6, window.Width, window.Height), 9);
            using var windowPath = RoundedPanel.RoundRect(window, 8);
            using var background = new SolidBrush(s.Background);
            e.Graphics.FillPath(background, windowPath);

            var top = new Rectangle(window.X, window.Y, window.Width, 25);
            using var topBrush = new SolidBrush(s.Background);
            e.Graphics.FillRectangle(topBrush, top);
            using var smallFont = UiFonts.Sans(6.8f, FontStyle.Bold);
            using var tinyFont = UiFonts.Sans(6.2f);
            using var textBrush = new SolidBrush(s.Text);
            using var mutedBrush = new SolidBrush(Color.FromArgb(172, s.Text));
            e.Graphics.DrawString("DVAUI PREVIEW", smallFont, mutedBrush, window.X + 10, window.Y + 8);
            var themeTitle = string.IsNullOrWhiteSpace(themeName.Text) ? "UNTITLED" : themeName.Text.ToUpperInvariant();
            var titleSize = e.Graphics.MeasureString(themeTitle, smallFont);
            e.Graphics.DrawString(themeTitle, smallFont, mutedBrush, window.X + (window.Width - titleSize.Width) / 2, window.Y + 8);

            var body = new Rectangle(window.X + 8, window.Y + 33, window.Width - 16, window.Height - 41);
            var sideWidth = Math.Clamp(body.Width / 5, 88, 150);
            var sidebar = new Rectangle(body.X, body.Y, sideWidth, body.Height);
            var main = new Rectangle(sidebar.Right + 6, body.Y, body.Width - sideWidth - 6, body.Height);
            using (var sideBrush = new SolidBrush(s.Panel))
                e.Graphics.FillRectangle(sideBrush, sidebar);
            using (var mainBrush = new SolidBrush(s.Raised))
                e.Graphics.FillRectangle(mainBrush, main);

            e.Graphics.DrawString("EFFECTS & PRESETS", smallFont, textBrush, sidebar.X + 10, sidebar.Y + 9);
            e.Graphics.DrawString("Animation Presets\n3D Channel\nBlur & Sharpen\nColor Correction\nDistort\nExpression Controls", tinyFont, textBrush, sidebar.X + 12, sidebar.Y + 30);
            using var linePen = new Pen(Color.FromArgb(80, s.Text), 1);
            e.Graphics.DrawLine(linePen, sidebar.X + 10, sidebar.Bottom - 24, sidebar.Right - 10, sidebar.Bottom - 24);

            var comp = new Rectangle(main.X + 10, main.Y + 10, main.Width - 20, Math.Max(34, (int)(main.Height * .58)));
            using var compBrush = new SolidBrush(s.Background);
            e.Graphics.FillRectangle(compBrush, comp);
            using var primaryBrush = new SolidBrush(s.Primary);
            using var secondaryBrush = new SolidBrush(s.Secondary);
            using var dangerBrush = new SolidBrush(s.Danger);
            var markSize = Math.Max(12, Math.Min(comp.Width, comp.Height) / 4);
            var cx = comp.X + comp.Width / 2;
            var cy = comp.Y + comp.Height / 2;
            e.Graphics.FillEllipse(primaryBrush, cx - markSize, cy - markSize, markSize * 2, markSize * 2);
            e.Graphics.FillEllipse(secondaryBrush, cx - markSize / 2, cy - markSize / 2, markSize, markSize);
            e.Graphics.FillRectangle(dangerBrush, cx - markSize * 2, comp.Bottom - 8, markSize, 3);

            var timelineY = comp.Bottom + 9;
            e.Graphics.DrawString("TIMELINE", smallFont, textBrush, main.X + 11, timelineY);
            for (var i = 0; i < 4; i++)
            {
                var y = timelineY + 15 + i * 8;
                using var track = new Pen(Color.FromArgb(65, s.Text), 2);
                e.Graphics.DrawLine(track, main.X + 12, y, main.Right - 14, y);
                var width = Math.Max(8, (main.Width - 34) * (i + 2) / 6);
                e.Graphics.FillRectangle(i % 2 == 0 ? primaryBrush : secondaryBrush, main.X + 20, y - 1, width, 3);
            }

            if (importedColors.Count > 0)
            {
                var x = window.Right - 13;
                foreach (var c in importedColors.Take(10).Reverse())
                {
                    x -= 9;
                    using var swatch = new SolidBrush(c);
                    e.Graphics.FillEllipse(swatch, x, window.Y + 8, 6, 6);
                }
            }
        }
        catch
        {
            e.Graphics.Clear(UiPalette.ErrorContainer);
        }
    }

    private void AddColorCard(FlowLayoutPanel panel, string name, string value)
    {
        var card = new RoundedPanel
        {
            Size = new Size(154, 62),
            BackColor = UiPalette.PanelRaised,
            BorderColor = UiPalette.Border,
            Radius = 7,
            Speckle = true,
            Margin = new Padding(0, 0, 7, 7),
            Padding = new Padding(9, 6, 8, 6),
            Cursor = Cursors.Hand
        };
        var layout = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = UiPalette.PanelRaised, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = NewLabel(name.ToUpperInvariant(), UiPalette.Muted, true);
        label.Dock = DockStyle.Fill;
        layout.SetColumnSpan(label, 2);
        layout.Controls.Add(label, 0, 0);
        var box = NewTextBox();
        box.Text = value;
        box.Font = UiFonts.Mono(8.5f);
        box.BorderStyle = BorderStyle.None;
        box.BackColor = UiPalette.PanelRaised;
        box.Margin = new Padding(0, 4, 0, 0);
        box.TextChanged += (_, _) => { UpdateColorChip(card, name); UpdatePreview(); };
        layout.Controls.Add(box, 0, 1);
        var chip = new ColorChip { Name = "chip", Dock = DockStyle.Fill, Margin = new Padding(5, 3, 1, 1) };
        chip.Click += (_, _) => PickColor(name, chip);
        layout.Controls.Add(chip, 1, 1);
        card.Controls.Add(layout);
        card.Click += (_, _) => PickColor(name, card);
        colorBoxes[name] = box;
        panel.Controls.Add(card);
        UpdateColorChip(card, name);
    }

    private void UpdateColorChip(Control card, string name)
    {
        try
        {
            var chip = card.Controls.Find("chip", true).FirstOrDefault();
            if (chip is not null && colorBoxes.TryGetValue(name, out var box)) chip.BackColor = ColorTranslator.FromHtml(box.Text.Trim());
        }
        catch { }
    }

    private static Control Field(string label, Control control)
    {
        var field = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = UiPalette.Panel, Margin = Padding.Empty };
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        field.Controls.Add(SectionLabel(label), 0, 0);
        control.Margin = new Padding(0, 0, 0, 2);
        field.Controls.Add(control, 0, 1);
        return field;
    }

    private static Control PathField(string label, TextBox box, Action browse)
    {
        var field = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = UiPalette.Panel, Margin = Padding.Empty };
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        field.Controls.Add(SectionLabel(label), 0, 0);
        var row = new SpeckledTable { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = UiPalette.Panel, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        box.Margin = new Padding(0, 0, 5, 2);
        row.Controls.Add(box, 0, 0);
        var button = new MacButton { Text = "•••", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 2) };
        button.Click += (_, _) => browse();
        row.Controls.Add(button, 1, 0);
        field.Controls.Add(row, 0, 1);
        return field;
    }

    private static Label SectionLabel(string text)
    {
        var label = NewLabel(text, UiPalette.Muted, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        return label;
    }

    private static TextBox NewTextBox(bool multiline = false, bool readOnly = false) => new()
    {
        Dock = DockStyle.Fill,
        Multiline = multiline,
        ReadOnly = readOnly,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
        AcceptsReturn = multiline && !readOnly,
        WordWrap = multiline,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = UiPalette.Input,
        ForeColor = UiPalette.Text,
        Font = UiFonts.Sans(8.5f)
    };

    private static MacComboBox NewComboBox() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = UiPalette.Input,
        ForeColor = UiPalette.Text,
        Font = UiFonts.Sans(8.5f)
    };

    private static CheckBox NewCheckBox(string text, bool value) => new()
    {
        Text = text,
        Checked = value,
        AutoSize = false,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        ForeColor = UiPalette.Text,
        BackColor = Color.Transparent,
        CheckAlign = ContentAlignment.MiddleLeft,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(5, 0, 0, 0),
        Font = UiFonts.Sans(8f)
    };

    private static Label NewLabel(string text, Color color, bool bold = false) => new()
    {
        Text = text,
        ForeColor = color,
        BackColor = Color.Transparent,
        AutoSize = false,
        // Inter stays crisp at compact desktop-control sizes; the uppercase
        // micro-labels stay at 7.5pt to keep fitting inside the colour cards.
        Font = UiFonts.Sans(bold ? 7.5f : 8.5f, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    private static void AddButton(FlowLayoutPanel panel, string text, Action action, bool accent = false, int width = 100, bool light = false)
    {
        var button = new MacButton
        {
            Text = text,
            Size = new Size(width, 30),
            Margin = new Padding(0, 0, 6, 0),
            BackColor = accent ? UiPalette.Accent : light ? UiPalette.LightAction : UiPalette.PanelRaised,
            ForeColor = accent ? UiPalette.OnAccent : light ? UiPalette.LightActionText : UiPalette.Text,
            HoverColor = accent ? UiPalette.AccentHover : light ? UiPalette.LightActionHover : UiPalette.PanelHover
        };
        button.Click += (_, _) => action();
        panel.Controls.Add(button);
    }

    private string SafeName()
    {
        var value = string.Concat(themeName.Text.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        return string.IsNullOrWhiteSpace(value) ? "Custom" : value;
    }

    private IReadOnlyList<TextReplacement> ReadTextReplacements()
    {
        var result = new List<TextReplacement>();
        foreach (var raw in textReplacements.Lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var split = line.Split("=>", 2, StringSplitOptions.TrimEntries);
            if (split.Length != 2 || split[0].Length == 0) throw new FormatException($"Invalid UI text replacement line: {raw}");
            result.Add(new TextReplacement(split[0], split[1]));
        }
        return result;
    }

    private void LoadInstalledFonts()
    {
        fontChoice.Items.Clear();
        fontChoice.Items.Add(AdobeDefaultFont);
        fontChoice.Items.AddRange(InstalledFontCatalog.FindCompatibleFamilies());
        var preferred = fontChoice.Items.FindIndex(name =>
            name.Equals("SF Pro Display", StringComparison.OrdinalIgnoreCase));
        fontChoice.SelectedIndex = preferred >= 0 ? preferred : 0;
        Log($"Detected {fontChoice.Items.Count - 1} installed font families that fit DVAUI's fixed-width font table safely.");
    }

    private string? ReadFontFamily()
    {
        if (fontChoice.SelectedIndex <= 0) return null;
        var name = fontChoice.Text.Trim();
        InstalledFontCatalog.ValidatePatchName(name);
        return name;
    }

    private void RefreshPanelDiscovery()
    {
        Try(() =>
        {
            var discovery = PanelThemeManager.Discover(target.Text.Trim());
            var debugMode = CepDeveloperModeManager.Inspect(target.Text.Trim());
            var debugLabel = debugMode.RuntimeMajor is null
                ? "CEP RUNTIME UNKNOWN"
                : $"CEP {debugMode.RuntimeMajor} DEBUG {(debugMode.IsEnabled ? "ON" : "AUTO-ENABLE")}";
            panelStatus.Text = $"{discovery.CepExtensions.Count} CEP  ·  {discovery.SignedExtensionCount} SIGNED  ·  {debugLabel}  ·  {discovery.ScriptUiPanels.Count} SCRIPTUI";
            var lines = new List<string>
            {
                "CEP HTML/CSS · every detected panel is themed from a verified original backup",
                "Signed bundles use Adobe CEP developer mode; Restore returns their files and the prior registry value.",
                debugMode.Description
            };
            lines.AddRange(discovery.CepExtensions.Select(extension =>
                $"{(extension.IsSigned ? "SIGNED" : "THEME")}  {extension.Name}  ·  {extension.ThemeFiles.Count} HTML/CSS  ·  {extension.RootPath}"));
            if (discovery.ScriptUiPanels.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("SCRIPTUI · native JSX follows DVAUI/its own drawing; CSS is not applicable");
                lines.AddRange(discovery.ScriptUiPanels.Select(panel =>
                    $"NATIVE  {panel.Name}{(panel.IsCompiled ? " · compiled" : string.Empty)}  ·  {panel.Path}"));
            }
            if (discovery.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.AddRange(discovery.Warnings.Select(warning => "WARN  " + warning));
            }
            panelDetails.Text = string.Join("\r\n", lines);
            Log($"Panel scan · {discovery.CepExtensions.Count} CEP extensions / {discovery.ThemeFileCount} HTML+CSS files / {discovery.ScriptUiPanels.Count} ScriptUI panels.");
        });
    }

    private void ApplyPanelsOnly()
    {
        Try(() =>
        {
            if (Process.GetProcessesByName("AfterFX").Length > 0)
                throw new InvalidOperationException("Close After Effects before modifying CEP panel files.");
            var targetPath = target.Text.Trim();
            if (!File.Exists(targetPath)) throw new InvalidOperationException("Select the installed After Effects dvaui.dll first.");
            PanelThemeManager.SaveConfiguration(PanelThemeFile, ReadSettings(), themeName.Text, ReadFontFamily());
            if (File.Exists(PanelReportFile)) File.Delete(PanelReportFile);
            var arguments = $"--apply-panels {Quote(targetPath)} {Quote(PanelBackups)} {Quote(PanelThemeFile)} {Quote(PanelReportFile)}";
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = arguments
            };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start the elevated panel modifier.");
            process.WaitForExit();
            if (!File.Exists(PanelReportFile)) throw new InvalidOperationException("The panel modifier did not return a report.");
            var report = JsonSerializer.Deserialize<PanelOperationReport>(File.ReadAllText(PanelReportFile));
            LogPanelReport(report);
            if (process.ExitCode == 8)
                throw new InvalidOperationException("Panel theming completed with conflicts; affected files were left unchanged. See the activity log.");
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Panel theming failed (exit code {process.ExitCode}). See the activity log.");
            RefreshPanelDiscovery();
        });
    }

    private void LogPanelReport(PanelOperationReport? report)
    {
        if (report is null)
        {
            Log("Panel operation returned an unreadable report.");
            return;
        }
        Log($"CEP panel {report.Operation.ToLowerInvariant()} · {report.FilesPatched} patched / {report.FilesRestored} restored / " +
            $"{report.ColorReplacements} palette values / {report.HtmlOverridesInjected} HTML overrides / " +
            $"{report.SignedExtensionsPatched} signed bundles themed / {report.SignedExtensionsSkipped} signed skipped / " +
            $"CEP debug {report.CepDebugModeChanges} enabled, {report.CepDebugModeRestored} restored / {report.Conflicts} conflicts.");
        foreach (var warning in report.Warnings.Take(8)) Log("Panel warning · " + warning);
        if (report.Warnings.Count > 8) Log($"Panel warning · {report.Warnings.Count - 8} additional warnings are recorded in {PanelReportFile}");
    }

    private void UpdatePreview() => preview.Invalidate();
    private Color ReadColor(string name) { try { return ColorTranslator.FromHtml(colorBoxes[name].Text.Trim()); } catch { throw new FormatException($"{name} must be a hex color like #FCEE0A."); } }
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private string EnsureOriginalSnapshot()
    {
        var targetPath = target.Text.Trim();
        if (targetPath.Length == 0) throw new InvalidOperationException("Select the installed After Effects dvaui.dll first.");
        var original = OriginalDllStore.CaptureIfMissing(targetPath, Originals, out var captured);
        source.Text = original;
        SaveLastTarget(targetPath);
        if (captured) Log($"Saved immutable original before customization: {original}");
        return original;
    }

    private void PickTargetDll()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "DVAUI DLL (dvaui.dll)|dvaui.dll|DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            FileName = target.Text
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        target.Text = dialog.FileName;
        Try(() =>
        {
            var original = EnsureOriginalSnapshot();
            Log($"Target selected · Preserved source: {original}");
            RefreshPanelDiscovery();
        });
    }

    private void SaveLastTarget(string path)
    {
        Directory.CreateDirectory(DataRoot);
        File.WriteAllText(LastTargetFile, Path.GetFullPath(path.Trim()));
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void PickColor(string name, Control anchor)
    {
        activeColorPicker?.Close();
        var picker = new VerbatimColorPickerForm(ReadColor(name));
        activeColorPicker = picker;
        picker.ColorChanged += color =>
        {
            if (IsDisposed || !colorBoxes.TryGetValue(name, out var box)) return;
            box.Text = Hex(color);
            preset.SelectedIndex = BuiltInPresets.Length;
            UpdatePreview();
        };
        picker.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(activeColorPicker, picker)) activeColorPicker = null;
            picker.Dispose();
        };
        picker.ShowNear(this, anchor);
    }
    private void Try(Action action) { try { action(); } catch (Exception ex) { Log("ERROR · " + ex.Message); MessageBox.Show(this, ex.Message, "AfterThemed by Drerachi", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    private void Log(string text) => log.AppendText($"[{DateTime.Now:HH:mm:ss}]  {text}\r\n");

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (WindowState == FormWindowState.Maximized) RestoreUnderCursor();
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    private void Minimize() => WindowState = FormWindowState.Minimized;

    private void ToggleMaximize()
    {
        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
            return;
        }
        // A borderless form maximizes over the taskbar unless it is clamped to the working area.
        MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
        WindowState = FormWindowState.Maximized;
    }

    private void RestoreUnderCursor()
    {
        var cursor = MousePosition;
        var ratio = Width > 0 ? (cursor.X - Left) / (double)Width : 0.5;
        WindowState = FormWindowState.Normal;
        Location = new Point(cursor.X - (int)(Width * ratio), Math.Max(0, cursor.Y - 18));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            var preference = 2;
            DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x84;
        const int grip = 7;
        if (m.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if ((int)m.Result != 1) return;
            var x = unchecked((short)(long)m.LParam);
            var y = unchecked((short)((long)m.LParam >> 16));
            var point = PointToClient(new Point(x, y));
            var left = point.X <= grip;
            var right = point.X >= ClientSize.Width - grip;
            var top = point.Y <= grip;
            var bottom = point.Y >= ClientSize.Height - grip;
            m.Result = (IntPtr)(top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1);
            return;
        }
        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

internal static class GraphicsExtensions
{
    internal static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedPanel.RoundRect(bounds, radius);
        graphics.FillPath(brush, path);
    }
}
