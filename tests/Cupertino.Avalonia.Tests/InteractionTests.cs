using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;
using CultureInfo = System.Globalization.CultureInfo;

namespace Cupertino.Avalonia.Tests;

public class InteractionTests
{
    private static Window ShowHosting(Control content)
    {
        var window = new Window { Width = 400, Height = 800, Content = content };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaTheory]
    [InlineData(SplitViewDisplayMode.Overlay, false, 0, 0)]
    [InlineData(SplitViewDisplayMode.Overlay, true, 100, 0)]
    [InlineData(SplitViewDisplayMode.CompactOverlay, false, 40, 40)]
    [InlineData(SplitViewDisplayMode.CompactOverlay, true, 100, 40)]
    [InlineData(SplitViewDisplayMode.Inline, false, 0, 0)]
    [InlineData(SplitViewDisplayMode.Inline, true, 100, 100)]
    [InlineData(SplitViewDisplayMode.CompactInline, false, 40, 40)]
    [InlineData(SplitViewDisplayMode.CompactInline, true, 100, 100)]
    public void Split_view_uses_template_settings_for_each_mode_and_placement(
        SplitViewDisplayMode displayMode, bool isPaneOpen, double paneLength, double contentInset)
    {
        foreach (var placement in Enum.GetValues<SplitViewPanePlacement>())
        {
            var splitView = new SplitView
            {
                Width = 300,
                Height = 200,
                DisplayMode = displayMode,
                PanePlacement = placement,
                IsPaneOpen = isPaneOpen,
                OpenPaneLength = 100,
                CompactPaneLength = 40,
                Pane = new Border(),
                Content = new Border(),
            };
            var window = ShowHosting(splitView);

            var pane = splitView.GetVisualDescendants().OfType<Panel>()
                .Single(control => control.Name == "PART_PaneRoot");
            var content = splitView.GetVisualDescendants().OfType<Panel>()
                .Single(control => control.Name == "ContentRoot");
            var scrim = splitView.GetVisualDescendants().OfType<Border>()
                .Single(control => control.Name == "Scrim");
            pane.Transitions = null;
            splitView.IsPaneOpen = !isPaneOpen;
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            splitView.IsPaneOpen = isPaneOpen;
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var horizontal = placement is SplitViewPanePlacement.Left or SplitViewPanePlacement.Right;
            var extent = horizontal ? 300 : 200;
            var paneExtent = horizontal ? pane.Bounds.Width : pane.Bounds.Height;
            var contentExtent = horizontal ? content.Bounds.Width : content.Bounds.Height;
            var contentOffset = horizontal ? content.Bounds.X : content.Bounds.Y;
            var expectedOffset = placement is SplitViewPanePlacement.Left or SplitViewPanePlacement.Top
                ? contentInset
                : 0;
            var isOverlayOpen = isPaneOpen
                && displayMode is SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay;

            Assert.Equal(paneLength, paneExtent, 3);
            Assert.Equal(extent - contentInset, contentExtent, 3);
            Assert.Equal(expectedOffset, contentOffset, 3);
            Assert.Equal(isOverlayOpen, scrim.IsHitTestVisible);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stepper_buttons_change_the_value()
    {
        var nud = new NumericUpDown { Value = 3, Minimum = 0, Maximum = 10, Increment = 1 };
        ShowHosting(nud);

        var spinner = nud.GetVisualDescendants().OfType<ButtonSpinner>().SingleOrDefault();
        Assert.True(spinner is not null,
            "NumericUpDown's template must contain a ButtonSpinner named PART_Spinner - " +
            "spin buttons placed directly in the NumericUpDown template are wired to nothing.");

        var up = spinner!.GetVisualDescendants().OfType<Button>()
                         .Single(b => b.Name == "PART_IncreaseButton");
        var down = spinner.GetVisualDescendants().OfType<Button>()
                          .Single(b => b.Name == "PART_DecreaseButton");

        up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(4m, nud.Value);

        down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(2m, nud.Value);
    }

    [AvaloniaFact]
    public void Stepper_respects_its_bounds()
    {
        var nud = new NumericUpDown { Value = 10, Minimum = 0, Maximum = 10, Increment = 1 };
        ShowHosting(nud);

        var up = nud.GetVisualDescendants().OfType<Button>()
                    .Single(b => b.Name == "PART_IncreaseButton");
        up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(10m, nud.Value);
    }

    [AvaloniaFact]
    public void Stepper_only_class_hides_the_text_field_but_keeps_the_buttons()
    {
        var nud = new NumericUpDown { Value = 1, Minimum = 0, Maximum = 5, Increment = 1 };
        nud.Classes.Add("stepper");
        ShowHosting(nud);

        var box = nud.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_TextBox");
        Assert.False(box.IsVisible);

        var up = nud.GetVisualDescendants().OfType<Button>()
                    .Single(b => b.Name == "PART_IncreaseButton");
        up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(2m, nud.Value);
    }

    [AvaloniaFact]
    public void Menu_items_and_separators_get_the_cupertino_templates()
    {
        var menu = new ContextMenu();
        var item = new MenuItem { Header = "Delete" };
        menu.Items.Add(item);
        menu.Items.Add(new Separator());

        var host = new Border { Width = 200, Height = 60, ContextMenu = menu };
        var window = ShowHosting(host);

        menu.Open(host);
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(item.IsVisible);
        Assert.True(item.Bounds.Height >= 44,
            $"context-menu row height: {item.Bounds.Height}");

        var separator = menu.GetVisualDescendants().OfType<Separator>().Single();
        Assert.True(separator.Bounds.Height <= 1.0,
            $"separator height: {separator.Bounds.Height}");
    }

    [AvaloniaFact]
    public void Slider_live_lens_clips_and_changes_fill_at_both_endpoints()
    {
        var slider = new Slider { Width = 110, Minimum = 0, Maximum = 100, Value = 0 };
        var window = ShowHosting(slider);
        var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();

        static RectangleGeometry Clip(Thumb thumb) =>
            Assert.IsType<RectangleGeometry>(SliderInteraction.GetRailClip(thumb));

        Assert.Contains(SliderInteraction.AtMinimumClass, thumb.Classes);
        Assert.DoesNotContain(SliderInteraction.AtMaximumClass, thumb.Classes);
        Assert.True(Clip(thumb).Rect.Left > 0);

        slider.Value = 50;
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(SliderInteraction.AtMinimumClass, thumb.Classes);
        Assert.DoesNotContain(SliderInteraction.AtMaximumClass, thumb.Classes);
        Assert.True(Clip(thumb).Rect.Left < 0);
        Assert.True(Clip(thumb).Rect.Right > thumb.Bounds.Width);

        slider.Value = 100;
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(SliderInteraction.AtMinimumClass, thumb.Classes);
        Assert.Contains(SliderInteraction.AtMaximumClass, thumb.Classes);
        Assert.True(Clip(thumb).Rect.Right < thumb.Bounds.Width);
    }

    [AvaloniaFact]
    public void Slider_live_lens_inherits_a_scoped_custom_fill()
    {
        var customFill = new SolidColorBrush(Color.Parse("#FFFF9500"));
        var slider = new Slider { Width = 140, Minimum = 0, Maximum = 100, Value = 60 };
        slider.Resources["CupertinoAccentBrush"] = customFill;

        ShowHosting(slider);

        var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
        Assert.Equal(customFill.Color, Assert.IsType<SolidColorBrush>(slider.Foreground).Color);
        Assert.Equal(customFill.Color, Assert.IsType<SolidColorBrush>(thumb.Foreground).Color);

        foreach (var name in new[] { "ThumbDragAccentOuter", "ThumbDragAccentSoft", "ThumbDragAccentCore" })
        {
            var segment = thumb.GetVisualDescendants().OfType<Border>().Single(border => border.Name == name);
            Assert.Equal(customFill.Color, Assert.IsType<SolidColorBrush>(segment.Background).Color);
        }

        var topRim = thumb.GetVisualDescendants().OfType<Border>()
                          .Single(border => border.Name == "ThumbDragRimTop");
        var topGradient = Assert.IsType<LinearGradientBrush>(topRim.BorderBrush);
        var strongestReflection = topGradient.GradientStops[3].Color;
        Assert.True(strongestReflection.R > strongestReflection.B,
            $"an orange slider must have a warm reflection, not system blue: {strongestReflection}");
    }

    [AvaloniaFact]
    public void Disabling_slider_interaction_clears_a_held_thumb()
    {
        var slider = new Slider { Width = 140, Minimum = 0, Maximum = 100, Value = 0 };
        var window = ShowHosting(slider);
        var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
        var centre = thumb.TranslatePoint(
            new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        Assert.Contains(SliderInteraction.ActiveClass, thumb.Classes);

        SliderInteraction.SetIsEnabled(slider, false);

        Assert.DoesNotContain(SliderInteraction.ActiveClass, thumb.Classes);
        Assert.DoesNotContain(SliderInteraction.AtMinimumClass, thumb.Classes);
        Assert.Null(SliderInteraction.GetRailClip(thumb));
        window.MouseUp(centre, MouseButton.Left);
    }
}

public class WheelTests
{
    private static (Window, CupertinoWheel) ShowWheel(bool loop = true)
    {
        var wheel = new CupertinoWheel
        {
            Items = Enumerable.Range(1, 31)
                .Select(day => day.ToString(CultureInfo.InvariantCulture))
                .ToList(),
            SelectedIndex = 14,
            ShouldLoop = loop,
            Width = 60,
            Height = 216,
        };
        var window = new Window { Width = 300, Height = 300, Content = wheel };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, wheel);
    }

    private static Point InWindow(CupertinoWheel wheel, Window window, double x, double y) =>
        wheel.TranslatePoint(new Point(x, y), window) ?? new Point(x, y);

    private static void Pump(int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Tapping_a_visible_row_selects_it()
    {
        var (window, wheel) = ShowWheel();

        var cy = wheel.Bounds.Height / 2;
        var theta = -2 * (wheel.ItemHeight / wheel.Radius);
        var y = cy + wheel.Radius * Math.Sin(theta);

        var p = InWindow(wheel, window, 30, y);
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Pump(600);

        Assert.Equal(12, wheel.SelectedIndex);
    }

    [AvaloniaFact]
    public void Tapping_the_selected_row_keeps_it()
    {
        var (window, wheel) = ShowWheel();
        var cy = wheel.Bounds.Height / 2;

        var p = InWindow(wheel, window, 30, cy);
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Pump(600);

        Assert.Equal(14, wheel.SelectedIndex);
    }

    [AvaloniaFact]
    public void A_non_looping_wheel_stops_at_its_ends()
    {
        var (window, wheel) = ShowWheel(loop: false);
        wheel.SelectedIndex = 0;
        window.UpdateLayout();

        var p = InWindow(wheel, window, 30, 8);
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Pump(600);

        Assert.Equal(0, wheel.SelectedIndex);
    }

    [AvaloniaFact]
    public void Detaching_during_settle_restores_the_committed_integral_offset()
    {
        var (window, wheel) = ShowWheel();
        var cy = wheel.Bounds.Height / 2;
        var theta = -2 * (wheel.ItemHeight / wheel.Radius);
        var point = InWindow(wheel, window, 30, cy + wheel.Radius * Math.Sin(theta));

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.Equal(12, wheel.SelectedIndex);

        window.Content = null;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var offset = (double)typeof(CupertinoWheel)
            .GetField("_offset", System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(wheel)!;
        Assert.Equal(wheel.SelectedIndex, offset, 5);
    }
}

public class CalendarTests
{
    private static CupertinoMonthGrid ShowGrid(double width, DateTime month)
    {
        var grid = new CupertinoMonthGrid
        {
            Width = width,
            DisplayMonth = month,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = new Window { Width = width + 40, Height = 500, Content = grid };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return grid;
    }

    [AvaloniaFact]
    public void The_grid_scales_from_a_square_cell()
    {
        var wide = ShowGrid(7 * 55.9, new DateTime(2026, 7, 1));
        var narrow = ShowGrid(298, new DateTime(2026, 7, 1));

        foreach (var (grid, cell) in new[] { (wide, 55.9), (narrow, 298 / 7.0) })
        {
            var expected = cell * 0.314 + 5 * cell;
            Assert.True(Math.Abs(grid.Bounds.Height - expected) < 1.0,
                        $"cell {cell:F2}: expected {expected:F2}, actual {grid.Bounds.Height:F2}");
        }
    }

    [AvaloniaFact]
    public void July_2026_starts_on_a_wednesday()
    {
        var grid = ShowGrid(298, new DateTime(2026, 7, 1));
        var cell = 298 / 7.0;
        var weekdayBand = cell * 0.314;

        const int column = 2;

        var picked = default(DateTime?);
        grid.DayPicked += (_, d) => picked = d;

        grid.RaiseEvent(new PointerReleasedEventArgs(
            grid, new Pointer(0, PointerType.Mouse, true), grid,
            new Point(cell * (column + 0.5), weekdayBand + cell * 0.5),
            0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(new DateTime(2026, 7, 1), picked);
    }

    [AvaloniaFact]
    public void Tapping_the_weekday_header_selects_nothing()
    {
        var grid = ShowGrid(298, new DateTime(2026, 7, 1));
        var picked = false;
        grid.DayPicked += (_, _) => picked = true;

        grid.RaiseEvent(new PointerReleasedEventArgs(
            grid, new Pointer(0, PointerType.Mouse, true), grid, new Point(100, 2),
            0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(picked);
    }

    [AvaloniaFact]
    public void The_calendar_follows_a_selection_into_its_month()
    {
        var view = new CupertinoCalendarView { DisplayMonth = new DateTime(2026, 7, 1) };
        var window = new Window { Width = 360, Height = 420, Content = view };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.SelectedDate = new DateTimeOffset(new DateTime(2025, 3, 20));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(new DateTime(2025, 3, 1), view.DisplayMonth);
    }

    [AvaloniaFact]
    public void Calendar_normalizes_changed_year_bounds_and_rebuilds_the_wheel()
    {
        var view = new CupertinoCalendarView
        {
            DisplayMonth = new DateTime(2024, 7, 1),
            MinYear = 2020,
            MaxYear = 2030,
        };
        var window = new Window { Width = 360, Height = 420, Content = view };
        window.Show();
        window.UpdateLayout();

        view.MinYear = 2035;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(2035, view.MinYear);
        Assert.Equal(2035, view.MaxYear);
        Assert.Equal(2035, view.DisplayMonth.Year);
        Assert.Contains(view.GetVisualDescendants().OfType<CupertinoWheel>(),
            wheel => wheel.Items?.SequenceEqual(new[] { "2035" }) == true);

        view.MaxYear = -20;
        Assert.Equal(1, view.MinYear);
        Assert.Equal(1, view.MaxYear);
        Assert.Equal(1, view.DisplayMonth.Year);
    }

    [AvaloniaFact]
    public void Calendar_clamps_navigation_and_selection_to_its_effective_date_range()
    {
        var view = new CupertinoCalendarView
        {
            MinYear = 2026,
            MaxYear = 2026,
            MinimumDate = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            MaximumDate = new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero),
            DisplayMonth = new DateTime(2026, 3, 1),
        };
        var window = new Window { Width = 360, Height = 420, Content = view };
        window.Show();
        window.UpdateLayout();

        var previous = view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "PART_PreviousButton");
        var next = view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "PART_NextButton");
        Assert.False(previous.IsEnabled);
        Assert.True(next.IsEnabled);

        view.SelectedDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTime(2026, 3, 10), view.SelectedDate?.Date);
        Assert.Equal(new DateTime(2026, 3, 1), view.DisplayMonth);

        view.DisplayMonth = new DateTime(2026, 4, 1);
        Assert.True(previous.IsEnabled);
        Assert.False(next.IsEnabled);
        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new DateTime(2026, 4, 1), view.DisplayMonth);
    }

    [AvaloniaFact]
    public void Wheel_normalizes_geometry_and_observes_item_mutations()
    {
        var items = new ObservableCollection<string> { "One", "Two", "Three" };
        var wheel = new CupertinoWheel
        {
            Items = items,
            ShouldLoop = false,
            SelectedIndex = 2,
            Radius = 0,
            ItemHeight = double.NaN,
            FontSize = double.PositiveInfinity,
        };
        var window = new Window { Width = 200, Height = 260, Content = wheel };
        window.Show();
        window.UpdateLayout();

        Assert.Equal(86, wheel.Radius);
        Assert.Equal(32, wheel.ItemHeight);
        Assert.Equal(23, wheel.FontSize);

        items.RemoveAt(2);
        Assert.Equal(1, wheel.SelectedIndex);
        items.Clear();
        Assert.Equal(0, wheel.SelectedIndex);
    }

    [AvaloniaFact]
    public void Bottom_bar_samples_the_content_under_the_bar_not_the_page_origin()
    {
        var page = new Grid { RowDefinitions = RowDefinitions.Parse("*,140") };
        page.Children.Add(new Border { Background = Brushes.Black });
        page.Children.Add(new Border { Background = Brushes.White, [Grid.RowProperty] = 1 });

        var tabs = new TabControl { Classes = { "bottom" } };
        tabs.Items.Add(new TabItem { Header = "Home", Content = page });
        var window = new Window { Width = 400, Height = 800, Content = tabs };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var presenter = tabs.GetVisualDescendants().OfType<ItemsPresenter>()
            .Single(item => item.Name == "PART_ItemsPresenter");
        Assert.True(BackdropLuminanceSampler.TryIsDark(tabs, presenter, out var isDark));
        Assert.False(isDark);
    }
}

public class HapticTests : IDisposable
{
    private readonly List<HapticFeedback> _played = new();

    public HapticTests()
    {
        CupertinoHaptics.IsEnabled = true;
        CupertinoHaptics.Handler = k => _played.Add(k);
    }

    public void Dispose()
    {
        CupertinoHaptics.Handler = null;
        CupertinoHaptics.IsEnabled = true;
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public void A_switch_flip_is_a_light_impact()
    {
        var sw = new ToggleSwitch();
        var window = new Window { Width = 300, Height = 200, Content = sw };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        _played.Clear();
        sw.IsChecked = true;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains(HapticFeedback.ImpactLight, _played);
    }

    [AvaloniaFact]
    public void A_wheel_ticks_once_per_row_it_passes()
    {
        var wheel = new CupertinoWheel
        {
            Items = Enumerable.Range(1, 31)
                .Select(day => day.ToString(CultureInfo.InvariantCulture))
                .ToList(),
            SelectedIndex = 14,
            Width = 60,
            Height = 216,
        };
        var window = new Window { Width = 300, Height = 300, Content = wheel };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        _played.Clear();

        Point At(double y) => wheel.TranslatePoint(new Point(30, y), window) ?? new Point(30, y);
        var start = wheel.Bounds.Height / 2;

        window.MouseDown(At(start), MouseButton.Left);
        for (var i = 1; i <= 12; i++)
            window.MouseMove(At(start - i * (wheel.ItemHeight / 4)));
        window.MouseUp(At(start - 12 * (wheel.ItemHeight / 4)), MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.All(_played, k => Assert.Equal(HapticFeedback.Selection, k));
        Assert.InRange(_played.Count, 2, 5);
    }

    [AvaloniaFact]
    public void Showing_a_wheel_does_not_buzz()
    {
        _played.Clear();
        var wheel = new CupertinoWheel
        {
            Items = Enumerable.Range(1, 12)
                .Select(day => day.ToString(CultureInfo.InvariantCulture))
                .ToList(),
            SelectedIndex = 5,
            Width = 60,
            Height = 216,
        };
        var window = new Window { Width = 300, Height = 300, Content = wheel };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.Empty(_played);
    }

    [AvaloniaFact]
    public void A_throwing_handler_is_swallowed()
    {
        CupertinoHaptics.Handler = _ => throw new InvalidOperationException("no haptic engine");
        CupertinoHaptics.Play(HapticFeedback.Selection);
    }

    [AvaloniaFact]
    public void Disabling_haptics_silences_them()
    {
        CupertinoHaptics.IsEnabled = false;
        _played.Clear();
        CupertinoHaptics.Play(HapticFeedback.Selection);
        Assert.Empty(_played);
    }
}

public class NavigationBarTests
{
    private static (Window, CupertinoNavigationBar) Show(bool large = true)
    {
        var bar = new CupertinoNavigationBar { Title = "Settings", Width = 402, IsLargeTitle = large };
        var window = new Window { Width = 402, Height = 600, Content = bar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, bar);
    }

    [AvaloniaFact]
    public void The_large_title_has_room_to_render()
    {
        var (_, bar) = Show();
        var title = bar.GetVisualDescendants().OfType<TextBlock>()
                       .Single(t => t.Name == "PART_LargeTitle");

        Assert.True(title.Bounds.Height > 30,
                    $"large title height: {title.Bounds.Height}");
        Assert.True(bar.Bounds.Height > 105,
                    $"large-title bar height: {bar.Bounds.Height}");
    }

    [AvaloniaFact]
    public void An_inline_bar_is_shorter_and_always_shows_its_title()
    {
        var (_, bar) = Show(large: false);

        Assert.Equal(1.0, bar.InlineTitleOpacity);
        Assert.Equal(0.0, bar.LargeTitleOpacity);
        Assert.True(bar.Bounds.Height < 70,
                    $"inline bar height: {bar.Bounds.Height}");
    }

    [AvaloniaFact]
    public void The_titles_cross_fade_without_both_being_legible()
    {
        var (_, bar) = Show();

        for (var p = 0.0; p <= 1.0; p += 0.05)
        {
            bar.CollapseProgress = p;
            Assert.False(bar.LargeTitleOpacity > 0.05 && bar.InlineTitleOpacity > 0.05,
                         $"at progress {p:F2} both titles are visible "
                         + $"({bar.LargeTitleOpacity:F2} / {bar.InlineTitleOpacity:F2})");
        }
    }

    [AvaloniaFact]
    public void The_ends_of_the_travel_are_fully_collapsed_or_fully_open()
    {
        var (_, bar) = Show();

        bar.CollapseProgress = 0;
        Assert.Equal(1.0, bar.LargeTitleOpacity, 3);
        Assert.Equal(0.0, bar.InlineTitleOpacity, 3);
        Assert.Equal(0.0, bar.BackdropOpacity, 3);

        bar.CollapseProgress = 1;
        Assert.Equal(0.0, bar.LargeTitleOpacity, 3);
        Assert.Equal(1.0, bar.InlineTitleOpacity, 3);
        Assert.Equal(1.0, bar.BackdropOpacity, 3);
    }

    [AvaloniaFact]
    public void Scrolling_drives_the_collapse()
    {
        var scroller = new ScrollViewer
        {
            Height = 200,
            Content = new StackPanel { Height = 2000 },
        };
        var bar = new CupertinoNavigationBar { Title = "Settings", Width = 402 };
        var window = new Window
        {
            Width = 402,
            Height = 600,
            Content = new Panel { Children = { scroller, bar } },
        };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        bar.Scroller = scroller;
        scroller.Offset = new Vector(0, bar.CollapseDistance);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1.0, bar.CollapseProgress, 2);

        scroller.Offset = new Vector(0, bar.CollapseDistance / 2);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(0.5, bar.CollapseProgress, 2);
    }
}

public class TabWipeTests
{
    [AvaloniaFact]
    public void A_partly_covered_tab_gets_a_gradient_foreground()
    {
        var tabs = new TabControl { Width = 320, Height = 400 };
        tabs.Classes.Add("bottom");
        for (var i = 0; i < 4; i++)
            tabs.Items.Add(new TabItem { Header = $"Tab {i}", Content = new Panel() });

        var window = new Window { Width = 360, Height = 500, Content = tabs };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var items = tabs.GetVisualDescendants().OfType<TabItem>().ToList();
        Assert.Equal(4, items.Count);

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        gradient.GradientStops.Add(new GradientStop(Colors.Black, 0));
        gradient.GradientStops.Add(new GradientStop(Colors.Black, 0.5));
        gradient.GradientStops.Add(new GradientStop(Colors.DodgerBlue, 0.5));
        gradient.GradientStops.Add(new GradientStop(Colors.DodgerBlue, 1));

        items[1].SetCurrentValue(TemplatedControl.ForegroundProperty, gradient);
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.IsType<LinearGradientBrush>(items[1].Foreground);

        items[1].ClearValue(TemplatedControl.ForegroundProperty);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.IsNotType<LinearGradientBrush>(items[1].Foreground);
    }
}

public class NavigationCollapseCurveTests
{
    private static CupertinoNavigationBar Bar()
    {
        var bar = new CupertinoNavigationBar { Title = "Settings", Width = 402 };
        var window = new Window { Width = 402, Height = 600, Content = bar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return bar;
    }

    [AvaloniaTheory]
    [InlineData(0, 1.0, 0.0)]
    [InlineData(16, 1.0, 0.0)]
    [InlineData(20, 0.68, 0.0)]
    [InlineData(32, 0.25, 0.0)]
    [InlineData(36, 0.0, 0.0)]
    [InlineData(48, 0.0, 0.51)]
    [InlineData(68, 0.0, 1.0)]
    public void The_handover_points_hold(double offset, double large, double inline)
    {
        var bar = Bar();
        bar.CollapseProgress = offset / bar.CollapseDistance;

        Assert.Equal(large, bar.LargeTitleOpacity, 1);
        Assert.Equal(inline, bar.InlineTitleOpacity, 1);
    }

    [AvaloniaFact]
    public void The_collapse_distance_is_68pt()
    {
        Assert.Equal(68.0, Bar().CollapseDistance);
    }

    [AvaloniaFact]
    public void The_large_title_is_gone_before_the_inline_one_starts()
    {
        var bar = Bar();

        for (var o = 0.0; o <= 68.0; o += 1.0)
        {
            bar.CollapseProgress = o / bar.CollapseDistance;
            Assert.False(bar.LargeTitleOpacity > 0.02 && bar.InlineTitleOpacity > 0.02,
                         $"at {o}pt both titles show "
                         + $"({bar.LargeTitleOpacity:F3} / {bar.InlineTitleOpacity:F3})");
        }
    }
}

public class TabTravelEdgeTests
{
    private static readonly Rect From = new(0, 0, 75, 62);
    private static readonly Rect To = new(150, 0, 75, 62);

    [AvaloniaFact]
    public void The_trailing_edge_lags_at_the_start()
    {
        var (left, right) = TabBarMotionModel.TravelEdges(0.10, From, To);

        Assert.True(right > From.Right + 8,
                    $"leading edge should have sprinted, it is at {right:F1}");
        Assert.True(left < From.X + 2,
                    $"trailing edge should still be parked, it is at {left:F1}");
    }

    [AvaloniaFact]
    public void The_blob_elongates_then_contracts()
    {
        double Width(double t)
        {
            var (l, r) = TabBarMotionModel.TravelEdges(t, From, To);
            return r - l;
        }

        var start = Width(0);
        var mid = Width(0.25);
        var end = Width(1.0);

        Assert.True(mid > start * 1.6, $"should elongate; {start:F1} -> {mid:F1}");
        Assert.True(Math.Abs(end - From.Width) < 1.5, $"should contract back; ended {end:F1}");
    }

    [AvaloniaFact]
    public void The_peak_stays_in_the_expected_range()
    {
        var peak = 0.0;
        for (var t = 0.0; t <= 1.0; t += 16 / 380.0)
        {
            var (l, r) = TabBarMotionModel.TravelEdges(t, From, To);
            peak = Math.Max(peak, (r - l) / From.Width);
        }

        Assert.InRange(peak, 1.95, 2.10);
    }

    [AvaloniaFact]
    public void A_leftward_hop_sprints_its_left_edge()
    {
        var from = new Rect(150, 0, 75, 62);
        var to = new Rect(0, 0, 75, 62);
        var (left, right) = TabBarMotionModel.TravelEdges(0.10, from, to);

        Assert.True(left < from.X - 8, $"leading (left) edge should sprint, it is at {left:F1}");
        Assert.True(right > from.Right - 2, $"trailing (right) edge should lag, it is at {right:F1}");
    }
}

public class TabGestureArbitrationTests
{
    [Theory]
    [InlineData(2, 12, false, true)]
    [InlineData(12, 8, false, false)]
    [InlineData(0, 9, true, false)]
    [InlineData(0, 10, true, true)]
    [InlineData(40, 12, true, true)]
    public void Vertical_scroll_takeover_uses_direction_lock_then_cancels_an_active_drag(
        double dx, double dy, bool dragging, bool expected)
    {
        Assert.Equal(expected,
            TabBarMotionModel.ShouldYieldToScroll(dx, dy, dragging));
    }
}

public class TabWipeExtentTests
{
    private static readonly IBrush Accent = Brushes.DodgerBlue;
    private static readonly IBrush Label = Brushes.Black;

    private static IBrush For(double itemX, double blobLeft, double blobRight) =>
        TabLabelWipe.Create(itemX, 75, blobLeft, blobRight, Accent, Label);

    [AvaloniaFact]
    public void An_item_the_blob_has_not_reached_is_not_tinted()
    {
        Assert.Same(Label, For(150, 0, 75));
    }

    [AvaloniaFact]
    public void An_item_the_blob_has_left_behind_is_not_tinted()
    {
        Assert.Same(Label, For(0, 150, 225));
    }

    [AvaloniaFact]
    public void An_item_fully_under_the_blob_is_accent()
    {
        Assert.Same(Accent, For(75, 60, 170));
    }

    [AvaloniaFact]
    public void An_item_the_edge_crosses_gets_a_hard_stopped_gradient()
    {
        var brush = For(75, 0, 110);
        var g = Assert.IsType<LinearGradientBrush>(brush);

        var split = 35.0 / 75.0;
        var atSplit = g.GradientStops.Count(s => Math.Abs(s.Offset - split) < 0.02);
        Assert.True(atSplit == 2,
                    $"expected a hard step at {split:F3}, found {atSplit} stops there");

        var accent = g.GradientStops.Where(s => s.Color == Colors.DodgerBlue)
                                    .Select(s => s.Offset).ToList();
        Assert.True(accent.Max() <= split + 0.02,
                    $"accent should stop at the blob edge {split:F3}, reaches {accent.Max():F3}");
        Assert.Equal(Colors.Black, g.GradientStops.Last().Color);
    }

    [AvaloniaFact]
    public void A_blob_touching_only_the_very_edge_barely_tints()
    {
        var g = Assert.IsType<LinearGradientBrush>(For(75, 0, 76));
        var accentSpan = g.GradientStops.Where(s => s.Color == Colors.DodgerBlue)
                                        .Select(s => s.Offset).ToList();
        Assert.True(accentSpan.Max() < 0.05,
                    $"only a sliver should be accent, got up to {accentSpan.Max():F3}");
    }
}

public class TabTravelHopLengthTests
{
    private const double Item = 60;

    private static (double Peak, int AtCeiling) Run(int hops)
    {
        var from = new Rect(0, 0, Item, 62);
        var to = new Rect(hops * Item, 0, Item, 62);

        var peak = 0.0; var atCeiling = 0;
        for (var t = 0.0; t <= 1.0; t += 16 / 380.0)
        {
            var (l, r) = TabBarMotionModel.TravelEdges(t, from, to);
            var s = (r - l) / Item;
            peak = Math.Max(peak, s);
            if (s >= 2.015)
                atCeiling++;
        }
        return (peak, atCeiling);
    }

    [AvaloniaTheory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_long_hop_peaks_without_plateauing(int hops)
    {
        var (peak, atCeiling) = Run(hops);

        Assert.InRange(peak, 1.95, 2.05);
        Assert.True(atCeiling <= 2,
                    $"{hops}-tab hop sat at the ceiling for {atCeiling} frames");
    }

    [AvaloniaFact]
    public void A_single_tab_hop_still_stretches_less()
    {
        var (peak, _) = Run(1);
        Assert.InRange(peak, 1.40, 1.62);
    }
}

public class ExpanderTests
{
    private static (Window, Expander) Show(bool expanded)
    {
        var expander = new Expander
        {
            Header = "Wi-Fi Networks",
            IsExpanded = expanded,
            Content = new TextBlock { Text = "Home", Height = 52 },
        };
        var window = new Window { Width = 380, Height = 400, Content = expander };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, expander);
    }

    [AvaloniaFact]
    public void The_header_row_is_52pt()
    {
        var (_, expander) = Show(expanded: false);
        var toggle = expander.GetVisualDescendants().OfType<ToggleButton>().Single();

        Assert.True(toggle.Bounds.Height >= 52,
                    $"expander row height: {toggle.Bounds.Height}");
    }

    [AvaloniaFact]
    public void Collapsed_hides_the_content()
    {
        var (_, expander) = Show(expanded: false);
        var content = expander.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.ContentPresenter>()
                              .Single(c => c.Name == "PART_ContentPresenter");

        Assert.False(content.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void Expanded_reveals_the_content_indented()
    {
        var (_, expander) = Show(expanded: true);
        var content = expander.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.ContentPresenter>()
                              .Single(c => c.Name == "PART_ContentPresenter");

        Assert.True(content.IsEffectivelyVisible);

        Assert.Equal(21, content.Margin.Left);
    }

    [AvaloniaFact]
    public void The_chevron_turns_down_when_open()
    {
        var (_, collapsed) = Show(expanded: false);
        var (_, open) = Show(expanded: true);

        global::Avalonia.Controls.Shapes.Path Chevron(Expander e) =>
            e.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>()
             .Single(p => p.Name == "Chevron");

        Assert.True(Chevron(collapsed).RenderTransform is null
                    || Chevron(collapsed).Bounds.Width > 0);
        Assert.NotNull(Chevron(open).RenderTransform);
    }

    [AvaloniaFact]
    public void Toggling_the_header_expands_it()
    {
        var (_, expander) = Show(expanded: false);
        var toggle = expander.GetVisualDescendants().OfType<ToggleButton>().Single();

        toggle.IsChecked = true;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(expander.IsExpanded);
    }
}

public class AccessibilityTests : IDisposable
{
    public void Dispose()
    {
        CupertinoAccessibility.ReduceTransparency = false;
        CupertinoAccessibility.ReduceMotion = false;
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public void Reduce_transparency_keeps_the_surface_laid_out()
    {
        var glass = new GlassSurface { Width = 200, Height = 60 };
        var window = new Window { Width = 300, Height = 200, Content = glass };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var before = glass.Bounds;
        CupertinoAccessibility.ReduceTransparency = true;
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, glass.Bounds);
    }

    [AvaloniaFact]
    public void Reduce_motion_still_changes_the_wheel_value()
    {
        CupertinoAccessibility.ReduceMotion = true;

        var wheel = new CupertinoWheel
        {
            Items = Enumerable.Range(1, 31)
                .Select(day => day.ToString(CultureInfo.InvariantCulture))
                .ToList(),
            SelectedIndex = 14,
            Width = 60,
            Height = 216,
        };
        var window = new Window { Width = 300, Height = 300, Content = wheel };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var settled = false;
        wheel.SelectionSettled += (_, _) => settled = true;

        var cy = wheel.Bounds.Height / 2;
        var theta = -2 * (wheel.ItemHeight / wheel.Radius);
        var y = cy + wheel.Radius * Math.Sin(theta);
        var p = wheel.TranslatePoint(new Point(30, y), window) ?? new Point(30, y);

        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(12, wheel.SelectedIndex);
        Assert.True(settled, "SelectionSettled must still fire without the animation");
    }

    [AvaloniaFact]
    public void The_settings_notify_so_live_surfaces_can_repaint()
    {
        var fired = 0;
        void Handler(object? s, EventArgs e) => fired++;

        CupertinoAccessibility.Changed += Handler;
        try
        {
            CupertinoAccessibility.ReduceTransparency = true;
            CupertinoAccessibility.ReduceTransparency = true;
            CupertinoAccessibility.ReduceMotion = true;
        }
        finally { CupertinoAccessibility.Changed -= Handler; }

        Assert.Equal(2, fired);
    }

    [AvaloniaFact]
    public void Activity_indicator_stops_under_a_hidden_ancestor_and_restarts_when_shown()
    {
        var indicator = new CupertinoActivityIndicator();
        var host = new Border { Child = indicator };
        var window = new Window { Width = 200, Height = 200, Content = host };
        window.Show();
        window.UpdateLayout();

        var timer = Assert.IsType<global::Avalonia.Threading.DispatcherTimer>(
            typeof(CupertinoActivityIndicator)
                .GetField("_timer", System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.NonPublic)!
                .GetValue(indicator));
        Assert.True(timer.IsEnabled);

        host.IsVisible = false;
        Assert.False(timer.IsEnabled);
        host.IsVisible = true;
        Assert.True(timer.IsEnabled);
    }
}

public class ButtonFamilyTests
{
    private static T Show<T>(T control) where T : Control
    {
        control.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
        control.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
        var window = new Window { Width = 320, Height = 200, Content = control };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return control;
    }

    [AvaloniaFact]
    public void Every_button_type_is_themed_and_capsule_height()
    {
        var cases = new (string Name, Control Control)[]
        {
            ("Button", new Button { Content = "A" }),
            ("RepeatButton", new RepeatButton { Content = "A" }),
            ("ToggleButton", new ToggleButton { Content = "A" }),
            ("SplitButton", new SplitButton { Content = "A" }),
            ("DropDownButton", new DropDownButton { Content = "A" }),
        };

        foreach (var (name, control) in cases)
        {
            Show(control);
            Assert.True(control.Bounds.Height >= 34,
                        $"{name} height: {control.Bounds.Height}");
        }
    }

    [AvaloniaFact]
    public void A_checked_toggle_button_reads_as_the_prominent_style()
    {
        var off = Show(new ToggleButton { Content = "A" });
        var on = Show(new ToggleButton { Content = "A", IsChecked = true });

        var offGlass = off.GetVisualDescendants().OfType<GlassSurface>().Single();
        var onGlass = on.GetVisualDescendants().OfType<GlassSurface>().Single();

        Assert.NotEqual(offGlass.Tint, onGlass.Tint);
    }

    [AvaloniaFact]
    public void A_hyperlink_button_has_no_capsule()
    {
        var link = Show(new HyperlinkButton { Content = "Learn more" });

        Assert.Empty(link.GetVisualDescendants().OfType<GlassSurface>());
        Assert.True(link.Bounds.Height < 34,
                    $"link height: {link.Bounds.Height}");
    }

    [AvaloniaFact]
    public void A_split_button_has_both_halves()
    {
        var split = Show(new SplitButton { Content = "Share" });
        var names = split.GetVisualDescendants().OfType<Button>()
                         .Select(b => b.Name).ToList();

        Assert.Contains("PART_PrimaryButton", names);
        Assert.Contains("PART_SecondaryButton", names);
    }
}

public class ButtonInteractionTests
{
    private static T Show<T>(T control) where T : Control
    {
        control.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;
        control.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
        var window = new Window { Width = 320, Height = 200, Content = control };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return control;
    }

    private static double GlassLight(Control c) =>
        c.GetVisualDescendants().OfType<GlassSurface>().First().LightIntensity;

    [AvaloniaTheory]
    [InlineData("Button")]
    [InlineData("RepeatButton")]
    [InlineData("ToggleButton")]
    public void Hovering_brightens_the_glass(string kind)
    {
        Control control = kind switch
        {
            "RepeatButton" => new RepeatButton { Content = "A" },
            "ToggleButton" => new ToggleButton { Content = "A" },
            _ => new Button { Content = "A" },
        };
        Show(control);

        var rest = GlassLight(control);
        ((global::Avalonia.Controls.IPseudoClasses)control.Classes).Add(":pointerover");
        control.InvalidateVisual();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(GlassLight(control) > rest,
                    $"{kind} should brighten on hover: {rest} -> {GlassLight(control)}");
    }

    [AvaloniaFact]
    public void A_split_button_chevron_opens_its_flyout()
    {
        var split = new SplitButton
        {
            Content = "Share",
            Flyout = new MenuFlyout { ItemsSource = new[] { new MenuItem { Header = "Copy" } } },
        };
        Show(split);

        var secondary = split.GetVisualDescendants().OfType<Button>()
                             .Single(b => b.Name == "PART_SecondaryButton");
        secondary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(split.Flyout!.IsOpen, "the chevron half must open the flyout");
    }
}

public class CalendarThemeTests
{
    private static Calendar Show()
    {
        var calendar = new Calendar
        {
            SelectedDate = new DateTime(2026, 7, 30),
            DisplayDate = new DateTime(2026, 7, 1),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
        };
        var window = new Window { Width = 420, Height = 460, Content = calendar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return calendar;
    }

    [AvaloniaFact]
    public void The_calendar_takes_the_cupertino_template()
    {
        var calendar = Show();

        var names = calendar.GetVisualDescendants().OfType<Button>()
                            .Select(b => b.Name).ToList();
        Assert.Contains("PART_HeaderButton", names);
        Assert.Contains("PART_PreviousButton", names);
        Assert.Contains("PART_NextButton", names);
    }

    [AvaloniaFact]
    public void Day_cells_are_44pt_square()
    {
        var calendar = Show();
        var day = calendar.GetVisualDescendants().OfType<CalendarDayButton>().First();

        Assert.Equal(44, day.Width);
        Assert.Equal(44, day.Height);
    }

    [AvaloniaFact]
    public void The_selection_disc_uses_the_expected_cell_fraction()
    {
        var calendar = Show();
        var day = calendar.GetVisualDescendants().OfType<CalendarDayButton>().First();
        var disc = day.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "Disc");

        Assert.Equal(44 * 0.787, disc.Width, 1);
    }

    [AvaloniaFact]
    public void The_selected_day_shows_its_disc()
    {
        var calendar = Show();
        var selected = calendar.GetVisualDescendants().OfType<CalendarDayButton>()
                               .FirstOrDefault(d => d.Classes.Contains(":selected"));
        Assert.NotNull(selected);

        var disc = selected!.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "Disc");
        Assert.True(disc.IsVisible, "the selected day must carry the accent disc");
    }
}

public class ThemeCoverageTests
{
    private readonly ITestOutputHelper _out;
    public ThemeCoverageTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Controls() =>
    [
        [typeof(Button)], [typeof(RepeatButton)], [typeof(ToggleButton)],
        [typeof(SplitButton)], [typeof(ToggleSplitButton)], [typeof(DropDownButton)],
        [typeof(HyperlinkButton)], [typeof(CheckBox)], [typeof(RadioButton)],
        [typeof(ToggleSwitch)], [typeof(Slider)], [typeof(TextBox)], [typeof(MaskedTextBox)],
        [typeof(ComboBox)], [typeof(NumericUpDown)], [typeof(ProgressBar)],
        [typeof(TabControl)], [typeof(TabStrip)], [typeof(ListBox)], [typeof(Expander)],
        [typeof(Calendar)], [typeof(TreeView)], [typeof(Menu)], [typeof(Label)],
        [typeof(CupertinoDatePicker)], [typeof(CupertinoTimePicker)],
        [typeof(CupertinoDateTimePicker)], [typeof(CupertinoListCell)],
        [typeof(CupertinoFormRow)], [typeof(CupertinoSearchView)],
        [typeof(CupertinoNavigationBar)], [typeof(CupertinoCalendarView)],
        [typeof(DatePicker)], [typeof(TimePicker)], [typeof(AutoCompleteBox)],
        [typeof(ToolTip)], [typeof(SplitView)], [typeof(GridSplitter)],
        [typeof(Carousel)], [typeof(TransitioningContentControl)],
        [typeof(CalendarDatePicker)], [typeof(ContextMenu)], [typeof(FlyoutPresenter)],
        [typeof(global::Avalonia.Controls.Notifications.NotificationCard)],
        [typeof(global::Avalonia.Controls.Notifications.WindowNotificationManager)],
    ];

    [AvaloniaTheory]
    [MemberData(nameof(Controls))]
    public void Every_styled_control_resolves_a_cupertino_theme(Type type)
    {
        var control = (Control)Activator.CreateInstance(type)!;
        control.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top;

        // Materialize an item container.
        if (control is Menu menu)
            menu.Items.Add(new MenuItem { Header = "File" });
        if (control is TreeView tree)
            tree.Items.Add(new TreeViewItem { Header = "Node" });

        var window = new Window { Width = 400, Height = 300, Content = control };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        if (control is TemplatedControl)
        {
            var built = control.GetVisualChildren().Any();
            _out.WriteLine($"{type.Name}: template built = {built}");
            Assert.True(built, $"{type.Name} has no Cupertino theme - it will render Fluent");
        }
    }
}

public class TimePickerFormatTests
{
    [AvaloniaTheory]
    [InlineData("24HourClock", 14, 30, "14:30")]
    [InlineData("24HourClock", 9, 5, "09:05")]
    [InlineData("12HourClock", 14, 30, "2:30")]
    public void The_label_follows_the_clock_identifier(string clock, int h, int m, string expected)
    {
        var picker = new CupertinoTimePicker
        {
            ClockIdentifier = clock,
            SelectedTime = new TimeSpan(h, m, 0),
        };

        Assert.StartsWith(expected, picker.DisplayText);

        if (clock == "24HourClock")
            Assert.DoesNotContain("M", picker.DisplayText.ToUpperInvariant()
                                             .Replace("AM", "").Replace("PM", "") + "");
    }

    [AvaloniaFact]
    public void A_24_hour_picker_has_no_period_designator()
    {
        var picker = new CupertinoTimePicker
        {
            ClockIdentifier = "24HourClock",
            SelectedTime = new TimeSpan(10, 24, 0),
        };

        Assert.Equal("10:24", picker.DisplayText);
    }

    [AvaloniaFact]
    public void Minute_increment_normalizes_the_selected_value_to_an_available_row()
    {
        var picker = new CupertinoTimePicker
        {
            ClockIdentifier = "24HourClock",
            MinuteIncrement = 5,
            SelectedTime = new TimeSpan(10, 13, 42),
        };

        Assert.Equal(new TimeSpan(10, 15, 0), picker.SelectedTime);
        Assert.Equal("10:15", picker.DisplayText);
    }

    [AvaloniaFact]
    public void Date_time_picker_applies_its_minute_increment_to_the_shared_value()
    {
        var picker = new CupertinoDateTimePicker
        {
            MinuteIncrement = 10,
            SelectedDateTime = new DateTimeOffset(2026, 8, 4, 9, 26, 20, TimeSpan.Zero),
        };

        Assert.Equal(30, picker.SelectedDateTime?.Minute);
        Assert.Equal(0, picker.SelectedDateTime?.Second);
    }
}

public class NavigationPageTests
{
    private static CupertinoNavigationPage Show()
    {
        var nav = new CupertinoNavigationPage
        {
            RootTitle = "Cupertino",
            RootContent = new TextBlock { Text = "root" },
        };
        var window = new Window { Width = 402, Height = 600, Content = nav };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return nav;
    }

    [AvaloniaFact]
    public void It_starts_at_the_root()
    {
        var nav = Show();
        Assert.Equal(0, nav.Depth);

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        Assert.Equal("Cupertino", bar.Title);
        Assert.True(bar.IsLargeTitle, "the root shows a large title");
    }

    [AvaloniaFact]
    public void Pushing_shows_the_page_and_its_title()
    {
        var nav = Show();
        nav.Push("Slider", new TextBlock { Text = "slider page" });
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, nav.Depth);

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        Assert.Equal("Slider", bar.Title);
        Assert.False(bar.IsLargeTitle, "a pushed page uses the inline title");
    }

    [AvaloniaFact]
    public void Popping_returns_to_the_root()
    {
        var nav = Show();
        nav.Push("Slider", new TextBlock());
        nav.Pop();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, nav.Depth);

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        Assert.Equal("Cupertino", bar.Title);
        Assert.True(bar.IsLargeTitle);
    }

    [AvaloniaFact]
    public void Popping_at_the_root_does_nothing()
    {
        var nav = Show();
        nav.Pop();
        nav.Pop();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, nav.Depth);
    }

    [AvaloniaFact]
    public void PopToRoot_unwinds_a_deep_stack()
    {
        var nav = Show();
        nav.Push("One", new TextBlock());
        nav.Push("Two", new TextBlock());
        nav.Push("Three", new TextBlock());
        Assert.Equal(3, nav.Depth);

        nav.PopToRoot();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, nav.Depth);
    }

    [AvaloniaFact]
    public void The_back_button_is_a_bare_chevron()
    {
        var nav = Show();
        nav.Push("Slider", new TextBlock());
        nav.Push("Steps", new TextBlock());
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        var host = Assert.IsType<GlassSurface>(bar.LeadingContent);
        var back = Assert.IsType<Button>(host.Child);

        Assert.Empty(back.GetVisualDescendants().OfType<TextBlock>());
        Assert.Contains(back.GetVisualDescendants().OfType<global::Avalonia.Controls.Shapes.Path>(),
                        p => p.Data is not null);
    }

    [AvaloniaFact]
    public void Changing_the_root_leading_content_updates_the_live_bar()
    {
        var nav = Show();
        var leading = new Button { Content = "Account" };

        nav.LeadingContent = leading;
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        Assert.Same(leading, bar.LeadingContent);
    }

    [AvaloniaFact]
    public void Changing_root_content_while_pushed_updates_the_pop_destination()
    {
        var previous = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var nav = Show();
            nav.Push("Detail", new TextBlock { Text = "detail" });
            var replacement = new TextBlock { Text = "replacement" };

            nav.RootContent = replacement;
            nav.Pop();

            Assert.Equal(0, nav.Depth);
            Assert.Same(replacement, nav.CurrentContent);
            Assert.Contains(nav.GetVisualDescendants(), visual => ReferenceEquals(visual, replacement));
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = previous;
        }
    }

    [AvaloniaFact]
    public void Reapplying_the_template_preserves_the_current_stack_entry()
    {
        var previous = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var nav = Show();
            var detail = new TextBlock { Text = "detail" };
            nav.Push("Detail", detail);
            var window = Assert.IsType<Window>(TopLevel.GetTopLevel(nav));
            Assert.True(nav.TryFindResource(typeof(CupertinoNavigationPage), out var resource));
            var theme = Assert.IsType<ControlTheme>(resource);

            nav.Theme = null;
            window.UpdateLayout();
            nav.Theme = theme;
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, nav.Depth);
            Assert.Same(detail, nav.CurrentContent);
            var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
            Assert.Equal("Detail", bar.Title);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = previous;
        }
    }
}

