using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
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
    public const double WideLayoutBreakpoint = 760;
    private const double WideDetailLargeTitleInset = 39;

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
    private readonly CupertinoNavigationPage _compactNav;
    private readonly CupertinoNavigationPage _wideCatalogNav;
    private readonly CupertinoNavigationPage _wideDetailNav;
    private readonly Grid _wideLayout;
    private CatalogEntry? _currentEntry;
    private bool _showingSettings;
    private bool _isWide;

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

        _compactNav = this.FindControl<CupertinoNavigationPage>("Nav")!;
        _wideCatalogNav = this.FindControl<CupertinoNavigationPage>("WideCatalogNav")!;
        _wideDetailNav = this.FindControl<CupertinoNavigationPage>("WideDetailNav")!;
        _wideLayout = this.FindControl<Grid>("WideLayout")!;

        var entries = _entries;
        if (NativeComparison.IsAvailable)
            entries = [.. _entries, new("Side by Side", "⇋", "#FF34AADC", () => new SideBySidePage(), "Showcases")];
        if (NativeComparison.IsAvailable
            && Environment.GetEnvironmentVariable("GALLERY_MOTION_CAPTURE") == "1")
            entries = [.. entries, new("Motion Verification", "◫", "#FF8E8E93", () => new MotionVerificationPage(), "Showcases")];

        var compactRoot = new RootPage(entries);
        compactRoot.EntryChosen += (_, entry) => OpenEntry(entry);
        compactRoot.SettingsChosen += (_, _) => OpenSettings();
        _compactNav.RootContent = compactRoot;

        var wideRoot = new RootPage(entries, showHero: false);
        wideRoot.EntryChosen += (_, entry) => OpenEntry(entry);
        wideRoot.SettingsChosen += (_, _) => OpenSettings();
        _wideCatalogNav.RootContent = wideRoot;
        _wideDetailNav.RootContent = new HomePage();

        _compactNav.TrailingContent = MakeActions(_compactNav, showSource: true, showSettings: true);
        _wideCatalogNav.TrailingContent = MakeActions(_wideCatalogNav, showSource: false, showSettings: true);
        _wideDetailNav.TrailingContent = MakeActions(_wideDetailNav, showSource: true, showSettings: false);
        _compactNav.NavigationCompleted += (_, _) => UpdateCompactSelection();

        var args = Environment.GetCommandLineArgs();
        var pageArg = Array.IndexOf(args, "--page");
        var requested = pageArg >= 0 && pageArg + 1 < args.Length
            ? args[pageArg + 1]
            : Environment.GetEnvironmentVariable("GALLERY_PAGE");
        if (requested is not null)
        {
            var scrollTo = RequestedScroll(args);
            var wanted = entries.FirstOrDefault(
                e => string.Equals(e.Title, requested, StringComparison.OrdinalIgnoreCase));
            if (wanted is not null)
            {
                // Wait until the adaptive host has selected its initial layout.
                void OpenOnce(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
                {
                    Loaded -= OpenOnce;
                    OpenEntry(wanted, attachCaptureState: true, scrollTo: scrollTo);
                }
                Loaded += OpenOnce;
            }
        }
    }

    protected override Avalonia.Size ArrangeOverride(Avalonia.Size finalSize)
    {
        UpdateAdaptiveLayout(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    private static double RequestedScroll(string[] args)
    {
        var index = Array.IndexOf(args, "--scroll");
        var text = index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : Environment.GetEnvironmentVariable("GALLERY_SCROLL");
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
    }

    private void OpenEntry(CatalogEntry entry, bool attachCaptureState = false, double scrollTo = 0)
    {
        _currentEntry = entry;
        _showingSettings = false;
        var content = entry.Build();
        if (attachCaptureState)
            GalleryCaptureState.Attach(content);

        if (_isWide)
            ShowWideDetail(entry.Title, content);
        else
            _compactNav.TryPush("catalog", entry.Title, content, entry);

        if (scrollTo != 0)
            ScrollAfterLayout(content, scrollTo);
    }

    // Negative scrolls to the end; reapplied while the page extent settles.
    private static void ScrollAfterLayout(Control content, double scrollTo)
    {
        var attempts = 0;

        void Apply()
        {
            var scroll = content.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scroll is not null)
            {
                var bottom = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                var target = scrollTo < 0 ? bottom : Math.Min(scrollTo, bottom);
                if (scroll.Offset.Y != target)
                    scroll.Offset = new Avalonia.Vector(0, target);
            }

            if (++attempts < 12)
                Avalonia.Threading.DispatcherTimer.RunOnce(
                    Apply, TimeSpan.FromMilliseconds(100), Avalonia.Threading.DispatcherPriority.Loaded);
        }

        Avalonia.Threading.DispatcherTimer.RunOnce(
            Apply, TimeSpan.FromMilliseconds(400), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OpenSettings()
    {
        _showingSettings = true;
        if (_isWide)
        {
            _currentEntry = null;
            ShowWideDetail("Settings", new SettingsPage());
        }
        else
        {
            _compactNav.TryPush("settings", "Settings", new SettingsPage());
        }
    }

    private void ShowWideDetail(string title, Control content, bool contentIncludesLargeTitleInset = false)
    {
        if (_wideDetailNav.Depth > 0)
            _wideDetailNav.RestoreState(new CupertinoNavigationState([]), _ => null);
        if (!contentIncludesLargeTitleInset)
        {
            var margin = content.Margin;
            content.Margin = new Avalonia.Thickness(
                margin.Left, margin.Top + WideDetailLargeTitleInset,
                margin.Right, margin.Bottom);
        }
        _wideDetailNav.RootTitle = title;
        _wideDetailNav.RootContent = content;
    }

    private void UpdateAdaptiveLayout(double width)
    {
        var useWideLayout = width >= WideLayoutBreakpoint;
        if (_isWide == useWideLayout)
            return;

        _isWide = useWideLayout;
        _compactNav.IsVisible = !useWideLayout;
        _wideLayout.IsVisible = useWideLayout;

        if (useWideLayout)
        {
            if (_showingSettings)
                ShowWideDetail("Settings", new SettingsPage());
            else if (_currentEntry is { } entry)
                ShowWideDetail(entry.Title, entry.Build());
            else
                ShowWideDetail("Welcome", new HomePage(), contentIncludesLargeTitleInset: true);
            return;
        }

        var state = _showingSettings
            ? new CupertinoNavigationState([new("settings", "Settings", null)])
            : _currentEntry is { } selected
                ? new CupertinoNavigationState([new("catalog", selected.Title, selected)])
                : new CupertinoNavigationState([]);
        _compactNav.RestoreState(state, saved => saved.Route switch
        {
            "settings" => new SettingsPage(),
            "catalog" when saved.Parameter is CatalogEntry entry => entry.Build(),
            _ => null,
        });
    }

    private void UpdateCompactSelection()
    {
        if (_isWide)
            return;
        if (_compactNav.CurrentContent is SettingsPage)
        {
            _showingSettings = true;
            return;
        }
        if (_compactNav.CurrentContent is SourcePage)
            return;
        if (_compactNav.CurrentEntry?.Parameter is CatalogEntry entry)
        {
            _currentEntry = entry;
            _showingSettings = false;
            return;
        }
        if (_compactNav.Depth == 0)
        {
            _currentEntry = null;
            _showingSettings = false;
        }
    }

    private GlassSurface MakeActions(
        CupertinoNavigationPage nav, bool showSource, bool showSettings)
    {
        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 2,
            Margin = new Avalonia.Thickness(8, 0),
        };
        Button? source = null;
        if (showSource)
        {
            source = new Button
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
                nav.TryPush("source", "Source", new SourcePage(reader.ReadToEnd()));
            };
            ToolTip.SetTip(source, "View source");
            Avalonia.Automation.AutomationProperties.SetName(source, "View source");
            actions.Children.Add(source);
        }

        if (showSettings)
        {
            var gear = new Button
            {
                Classes = { "plain" },
                Padding = new Avalonia.Thickness(8, 7),
                Content = MakeBarIcon("gear"),
            };
            gear.Click += (_, _) => OpenSettings();
            ToolTip.SetTip(gear, "Settings");
            Avalonia.Automation.AutomationProperties.SetName(gear, "Settings");
            actions.Children.Add(gear);
        }

        var capsule = new GlassSurface
        {
            Height = 44,
            CornerRadius = new Avalonia.CornerRadius(22),
            Child = actions,
        };
        capsule.Bind(Avalonia.StyledElement.ThemeProperty,
            capsule.GetResourceObservable("CupertinoBarCapsule"));

        if (source is not null)
        {
            void UpdateSourceAction()
            {
                source.IsVisible = SourceResourceFor(nav.CurrentContent) is not null;
                if (!showSettings)
                    capsule.IsVisible = source.IsVisible;
            }
            nav.Navigated += (_, _) => UpdateSourceAction();
            UpdateSourceAction();
        }

        return capsule;
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
                  icon.GetResourceObservable("CupertinoBarForegroundBrush"));
        return icon;
    }
}
