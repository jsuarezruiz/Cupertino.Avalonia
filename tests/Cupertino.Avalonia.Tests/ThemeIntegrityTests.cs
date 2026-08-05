using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Cupertino.Themes;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class ThemeIntegrityTests
{
    private static ResourceDictionary ThemeDictionary(ThemeVariant variant)
    {
        var theme = new CupertinoTheme();
        var resources = (ResourceDictionary)theme.Resources;
        Assert.True(resources.ThemeDictionaries.TryGetValue(variant, out var dict),
            $"CupertinoTheme has no '{variant}' theme dictionary.");
        return (ResourceDictionary)dict!;
    }

    [AvaloniaFact]
    public void Light_and_dark_declare_the_same_resource_keys()
    {
        var light = ThemeDictionary(ThemeVariant.Light);
        var dark = ThemeDictionary(ThemeVariant.Dark);

        var onlyInLight = light.Keys.Except(dark.Keys).Select(k => k.ToString()).Order().ToArray();
        var onlyInDark = dark.Keys.Except(light.Keys).Select(k => k.ToString()).Order().ToArray();

        Assert.True(onlyInLight.Length == 0, "Missing from Dark: " + string.Join(", ", onlyInLight));
        Assert.True(onlyInDark.Length == 0, "Missing from Light: " + string.Join(", ", onlyInDark));
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Every_declared_brush_resolves(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = NewWindow(variant);

        foreach (var key in ThemeDictionary(variant).Keys)
        {
            Assert.True(window.TryFindResource(key, variant, out var value),
                $"Resource '{key}' does not resolve in {variantName}.");
            Assert.NotNull(value);
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Search_field_is_distinguishable_from_the_card_behind_it(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = NewWindow(variant);

        window.TryFindResource("CupertinoSearchFieldBrush", variant, out var fieldRes);
        window.TryFindResource("CupertinoCardBrush", variant, out var cardRes);
        var field = Assert.IsType<SolidColorBrush>(fieldRes);
        var card = Assert.IsType<SolidColorBrush>(cardRes);

        var composited = Composite(field.Color, card.Color);
        var delta = Math.Abs(Luminance(composited) - Luminance(card.Color));
        var minimumDelta = variant == ThemeVariant.Light ? 0.005 : 0.015;
        Assert.True(delta > minimumDelta,
            $"{variantName}: search field is invisible on the card (delta {delta:F4}).");
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Themed_controls_template_and_measure(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = NewWindow(variant);

        var controls = new Control[]
        {
            new Button { Content = "Button" },
            new Button { Content = "Prominent", Classes = { "prominent" } },
            new Button { Content = "Bordered", Classes = { "bordered" } },
            new ToggleSwitch { IsChecked = true },
            new Slider { Value = 40 },
            new TextBox { PlaceholderText = "Field" },
            new TextBox { Classes = { "search" }, Text = "q" },
            new ListBox { ItemsSource = new[] { "a", "b" }, Classes = { "inset" } },
            new TabControl
            {
                Classes = { "segmented" },
                ItemsSource = new[] { new TabItem { Header = "One" }, new TabItem { Header = "Two" } },
            },
            new TabStrip { ItemsSource = new[] { new TabStripItem { Content = "One" } } },
            new Section { Header = "HEADER", Content = new TextBlock { Text = "body" }, Footer = "footer" },
            new GlassSurface { Width = 120, Height = 40 },
            new RefreshContainer { Content = new TextBlock { Text = "pull" } },
            new CupertinoSwipeView
            {
                Content = new TextBlock { Text = "row" },
                TrailingActions = new Button { Content = "Delete", Classes = { "swipe" } },
            },
            new CupertinoBadge { Value = 3 },
            new CupertinoListCell { Title = "Cell", AccessoryKind = CupertinoListAccessory.Disclosure },
            new CupertinoFormRow { Label = "Name", Content = new TextBox() },
            new CupertinoPageControl { NumberOfPages = 3 },
            new CupertinoSearchController { ItemsSource = new[] { "a", "b" } },
            new CupertinoDateTimePicker { SelectedDateTime = DateTimeOffset.Now },
            new CupertinoSheetPresenter { Content = new TextBlock { Text = "sheet" }, Width = 200, Height = 120 },
            new CupertinoToolbar { Width = 300 },
            new ColorView(),
            new ColorPicker(),
            new ColorSlider(),
        };

        var panel = new StackPanel();
        foreach (var c in controls)
            panel.Children.Add(c);
        window.Content = panel;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        foreach (var c in controls)
        {
            if (c is TemplatedControl templated)
                Assert.True(templated.IsAttachedToVisualTree(),
                    $"{c.GetType().Name} never attached in {variantName}.");
            Assert.True(c.Bounds.Width > 0 && c.Bounds.Height > 0,
                $"{c.GetType().Name} laid out empty in {variantName} ({c.Bounds}).");
        }
    }

    [AvaloniaFact]
    public void Search_field_uses_custom_caret_and_reserves_clear_target()
    {
        var window = NewWindow(ThemeVariant.Light);
        var search = new TextBox { Classes = { "search" }, Width = 120, Height = 47 };
        window.Content = search;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var presenter = search.GetVisualDescendants().OfType<TextPresenter>().Single();
        var caret = search.GetVisualDescendants().OfType<CupertinoSearchCaret>().Single();
        var shadow = Assert.IsType<CupertinoSearchFieldShadow>(AdornerLayer.GetAdorner(search));
        var clear = search.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ClearButton");
        var scroll = search.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_ScrollViewer");
        var watermark = search.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Watermark");

        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.CaretBrush).Color);
        Assert.Equal(Color.Parse("#426BF2"), Assert.IsAssignableFrom<ISolidColorBrush>(caret.Brush).Color);
        Assert.Equal(2, caret.CaretWidth);
        Assert.Equal(23, caret.CaretHeight);
        Assert.True(caret.Bounds.Width > 0 && caret.Bounds.Height > 0,
            $"search caret overlay laid out empty ({caret.Bounds})");
        Assert.False(search.ClipToBounds);
        Assert.Equal(new Rect(0, 0, 120, 47), shadow.Bounds);
        Assert.Equal(default, shadow.Margin);
        Assert.False(AdornerLayer.GetIsClipEnabled(shadow));
        Assert.Equal(30, clear.Bounds.Width);
        Assert.Equal(47, clear.Bounds.Height);
        Assert.False(clear.IsHitTestVisible);
        Assert.Equal(new Thickness(0), scroll.Margin);
        Assert.InRange(scroll.Bounds.Width, 63.9, 64.1);
        Assert.True(watermark.Bounds.Width > 50, $"search prompt clipped to {watermark.Bounds.Width}");

        search.Text = "q";
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(clear.IsHitTestVisible);
        Assert.Equal(new Thickness(0, 0, 24, 0), scroll.Margin);
    }

    [AvaloniaFact]
    public void Tab_bar_drag_only_responds_inside_the_bar()
    {
        var window = NewWindow(ThemeVariant.Light);
        var tabs = new TabControl { Classes = { "bottom" } };
        tabs.Items.Add(new TabItem { Header = "A", Content = new Border { Height = 400 } });
        tabs.Items.Add(new TabItem { Header = "B", Content = new Border { Height = 400 } });
        window.Content = tabs;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var indicator = tabs.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c.Name == "PART_Indicator");

        Assert.True(indicator is not null, "PART_Indicator missing from the bottom tab bar template.");
        Assert.False(indicator!.IsVisible,
            "The travelling indicator must stay hidden until a drag starts on the bar.");
    }

    [AvaloniaFact]
    public void A_bottom_tab_strip_carries_the_drag_indicator_too()
    {
        var window = NewWindow(ThemeVariant.Light);
        var strip = new TabStrip { Classes = { "bottom" } };
        strip.Items.Add(new TabStripItem { Content = "A" });
        strip.Items.Add(new TabStripItem { Content = "B" });
        window.Content = strip;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var indicator = strip.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c.Name == "PART_Indicator");

        Assert.True(indicator is not null, "PART_Indicator missing from the bottom tab strip template.");
        Assert.False(indicator!.IsVisible,
            "The travelling indicator must stay hidden until a drag starts on the strip.");
        Assert.True(Cupertino.Controls.TabBarInteraction.GetIsEnabled(strip),
            "The drag behaviour must be enabled on the bottom strip.");
    }

    [AvaloniaFact]
    public void Disabling_tab_bar_interaction_disposes_an_active_drag()
    {
        var window = NewWindow(ThemeVariant.Light);
        var strip = new TabStrip { Classes = { "segmented" }, Width = 360, SelectedIndex = 0 };
        strip.Items.Add(new TabStripItem { Content = "Years" });
        strip.Items.Add(new TabStripItem { Content = "Months" });
        window.Content = strip;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var indicator = strip.GetVisualDescendants().OfType<Control>()
                             .Single(c => c.Name == "PART_Indicator");
        var selected = strip.GetVisualDescendants().OfType<TabStripItem>().Single(item => item.IsSelected);
        var start = selected.TranslatePoint(
            new Point(selected.Bounds.Width / 2, selected.Bounds.Height / 2), window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + 24, start.Y));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(indicator.IsVisible);

        TabBarInteraction.SetIsEnabled(strip, false);

        Assert.False(indicator.IsVisible);
        Assert.DoesNotContain(strip.GetVisualDescendants().OfType<TabStripItem>(),
            item => item.Classes.Contains("cupertino-nopill"));
        window.MouseUp(new Point(start.X + 24, start.Y), MouseButton.Left);
    }

    [AvaloniaFact]
    public void Disabling_refresh_interaction_releases_its_transforms()
    {
        var window = NewWindow(ThemeVariant.Light);
        var refresh = new RefreshContainer { Content = new TextBlock { Text = "Pull" } };
        window.Content = refresh;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var presenter = refresh.GetVisualDescendants().OfType<Panel>()
                               .Single(panel => panel.Name == "PART_RefreshVisualizerPresenter");
        var content = refresh.GetVisualDescendants().OfType<ContentPresenter>()
                             .Single(presenter => presenter.Name == "PART_ContentPresenter");
        Assert.NotNull(presenter.RenderTransform);
        Assert.NotNull(content.RenderTransform);

        RefreshInteraction.SetIsEnabled(refresh, false);

        Assert.Null(presenter.RenderTransform);
        Assert.Null(content.RenderTransform);
    }

    [AvaloniaFact]
    public void A_segmented_drag_cancelled_by_vertical_scroll_clears_its_travel_pill()
    {
        var window = NewWindow(ThemeVariant.Light);
        var strip = new TabStrip
        {
            Classes = { "segmented" },
            Width = 360,
            SelectedIndex = 0,
        };
        strip.Items.Add(new TabStripItem { Content = "Years" });
        strip.Items.Add(new TabStripItem { Content = "Months" });
        strip.Items.Add(new TabStripItem { Content = "Days" });
        window.Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Children =
                {
                    strip,
                    new Border { Height = 1200 },
                },
            },
        };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var indicator = strip.GetVisualDescendants().OfType<Control>()
                             .Single(c => c.Name == "PART_Indicator");
        var lens = strip.GetVisualDescendants().OfType<GlassSurface>()
                        .Single(c => c.Name == "PART_IndicatorLens");
        var track = indicator.GetVisualAncestors().OfType<Border>()
                             .First(b => Math.Abs(b.Bounds.Height - 32) < 0.1);
        var selected = strip.GetVisualDescendants().OfType<TabStripItem>()
                            .Single(i => i.IsSelected);
        var start = selected.TranslatePoint(
            new Point(selected.Bounds.Width / 2, selected.Bounds.Height / 2), window)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + 24, start.Y));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.True(indicator.IsVisible, "the horizontal move should have started a pill drag");
        Assert.True(lens.IsVisible, "a held segmented pill should lift into its glass lens");
        Assert.Equal(9.0, lens.GlassThickness, 3);
        Assert.Equal(13.0, lens.RefractionStrength, 3);
        Assert.Equal(0.42, lens.ChromaticAberration, 3);
        Assert.Equal(1.045, lens.Magnification, 3);

        var trackTop = track.TranslatePoint(default, strip)!.Value.Y;
        var lensTop = lens.TranslatePoint(default, strip)!.Value.Y;
        var topOverflow = trackTop - lensTop;
        var bottomOverflow = lensTop + lens.Bounds.Height - (trackTop + track.Bounds.Height);
        Assert.True(topOverflow > 0, "the held pill should swell beyond the track");
        Assert.InRange(Math.Abs(topOverflow - bottomOverflow), 0, 0.5);

        window.MouseMove(new Point(start.X + 24, start.Y + 14));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(indicator.IsVisible,
            "scroll takeover must not strand the stretched pill between segments");
        Assert.DoesNotContain(strip.GetVisualDescendants().OfType<TabStripItem>(),
            i => i.Classes.Contains("cupertino-nopill"));

        window.MouseUp(new Point(start.X + 24, start.Y + 14), MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, strip.SelectedIndex);
    }

    [AvaloniaFact]
    public void Controls_suppress_the_platform_focus_adorner()
    {
        var window = NewWindow(ThemeVariant.Light);
        var controls = new Control[]
        {
            new Button { Content = "b" },
            new TextBox(),
            new Slider(),
            new ToggleSwitch(),
            new ListBoxItem(),
            new TabItem { Header = "t" },
            new TabStripItem { Content = "t" },
        };
        var panel = new StackPanel();
        foreach (var c in controls)
            panel.Children.Add(c);
        window.Content = panel;
        window.Show();
        window.UpdateLayout();

        foreach (var c in controls)
            Assert.True(c.FocusAdorner is null,
                $"{c.GetType().Name} still uses the platform focus adorner.");
    }

    [AvaloniaFact]
    public void Controls_do_not_tint_on_pointer_focus()
    {
        var window = NewWindow(ThemeVariant.Light);
        var box = new TextBox { Width = 200 };
        window.Content = box;
        window.Show();

        var before = box.BorderBrush;
        box.Focus();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(":focus-visible", box.Classes);
        Assert.Equal(before, box.BorderBrush);
    }

    [AvaloniaTheory]
    [InlineData("largetitle", 34)]
    [InlineData("title1", 28)]
    [InlineData("title2", 22)]
    [InlineData("title3", 20)]
    [InlineData("headline", 17)]
    [InlineData("body", 17)]
    [InlineData("callout", 16)]
    [InlineData("subheadline", 15)]
    [InlineData("footnote", 13)]
    [InlineData("caption1", 12)]
    [InlineData("caption2", 11)]
    public void The_type_ramp_classes_set_their_sizes(string cls, double size)
    {
        var window = NewWindow(ThemeVariant.Light);
        var text = new TextBlock { Text = "Sphinx of black quartz" };
        text.Classes.Add(cls);
        window.Content = text;
        window.Show();
        window.UpdateLayout();

        Assert.Equal(size, text.FontSize);
    }

    [AvaloniaFact]
    public void Accent_retints_the_accent_family_and_null_restores_system_blue()
    {
        var theme = new CupertinoTheme { Accent = Colors.Orange };

        Assert.True(theme.TryGetResource("CupertinoAccentBrush", ThemeVariant.Dark, out var brush));
        var accent = Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        Assert.Equal((Colors.Orange.R, Colors.Orange.G, Colors.Orange.B, (byte)0xFF),
                     (accent.R, accent.G, accent.B, accent.A));

        Assert.True(theme.TryGetResource("CupertinoProminentTint", ThemeVariant.Light, out var tint));
        var prominent = Assert.IsType<Color>(tint);
        Assert.Equal((byte)0xFF, prominent.A);
        Assert.Equal(Colors.Orange.R, prominent.R);

        theme.Accent = null;
        Assert.True(theme.TryGetResource("CupertinoAccentBrush", ThemeVariant.Light, out var restored));
        Assert.Equal(Color.FromRgb(0x00, 0x88, 0xFF),
                     Assert.IsAssignableFrom<ISolidColorBrush>(restored).Color);
    }

    [AvaloniaFact]
    public void Every_icon_glyph_resolves_and_renders()
    {
        var window = NewWindow(ThemeVariant.Light);
        var panel = new global::Avalonia.Controls.StackPanel();
        foreach (var glyph in CupertinoIcon.Glyphs)
            panel.Children.Add(new CupertinoIcon { Glyph = glyph });
        window.Content = panel;
        window.Show();
        window.UpdateLayout();

        foreach (var icon in panel.Children)
            Assert.True(icon.Bounds.Width > 0 && icon.Bounds.Height > 0,
                $"icon '{(icon as CupertinoIcon)?.Glyph}' laid out empty");
    }

    private static Window NewWindow(ThemeVariant variant) =>
        new() { RequestedThemeVariant = variant, Width = 400, Height = 800 };

    private static Color Composite(Color over, Color under)
    {
        var a = over.A / 255.0;
        return Color.FromRgb(
            (byte)(over.R * a + under.R * (1 - a)),
            (byte)(over.G * a + under.G * (1 - a)),
            (byte)(over.B * a + under.B * (1 - a)));
    }

    private static double Luminance(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
}