public class NavigationScrollTests
{
    [AvaloniaFact]
    public void The_bar_picks_up_the_pages_scroller()
    {
        var page = new ScrollViewer { Content = new StackPanel { Height = 2000 } };
        var nav = new CupertinoNavigationPage { RootTitle = "Cupertino", RootContent = page };
        var window = new Window { Width = 402, Height = 600, Content = nav };
        window.Show();
        window.UpdateLayout();

        // AttachScroller runs at Loaded priority.
        var until = DateTime.UtcNow.AddMilliseconds(400);
        while (DateTime.UtcNow < until)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        Assert.Same(page, bar.Scroller);
    }

    [AvaloniaFact]
    public void A_pushed_page_swaps_the_scroller()
    {
        var root = new ScrollViewer { Content = new StackPanel { Height = 2000 } };
        var pushed = new ScrollViewer { Content = new StackPanel { Height = 2000 } };
        var nav = new CupertinoNavigationPage { RootTitle = "Cupertino", RootContent = root };
        var window = new Window { Width = 402, Height = 600, Content = nav };
        window.Show();
        window.UpdateLayout();

        nav.Push("Slider", pushed);

        var until = DateTime.UtcNow.AddMilliseconds(400);
        while (DateTime.UtcNow < until)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
        Assert.Same(pushed, bar.Scroller);
    }
}

