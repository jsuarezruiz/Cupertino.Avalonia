using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Cupertino.Controls;
using Cupertino.Gallery.Pages;

namespace Cupertino.Gallery;

public sealed class CatalogEntry
{
    public CatalogEntry(string title, string glyph, string tile, Func<Control> build,
                        string category = "Controls")
    {
        Title = title;
        Glyph = glyph;
        TileBrush = new SolidColorBrush(Color.Parse(tile));
        Build = build;
        Category = category;
    }

    public string Title { get; }
    public string Glyph { get; }
    public IBrush TileBrush { get; }
    public Func<Control> Build { get; }
    public string Category { get; }

    public override string ToString() => Title;
}

public partial class ShellView : UserControl
{
    // Avoid running shaders on hidden pages.
    private readonly IReadOnlyList<CatalogEntry> _entries =
    [
        new("Button", "●", "#FF007AFF", () => new ButtonPage()),
        new("RepeatButton", "⟳", "#FF5856D6", () => new RepeatButtonPage()),
        new("ToggleButton", "◑", "#FF5E5CE6", () => new ToggleButtonPage()),
        new("SplitButton", "◨", "#FF6C6BEA", () => new SplitButtonPage()),
        new("DropDownButton", "⌄", "#FF7D7AEE", () => new DropDownButtonPage()),
        new("HyperlinkButton", "↗", "#FF8E8CF2", () => new HyperlinkButtonPage()),
        new("ToggleSwitch", "◍", "#FF34C759", () => new ToggleSwitchPage()),
        new("Slider", "◎", "#FFFF9500", () => new SliderPage()),
        new("TextBox", "▭", "#FF32ADE6", () => new TextBoxPage()),
        new("MaskedTextBox", "⌗", "#FF40C8E0", () => new MaskedTextBoxPage()),
        new("AutoCompleteBox", "⌕", "#FF00C7BE", () => new AutoCompleteBoxPage()),
        new("CheckBox", "✓", "#FF30B0C7", () => new CheckBoxPage()),
        new("RadioButton", "⊙", "#FF3FB6CB", () => new RadioButtonPage()),
        new("ColorPicker", "◕", "#FF9A6FDE", () => new ColorPickerPage()),
        new("ComboBox", "▾", "#FFAF52DE", () => new ComboBoxPage()),
        new("Menu", "☰", "#FFA2845E", () => new MenuPage()),
        new("Flyout", "❐", "#FFC77DFF", () => new FlyoutPage(), "Presentation"),
        new("Calendar", "▦", "#FFFF3B30", () => new CalendarPage()),
        new("CalendarDatePicker", "⊞", "#FFFF6482", () => new CalendarDatePickerPage()),
        new("DatePicker", "▣", "#FFFF2D55", () => new DatePickerPage(), "Cupertino"),
        new("TimePicker", "◴", "#FFFF5470", () => new TimePickerPage(), "Cupertino"),
        new("Expander", "›", "#FF8E8E93", () => new ExpanderPage()),
        new("NumericUpDown", "±", "#FF64D2FF", () => new NumericUpDownPage()),
        new("ContextMenu", "⋯", "#FFD08770", () => new ContextMenuPage()),
        new("ProgressBar", "▰", "#FF0A84FF", () => new ProgressBarPage()),
        new("RefreshContainer", "↻", "#FF32D74B", () => new RefreshContainerPage()),
        new("SwipeView", "⇆", "#FFFF3B30", () => new SwipeViewPage(), "Cupertino"),
        new("Sheet", "▁", "#FF5E5CE6", () => new SheetPage(), "Cupertino"),
        new("Toolbar", "▬", "#FF30B0C7", () => new ToolbarPage(), "Cupertino"),
        new("Badge", "❶", "#FFE0483E", () => new BadgePage(), "Cupertino"),
        new("List & Search", "⌕", "#FF007AFF", () => new ListAndSearchPage(), "Cupertino"),
        new("TabControl", "⊡", "#FFFF9F0A", () => new TabControlPage()),
        new("TabStrip", "▥", "#FFE59500", () => new TabStripPage()),
        new("ListBox", "▤", "#FF5AC8FA", () => new ListBoxPage()),
        new("TreeView", "⋔", "#FF32D74B", () => new TreeViewPage()),
        new("SplitView", "◫", "#FF66D4CF", () => new SplitViewPage()),
        new("GridSplitter", "⇹", "#FF98989D", () => new GridSplitterPage()),
        new("Carousel", "⧉", "#FFFFD60A", () => new Cupertino.Gallery.Pages.CarouselPage()),
        new("TransitioningContentControl", "⇄", "#FFFFC300", () => new TransitioningContentPage()),
        new("ToolTip & Label", "ⓘ", "#FFAC8E68", () => new ToolTipAndLabelPage()),
        new("Notifications", "◉", "#FFFF6961", () => new NotificationsPage(), "Presentation"),
        new("NavigationBar", "⊤", "#FFBF5AF2", () => new CupertinoNavigationBarPage(), "Cupertino"),
        new("Dialog", "⚠", "#FFFF453A", () => new DialogPage(), "Presentation"),
        new("Photos", "▨", "#FFFFB340", () => new PhotosShowcasePage(), "Showcases"),
        new("Lock Screen", "◐", "#FF5E5CE6", () => new LockScreenShowcasePage(), "Showcases"),
        new("Typography", "Aa", "#FF1C1C1E", () => new TypographyPage(), "Styles"),
        new("Icons", "✦", "#FF8E8E93", () => new IconsPage(), "Styles"),
        new("Colors", "◧", "#FFFF375F", () => new ColorsPage(), "Styles"),
        new("Liquid Glass", "◇", "#FF6E6E73", () => new LiquidGlassPage(), "Styles"),
    ];

