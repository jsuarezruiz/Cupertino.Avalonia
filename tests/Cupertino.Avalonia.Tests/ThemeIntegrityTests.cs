using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
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

    [AvaloniaFact]
    public void Light_and_dark_declare_the_same_resource_types()
    {
        var light = ThemeDictionary(ThemeVariant.Light);
        var dark = ThemeDictionary(ThemeVariant.Dark);

        foreach (var key in light.Keys)
        {
            Assert.True(dark.TryGetResource(key, ThemeVariant.Dark, out var darkValue),
                $"Missing from Dark: {key}");
            Assert.True(light.TryGetResource(key, ThemeVariant.Light, out var lightValue),
                $"Missing from Light: {key}");
            Assert.Equal(lightValue?.GetType(), darkValue?.GetType());
        }
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
            new CupertinoSearchView { ItemsSource = new[] { "a", "b" } },
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

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void ToggleSwitch_shows_only_the_active_state_content(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = NewWindow(variant);
        var toggle = new ToggleSwitch
        {
            OnContent = "On",
            OffContent = "Off",
            IsChecked = false,
        };
        window.Content = toggle;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var on = toggle.GetVisualDescendants().OfType<ContentPresenter>()
            .Single(presenter => presenter.Name == "PART_OnContentPresenter");
        var off = toggle.GetVisualDescendants().OfType<ContentPresenter>()
            .Single(presenter => presenter.Name == "PART_OffContentPresenter");

        Assert.False(on.IsVisible);
        Assert.True(off.IsVisible);

        toggle.IsChecked = true;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(on.IsVisible);
        Assert.False(off.IsVisible);

        toggle.IsEnabled = false;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(on.IsVisible);
        Assert.False(off.IsVisible);
        Assert.Equal(0.5, toggle.Opacity);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void ComboBox_shows_placeholder_until_an_item_is_selected(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = NewWindow(variant);
        var comboBox = new ComboBox
        {
            PlaceholderText = "Choose a size",
            ItemsSource = new[] { "Small", "Medium", "Large" },
        };
        window.Content = comboBox;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var placeholder = comboBox.GetVisualDescendants().OfType<TextBlock>()
            .Single(text => text.Name == "Placeholder");
        var content = comboBox.GetVisualDescendants().OfType<ContentControl>()
            .Single(control => control.Name == "ContentPresenter");

        Assert.True(placeholder.IsVisible);
        Assert.False(content.IsVisible);

        comboBox.SelectedIndex = 1;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(placeholder.IsVisible);
        Assert.True(content.IsVisible);
        Assert.Equal("Medium", content.Content);
    }

    [AvaloniaFact]
    public void Label_hides_the_access_key_marker()
    {
        var window = NewWindow(ThemeVariant.Light);
        var label = new Label { Content = "_Name" };
        window.Content = label;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var presenter = label.GetVisualDescendants().OfType<ContentPresenter>().Single();

        Assert.True(presenter.RecognizesAccessKey);
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
    public void Three_item_bottom_bar_matches_ios_26_geometry()
    {
        var window = NewWindow(ThemeVariant.Light);
        window.Width = 402;
        var tabs = new TabControl { Classes = { "bottom" }, SelectedIndex = 0 };
        var inbox = new TabItem { Header = "Inbox", Content = new Border() };
        var news = new TabItem { Header = "New", Content = new Border() };
        var settings = new TabItem { Header = "Settings", Content = new Border() };
        Tabs.SetBadgeValue(inbox, 3);
        Tabs.SetBadgeValue(news, -1);
        tabs.Items.Add(inbox);
        tabs.Items.Add(news);
        tabs.Items.Add(settings);
        window.Content = tabs;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var bar = tabs.GetVisualDescendants().OfType<GlassSurface>()
                      .Single(surface => surface.Name == "PART_BarCapsule");
        var items = tabs.GetVisualDescendants().OfType<TabItem>()
                        .OrderBy(item => item.TranslatePoint(default, bar)!.Value.X)
                        .ToArray();

        Assert.InRange(bar.Bounds.Width, 271.5, 272.5);
        Assert.All(items, item => Assert.InRange(item.Bounds.Width, 85.5, 86.5));
        var centres = items.Select(item =>
            item.TranslatePoint(new Point(item.Bounds.Width / 2, 0), bar)!.Value.X).ToArray();
        Assert.InRange(centres[0], 49.5, 50.5);
        Assert.InRange(centres[1], 135.5, 136.5);
        Assert.InRange(centres[2], 221.5, 222.5);

        var lens = inbox.GetVisualDescendants().OfType<GlassSurface>()
                        .Single(surface => surface.Name == "Lens");
        Assert.InRange(lens.Bounds.Width, 91.5, 92.5);
        var lensLeft = lens.TranslatePoint(default, bar)!.Value.X;
        Assert.InRange(lensLeft, 3.5, 4.5);

        var count = inbox.GetVisualDescendants().OfType<CupertinoBadge>().Single();
        var dot = news.GetVisualDescendants().OfType<CupertinoBadge>().Single();
        Assert.InRange(count.Bounds.Height, 17.5, 18.5);
        Assert.InRange(dot.Bounds.Width, 17.5, 18.5);
        Assert.InRange(dot.Bounds.Height, 17.5, 18.5);
    }

    [AvaloniaTheory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Adjacent_bottom_bar_surfaces_do_not_overlap(bool useStrip, int itemCount)
    {
        var window = NewWindow(ThemeVariant.Light);
        window.Width = 402;
        Control owner;
        Control selected;
        Control adjacent;

        if (useStrip)
        {
            var strip = new TabStrip { Classes = { "bottom" }, SelectedIndex = 0 };
            var items = Enumerable.Range(0, itemCount)
                .Select(index => new TabStripItem { Content = $"Item {index}" })
                .ToArray();
            foreach (var item in items)
                strip.Items.Add(item);
            owner = strip;
            selected = items[0];
            adjacent = items[1];
        }
        else
        {
            var tabs = new TabControl { Classes = { "bottom" }, SelectedIndex = 0 };
            var items = Enumerable.Range(0, itemCount)
                .Select(index => new TabItem { Header = $"Item {index}", Content = new Border() })
                .ToArray();
            foreach (var item in items)
                tabs.Items.Add(item);
            owner = tabs;
            selected = items[0];
            adjacent = items[1];
        }

        window.Content = owner;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var bar = owner.GetVisualDescendants().OfType<GlassSurface>()
            .Single(surface => surface.Name == "PART_BarCapsule");
        var lens = selected.GetVisualDescendants().OfType<GlassSurface>()
            .Single(surface => surface.Name == "Lens");
        var hover = adjacent.GetVisualDescendants().OfType<Border>()
            .Single(border => border.Name == "Hover");
        var lensRight = lens.TranslatePoint(default, bar)!.Value.X + lens.Bounds.Width;
        var hoverLeft = hover.TranslatePoint(default, bar)!.Value.X;

        Assert.True(hoverLeft - lensRight >= 4,
            $"Selected lens ends at {lensRight:F1}, hover starts at {hoverLeft:F1}.");
    }

    [AvaloniaFact]
    public void Two_item_bottom_bar_compacts_like_ios_26()
    {
        var window = NewWindow(ThemeVariant.Light);
        var strip = new TabStrip
        {
            Classes = { "bottom" },
            Width = 160,
            SelectedIndex = 0,
        };
        strip.Items.Add(new TabStripItem { Content = "Home" });
        strip.Items.Add(new TabStripItem { Content = "Settings" });
        window.Content = strip;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var bar = strip.GetVisualDescendants().OfType<GlassSurface>()
                       .Single(surface => surface.Name == "PART_BarCapsule");
        var items = strip.GetVisualDescendants().OfType<TabStripItem>()
                         .OrderBy(item => item.TranslatePoint(default, bar)!.Value.X)
                         .ToArray();

        Assert.Contains("cupertino-two-item", strip.Classes);
        Assert.InRange(bar.Bounds.Width, 117.5, 118.5);
        Assert.All(items, item => Assert.InRange(item.Bounds.Width, 50.5, 51.5));
        var centres = items.Select(item =>
            item.TranslatePoint(new Point(item.Bounds.Width / 2, 0), bar)!.Value.X).ToArray();
        Assert.InRange(centres[0], 33.0, 34.0);
        Assert.InRange(centres[1], 84.0, 85.0);

        var lens = items[0].GetVisualDescendants().OfType<GlassSurface>()
                           .Single(surface => surface.Name == "Lens");
        Assert.InRange(lens.Bounds.Width, 58.5, 59.5);
        var lensLeft = lens.TranslatePoint(default, bar)!.Value.X;
        Assert.InRange(lensLeft, 3.5, 4.5);
    }

    [AvaloniaFact]
    public void Two_item_bottom_bar_fits_narrow_widths()
    {
        var window = NewWindow(ThemeVariant.Light);
        var strip = new TabStrip
        {
            Classes = { "bottom" },
            Width = 150,
            SelectedIndex = 0,
        };
        strip.Items.Add(new TabStripItem { Content = "Home" });
        strip.Items.Add(new TabStripItem { Content = "Settings" });
        window.Content = strip;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var bar = strip.GetVisualDescendants().OfType<GlassSurface>()
            .Single(surface => surface.Name == "PART_BarCapsule");
        var origin = bar.TranslatePoint(default, strip)!.Value.X;

        Assert.True(origin >= 21);
        Assert.True(origin + bar.Bounds.Width <= strip.Bounds.Width - 21);
        Assert.All(strip.GetVisualDescendants().OfType<TabStripItem>(),
            item => Assert.InRange(item.Bounds.Width, 45.5, 46.5));
    }

    [AvaloniaFact]
    public void Bottom_bar_geometry_tracks_visible_items()
    {
        var window = NewWindow(ThemeVariant.Light);
        var strip = new TabStrip
        {
            Classes = { "bottom" },
            Width = 300,
            SelectedIndex = 0,
        };
        var hidden = new TabStripItem { Content = "Hidden", IsVisible = false };
        strip.Items.Add(new TabStripItem { Content = "Home" });
        strip.Items.Add(new TabStripItem { Content = "Settings" });
        strip.Items.Add(hidden);
        window.Content = strip;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Contains("cupertino-two-item", strip.Classes);
        Assert.All(strip.GetVisualDescendants().OfType<TabStripItem>().Where(item => item.IsVisible),
            item => Assert.InRange(item.Bounds.Width, 50.5, 51.5));

        hidden.IsVisible = true;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.DoesNotContain("cupertino-two-item", strip.Classes);
        Assert.All(strip.GetVisualDescendants().OfType<TabStripItem>(),
            item => Assert.InRange(item.Bounds.Width, 81, 83));
    }

    [AvaloniaTheory]
    [InlineData(4, 375)]
    [InlineData(5, 375)]
    [InlineData(5, 402)]
    public void Bottom_tab_bar_fits_common_phone_widths(int itemCount, double width)
    {
        var window = NewWindow(ThemeVariant.Light);
        window.Width = width;
        var tabs = new TabControl { Classes = { "bottom" } };
        for (var i = 0; i < itemCount; i++)
            tabs.Items.Add(new TabItem { Header = $"Tab {i + 1}", Content = new Border() });
        window.Content = tabs;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var bar = tabs.GetVisualDescendants().OfType<GlassSurface>()
                      .Single(surface => surface.Name == "PART_BarCapsule");
        var left = bar.TranslatePoint(default, tabs)!.Value.X;

        Assert.True(left >= 0, $"bar starts outside the {width}px host at {left:F1}");
        Assert.True(left + bar.Bounds.Width <= width + 0.1,
            $"bar ends at {left + bar.Bounds.Width:F1} in a {width}px host");
    }

    [AvaloniaFact]
    public void Five_item_bottom_strip_fits_a_phone_width()
    {
        var window = NewWindow(ThemeVariant.Light);
        window.Width = 375;
        var strip = new TabStrip { Classes = { "bottom" } };
        for (var i = 0; i < 5; i++)
            strip.Items.Add(new TabStripItem { Content = $"Tab {i + 1}" });
        window.Content = strip;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var bar = strip.GetVisualDescendants().OfType<GlassSurface>()
                       .Single(surface => surface.Name == "PART_BarCapsule");
        var left = bar.TranslatePoint(default, strip)!.Value.X;

        Assert.True(left >= 0, $"bar starts outside the host at {left:F1}");
        Assert.True(left + bar.Bounds.Width <= 375.1,
            $"bar ends at {left + bar.Bounds.Width:F1} in a 375px host");
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
            item => item.Classes.Contains("cupertino-no-pill"));
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
            i => i.Classes.Contains("cupertino-no-pill"));

        window.MouseUp(new Point(start.X + 24, start.Y + 14), MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, strip.SelectedIndex);
    }

    [AvaloniaFact]
    public void Stepper_only_keeps_the_native_capsule_and_divider_geometry()
    {
        var window = NewWindow(ThemeVariant.Light);
        var stepper = new NumericUpDown
        {
            Classes = { "stepper" },
            Width = 94,
            Value = 3,
        };
        window.Content = stepper;
        window.Show();
        window.UpdateLayout();

        var capsule = stepper.GetVisualDescendants().OfType<Border>()
                             .Single(border => border.Name == "Capsule");
        var divider = stepper.GetVisualDescendants()
                             .OfType<global::Avalonia.Controls.Shapes.Rectangle>()
                             .Single(rectangle => rectangle.Name == "Divider");
        var field = stepper.GetVisualDescendants().OfType<TextBox>()
                           .Single(textBox => textBox.Name == "PART_TextBox");

        Assert.InRange(capsule.Bounds.Width, 93.5, 94.5);
        Assert.InRange(capsule.Bounds.Height, 31.5, 32.5);
        Assert.InRange(divider.Bounds.Width, 0.9, 1.1);
        Assert.InRange(divider.Bounds.Height, 23.5, 24.5);
        Assert.False(field.IsVisible);
        var capsuleLeft = capsule.TranslatePoint(default, stepper)!.Value.X;
        Assert.InRange(capsuleLeft, -0.1, 0.1);
        Assert.InRange(capsuleLeft + capsule.Bounds.Width, 93.9, 94.1);
    }

    [AvaloniaFact]
    public void Numeric_field_keeps_the_native_eight_point_stepper_gap()
    {
        var window = NewWindow(ThemeVariant.Light);
        var numeric = new NumericUpDown { Value = 3 };
        window.Content = numeric;
        window.Show();
        window.UpdateLayout();

        var field = numeric.GetVisualDescendants().OfType<TextBox>()
                           .Single(textBox => textBox.Name == "PART_TextBox");
        var capsule = numeric.GetVisualDescendants().OfType<Border>()
                             .Single(border => border.Name == "Capsule");
        var fieldLeft = field.TranslatePoint(default, numeric)!.Value.X;
        var capsuleLeft = capsule.TranslatePoint(default, numeric)!.Value.X;
        var gap = capsuleLeft - (fieldLeft + field.Bounds.Width);

        Assert.InRange(gap, 7.9, 8.1);
    }

    [AvaloniaFact]
    public void Slider_rails_are_flat_where_they_meet_the_thumb()
    {
        var window = NewWindow(ThemeVariant.Light);
        var slider = new Slider { Width = 240, Value = 50 };
        window.Content = slider;
        window.Show();
        window.UpdateLayout();

        var decrease = slider.GetVisualDescendants().OfType<RepeatButton>()
                             .Single(button => button.Name == "PART_DecreaseButton");
        var increase = slider.GetVisualDescendants().OfType<RepeatButton>()
                             .Single(button => button.Name == "PART_IncreaseButton");
        var filledRail = decrease.GetVisualDescendants().OfType<Border>()
                                 .Single(border => border.Name == "Rail");
        var remainderRail = increase.GetVisualDescendants().OfType<Border>()
                                    .Single(border => border.Name == "Rail");

        Assert.Equal(new CornerRadius(2.85, 0, 0, 2.85), filledRail.CornerRadius);
        Assert.Equal(new CornerRadius(0, 2.85, 2.85, 0), remainderRail.CornerRadius);
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

        Assert.True(theme.TryGetResource("CupertinoProminentForegroundBrush", ThemeVariant.Dark, out var label));
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(label).Color);
        Assert.True(theme.TryGetResource("CupertinoProminentRimBrush", ThemeVariant.Light, out var rim));
        Assert.Equal(Color.FromArgb(0x77, 255, 255, 255), Assert.IsAssignableFrom<ISolidColorBrush>(rim).Color);

        theme.Accent = null;
        Assert.True(theme.TryGetResource("CupertinoAccentBrush", ThemeVariant.Light, out var restored));
        Assert.Equal(Color.FromRgb(0x00, 0x88, 0xFF),
                     Assert.IsAssignableFrom<ISolidColorBrush>(restored).Color);
        Assert.True(theme.TryGetResource("CupertinoProminentForegroundBrush", ThemeVariant.Dark, out var restoredLabel));
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(restoredLabel).Color);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Prominent_buttons_use_the_native_white_label(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var resources = ThemeDictionary(variant);

        Assert.True(resources.TryGetResource(
            "CupertinoProminentForegroundBrush", variant, out var value));
        Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(value).Color);
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

    [AvaloniaFact]
    public void Carousel_native_swipe_recognizer_accepts_mouse_drag()
    {
        var carousel = new Carousel
        {
            Items =
            {
                new Border { Background = Brushes.Red },
                new Border { Background = Brushes.Blue },
            },
        };
        var window = NewWindow(ThemeVariant.Light);
        window.Content = carousel;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var panel = Assert.IsType<VirtualizingCarouselPanel>(carousel.ItemsPanelRoot);
        var recognizer = Assert.Single(
            panel.GestureRecognizers.OfType<SwipeGestureRecognizer>());
        Assert.True(recognizer.IsMouseEnabled);

        window.MouseDown(new Point(300, 200), MouseButton.Left);
        window.MouseMove(new Point(250, 200));
        window.MouseMove(new Point(200, 200));
        window.MouseMove(new Point(150, 200));
        window.MouseMove(new Point(100, 200));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(carousel.IsSwiping);
        window.MouseUp(new Point(100, 200), MouseButton.Left);
    }

    [AvaloniaFact]
    public void Transitioning_content_control_runs_its_page_transition()
    {
        var transition = new RecordingPageTransition();
        var control = new TransitioningContentControl
        {
            Content = "First",
            PageTransition = transition,
        };
        var window = NewWindow(ThemeVariant.Light);
        window.Content = control;
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        control.Content = "Second";
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, transition.CallCount);
        Assert.NotNull(transition.From);
        Assert.NotNull(transition.To);
        Assert.NotSame(transition.From, transition.To);
    }

    private sealed class RecordingPageTransition : IPageTransition
    {
        public int CallCount { get; private set; }

        public Visual? From { get; private set; }

        public Visual? To { get; private set; }

        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            CallCount++;
            From = from;
            To = to;
            return Task.CompletedTask;
        }
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