public class NavigationGestureTests
{
    private static (Window Window, CupertinoNavigationPage Nav) ShowStack()
    {
        var nav = new CupertinoNavigationPage
        {
            RootTitle = "Root",
            RootContent = new Border { Background = Brushes.White },
        };
        var window = new Window { Width = 400, Height = 600, Content = nav };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        nav.Push("Detail", new Border { Background = Brushes.White });
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, nav);
    }

    [AvaloniaFact]
    public void An_edge_swipe_past_halfway_pops()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, nav) = ShowStack();
            Assert.Equal(1, nav.Depth);

            window.MouseDown(new Point(6, 300), MouseButton.Left);
            window.MouseMove(new Point(120, 300));
            window.MouseMove(new Point(320, 300));
            window.MouseUp(new Point(320, 300), MouseButton.Left);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, nav.Depth);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_short_slow_edge_swipe_settles_back()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, nav) = ShowStack();

            window.MouseDown(new Point(6, 300), MouseButton.Left);
            window.MouseMove(new Point(40, 300));
            window.MouseMove(new Point(60, 300));
            window.MouseMove(new Point(56, 300));
            window.MouseMove(new Point(53, 300));
            window.MouseMove(new Point(52, 300));
            window.MouseUp(new Point(52, 300), MouseButton.Left);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, nav.Depth);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_drag_that_starts_away_from_the_edge_does_nothing()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, nav) = ShowStack();

            window.MouseDown(new Point(200, 300), MouseButton.Left);
            window.MouseMove(new Point(390, 300));
            window.MouseUp(new Point(390, 300), MouseButton.Left);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, nav.Depth);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }
}