    private Avalonia.Controls.Platform.IInsetsManager? _insets;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is not { } top || top.InsetsManager is not { } insets)
            return;
        _insets = insets;
        insets.DisplayEdgeToEdgePreference = true;
        TopLevel.SetAutoSafeAreaPadding(top, false);
        Padding = insets.SafeAreaPadding;
        insets.SafeAreaChanged += OnSafeAreaChanged;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_insets is not null)
            _insets.SafeAreaChanged -= OnSafeAreaChanged;
        _insets = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSafeAreaChanged(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e) =>
        Padding = e.SafeAreaPadding;

    public ShellView()
    {
        InitializeComponent();

        var nav = this.FindControl<CupertinoNavigationPage>("Nav")!;

        var entries = _entries;
        if (NativeComparison.IsAvailable)
            entries = [.. _entries, new("Side by Side", "⇋", "#FF34AADC", () => new SideBySidePage(), "Showcases")];

        var root = new RootPage(entries);
        root.EntryChosen += (_, entry) => nav.Push(entry.Title, entry.Build());
        root.SettingsChosen += (_, _) => nav.Push("Settings", new SettingsPage());

        var args = Environment.GetCommandLineArgs();
        var pageArg = Array.IndexOf(args, "--page");
        var requested = pageArg >= 0 && pageArg + 1 < args.Length
            ? args[pageArg + 1]
            : Environment.GetEnvironmentVariable("GALLERY_PAGE");
        if (requested is not null)
        {
            var wanted = entries.FirstOrDefault(
                e => string.Equals(e.Title, requested, StringComparison.OrdinalIgnoreCase));
            if (wanted is not null)
            {
                // Wait for the navigation host.
                void OpenOnce(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
                {
                    nav.Loaded -= OpenOnce;
                    var content = wanted.Build();
                    GalleryCaptureState.Attach(content);
                    nav.Push(wanted.Title, content);
                }
                nav.Loaded += OpenOnce;
            }
        }

        var source = new Button
        {
            Classes = { "plain" },
            Padding = new Avalonia.Thickness(8, 7),
            IsVisible = false,
            Content = MakeBarIcon("chevron.left.forwardslash.chevron.right"),
        };
        source.Click += (_, _) =>
        {
            if (SourceResourceFor(nav.CurrentContent) is not { } resource)
                return;
            using var stream = typeof(ShellView).Assembly.GetManifestResourceStream(resource)!;
            using var reader = new System.IO.StreamReader(stream);
            nav.Push("Source", new Pages.SourcePage(reader.ReadToEnd()));
        };
        nav.Navigated += (_, _) =>
            source.IsVisible = SourceResourceFor(nav.CurrentContent) is not null;

        nav.RootContent = root;

        var gear = new Button
        {
            Classes = { "plain" },
            Padding = new Avalonia.Thickness(8, 7),
            Content = MakeBarIcon("gear"),
        };
        gear.Click += (_, _) => nav.Push("Settings", new SettingsPage());
        ToolTip.SetTip(source, "View source");
        ToolTip.SetTip(gear, "Settings");
        Avalonia.Automation.AutomationProperties.SetName(source, "View source");
        Avalonia.Automation.AutomationProperties.SetName(gear, "Settings");

        var trailingCapsule = new Cupertino.Controls.GlassSurface
        {
            Height = 36,
            CornerRadius = new Avalonia.CornerRadius(18),
            BlurRadius = 18,
            GlassThickness = 0,
            Saturation = 1,
            RefractionStrength = 0,
            ChromaticAberration = 0,
            DepthEffect = 0,
            LightIntensity = 0,
            FresnelStrength = 0,
            Magnification = 1,
            ShadowOpacity = 0.10,
            ShadowBlur = 12,
            ShadowOffset = 2,
            ShadowContactWeight = 0.2,
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 2,
                Margin = new Avalonia.Thickness(8, 0),
                Children = { source, gear },
            },
        };
        trailingCapsule.Bind(Cupertino.Controls.GlassSurface.TintProperty,
            this.GetResourceObservable("CupertinoBarButtonTint"));
        nav.TrailingContent = trailingCapsule;
    }

    private static string? SourceResourceFor(Control? page)
    {
        if (page is null)
            return null;
        return typeof(ShellView).Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".{page.GetType().Name}.axaml", StringComparison.Ordinal));
    }

    private static CupertinoIcon MakeBarIcon(string glyph)
    {
        var icon = new CupertinoIcon { Glyph = glyph, Size = 22 };
        icon.Bind(CupertinoIcon.ForegroundProperty,
                  icon.GetResourceObservable("CupertinoLabelBrush"));
        return icon;
    }
}