public class NavigationTitleCentringTests
{
    [AvaloniaTheory]
    [InlineData(0, 0)]
    [InlineData(0, 90)]
    [InlineData(120, 40)]
    public void The_title_stays_on_the_bars_centre(double leading, double trailing)
    {
        // Keep the title within the headless font's centre slot.
        var bar = new CupertinoNavigationBar
        {
            Title = "Hub",
            IsLargeTitle = false,
            Width = 402,
            LeadingContent = leading > 0
                ? new Border { Width = leading, Height = 30 } : null,
            TrailingContent = trailing > 0
                ? new Border { Width = trailing, Height = 30 } : null,
        };
        var window = new Window { Width = 402, Height = 300, Content = bar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var title = bar.GetVisualDescendants().OfType<TextBlock>()
                       .Single(t => t.Name == "PART_InlineTitle");
        var centre = title.TranslatePoint(new Point(title.Bounds.Width / 2, 0), bar)!.Value.X;

        Assert.InRange(centre, 201 - 2, 201 + 2);
    }

    [AvaloniaFact]
    public void A_long_title_truncates_clear_of_a_wide_back_button()
    {
        var bar = new CupertinoNavigationBar
        {
            Title = "CalendarDatePicker and then some",
            IsLargeTitle = false,
            Width = 402,
            LeadingContent = new Border { Width = 120, Height = 30 },
        };
        var window = new Window { Width = 402, Height = 300, Content = bar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var title = bar.GetVisualDescendants().OfType<TextBlock>()
                       .Single(t => t.Name == "PART_InlineTitle");
        var leading = bar.GetVisualDescendants().OfType<Control>()
                         .Single(c => c.Name == "PART_Leading");

        var titleLeft = title.TranslatePoint(default, bar)!.Value.X;
        var buttonRight = leading.TranslatePoint(new Point(leading.Bounds.Width, 0), bar)!.Value.X;

        Assert.True(titleLeft >= buttonRight,
                    $"title starts at {titleLeft:F1} but the leading button runs to {buttonRight:F1}");
    }

    [AvaloniaFact]
    public void A_detached_tab_moves_to_the_circle_and_still_selects()
    {
        var search = new TabItem
        {
            Header = new TextBlock { Text = "s" },
            Content = new Panel(),
        };
        Tabs.SetIsDetached(search, true);

        var tc = new TabControl { SelectedIndex = 0 };
        tc.Classes.Add("bottom");
        tc.Items.Add(new TabItem { Header = new TextBlock { Text = "a" }, Content = new Panel() });
        tc.Items.Add(new TabItem { Header = new TextBlock { Text = "b" }, Content = new Panel() });
        tc.Items.Add(search);
        var window = new Window { Width = 400, Height = 800, Content = tc };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var accessory = tc.GetVisualDescendants().OfType<GlassSurface>()
                          .Single(g => g.Name == "AccessoryHost");
        Assert.True(accessory.IsVisible,
            "marking a tab detached must reveal the circle without Tabs.Accessory being set");
        Assert.False(search.IsVisible, "a detached tab must leave the capsule strip");
        Assert.True(accessory.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "s"),
            "the detached tab's header must move into the circle");

        var button = accessory.GetVisualDescendants().OfType<Button>().Single();
        var centre = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Same(search, tc.SelectedItem);

        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotSame(search, tc.SelectedItem);
    }

    [AvaloniaFact]
    public void Removing_a_detached_tab_releases_the_accessory_and_restores_its_header()
    {
        var header = new TextBlock { Text = "search" };
        var search = new TabItem { Header = header, Content = new Panel() };
        Tabs.SetIsDetached(search, true);
        var tabs = new TabControl { Classes = { "bottom" } };
        tabs.Items.Add(new TabItem { Header = "home", Content = new Panel() });
        tabs.Items.Add(search);
        var window = new Window { Width = 400, Height = 800, Content = tabs };
        window.Show();
        window.UpdateLayout();

        Assert.NotNull(Tabs.GetAccessory(tabs));
        tabs.Items.Remove(search);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(Tabs.GetAccessory(tabs));
        Assert.True(search.IsVisible);
        Assert.Same(header, search.Header);
    }

    [AvaloniaFact]
    public void A_custom_accessory_temporarily_wins_and_the_detached_tab_returns_when_cleared()
    {
        var header = new TextBlock { Text = "search" };
        var search = new TabItem { Header = header, Content = new Panel() };
        Tabs.SetIsDetached(search, true);
        var tabs = new TabControl { Classes = { "bottom" } };
        tabs.Items.Add(new TabItem { Header = "home", Content = new Panel() });
        tabs.Items.Add(search);
        var window = new Window { Width = 400, Height = 800, Content = tabs };
        window.Show();
        window.UpdateLayout();

        var custom = new TextBlock { Text = "custom" };
        Tabs.SetAccessory(tabs, custom);

        Assert.Same(custom, Tabs.GetAccessory(tabs));
        Assert.True(search.IsVisible);
        Assert.Same(header, search.Header);

        Tabs.SetAccessory(tabs, null);

        Assert.NotNull(Tabs.GetAccessory(tabs));
        Assert.False(search.IsVisible);
        Assert.Null(search.Header);
    }


}

public class SegmentedTravelTimelineTests
{
    [Fact]
    public void Held_lens_becomes_optically_thicker_instead_of_only_scaling()
    {
        var resting = TabBarMotionModel.SegmentedLensOptics(1.0);
        var held = TabBarMotionModel.SegmentedLensOptics(1.5);

        Assert.Equal(5.0, resting.Thickness, 3);
        Assert.Equal(8.0, resting.Refraction, 3);
        Assert.Equal(0.25, resting.Chroma, 3);
        Assert.Equal(1.025, resting.Magnification, 3);
        Assert.Equal(9.0, held.Thickness, 3);
        Assert.Equal(13.0, held.Refraction, 3);
        Assert.Equal(0.42, held.Chroma, 3);
        Assert.Equal(1.045, held.Magnification, 3);
    }

    [Fact]
    public void Segmented_drag_does_not_inherit_the_two_cell_tab_bar_stretch()
    {
        Assert.Equal(1.10, TabBarMotionModel.DragWidthScale(16, segmented: true), 3);
        Assert.Equal(1.25, TabBarMotionModel.DragWidthScale(80, segmented: true), 3);
        Assert.Equal(2.00, TabBarMotionModel.DragWidthScale(80, segmented: false), 3);
    }

    [Fact]
    public void Position_is_a_critically_damped_spring()
    {
        Assert.InRange(TabBarMotionModel.SegmentedTravel(31.5, 1).Progress, 0.45, 0.58);
        Assert.InRange(TabBarMotionModel.SegmentedTravel(75, 1).Progress, 0.90, 0.98);
        Assert.InRange(TabBarMotionModel.SegmentedTravel(180, 1).Progress, 0.99, 1.0);
        Assert.Equal(0, TabBarMotionModel.SegmentedTravel(0, 1).Progress, 3);
    }

    [Fact]
    public void White_fill_lifts_into_the_lens_for_the_flight()
    {
        Assert.Equal(1, TabBarMotionModel.SegmentedTravel(0, 1).FillOpacity);
        Assert.Equal(0, TabBarMotionModel.SegmentedTravel(45, 1).FillOpacity);
        Assert.Equal(1, TabBarMotionModel.SegmentedTravel(45, 1).LensOpacity);
        Assert.Equal(1, TabBarMotionModel.SegmentedTravel(177, 1).FillOpacity);
        Assert.Equal(0, TabBarMotionModel.SegmentedTravel(177, 1).LensOpacity);
    }

    [Fact]
    public void Width_swells_with_distance_and_lands_with_a_squish()
    {
        Assert.InRange(TabBarMotionModel.SegmentedTravel(45, 1).WidthScale, 1.07, 1.13);
        Assert.InRange(TabBarMotionModel.SegmentedTravel(45, 2).WidthScale, 1.16, 1.23);
        Assert.InRange(TabBarMotionModel.SegmentedTravel(108, 1).WidthScale, 0.92, 0.94);
        Assert.Equal(1, TabBarMotionModel.SegmentedTravel(180, 1).WidthScale, 2);
    }
}
