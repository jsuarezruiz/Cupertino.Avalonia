using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Cupertino.Rendering;
using SkiaSharp;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class ResourceLifecycleTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 800, Content = content };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Popped_page_host_stops_following_theme_resources()
    {
        var root = new TextBlock { Text = "root" };
        var page = new TextBlock { Text = "page" };
        var nav = new CupertinoNavigationPage { RootContent = root };
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var window = Show(nav);
        try
        {
            Assert.True(nav.TryPush("page", "Page", page));
            Dispatcher.UIThread.RunJobs();
            var retired = Assert.IsType<Border>(page.GetVisualParent());
            var kept = Assert.IsType<Border>(root.GetVisualParent());
            Assert.NotNull(retired.Background);

            Assert.True(nav.TryPop());
            Dispatcher.UIThread.RunJobs();
            Assert.Null(retired.Child);

            var replacement = new SolidColorBrush(Colors.Magenta);
            nav.Resources["CupertinoGroupedBackgroundBrush"] = replacement;
            Dispatcher.UIThread.RunJobs();

            Assert.Same(replacement, kept.Background);
            Assert.NotSame(replacement, retired.Background);
        }
        finally { window.Close(); CupertinoAccessibility.ReduceMotion = previousMotion; }
    }

    [AvaloniaFact]
    public void Canceled_push_releases_its_host_resource_binding()
    {
        var nav = new CupertinoNavigationPage { RootContent = new Border() };
        var window = Show(nav);
        Border? rejectedHost = null;
        nav.Navigating += (_, e) =>
        {
            rejectedHost = Assert.IsType<Border>(e.To!.Host);
            e.Cancel = true;
        };
        try
        {
            Assert.False(nav.TryPush("rejected", "Rejected", new Border()));
            Assert.NotNull(rejectedHost);
            Assert.Null(rejectedHost.Child);

            var replacement = new SolidColorBrush(Colors.Magenta);
            nav.Resources["CupertinoGroupedBackgroundBrush"] = replacement;
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(replacement, rejectedHost.Background);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Search_results_keep_one_collection_instance_while_filtering()
    {
        var items = new ObservableCollection<string> { "alpha", "beta", "gamma" };
        var search = new CupertinoSearchView { ItemsSource = items };
        var window = Show(search);
        try
        {
            var filtered = search.FilteredItems;
            var results = search.GetVisualDescendants().OfType<ListBox>().First();
            Assert.Same(filtered, results.ItemsSource);

            search.Text = "a";
            Dispatcher.UIThread.RunJobs();
            Assert.Same(filtered, search.FilteredItems);
            Assert.Same(filtered, results.ItemsSource);
            Assert.Equal(new[] { "alpha", "beta", "gamma" }, filtered.Cast<string>());

            search.Text = "be";
            Dispatcher.UIThread.RunJobs();
            Assert.Same(filtered, search.FilteredItems);
            Assert.Equal(new[] { "beta" }, filtered.Cast<string>());

            search.Text = "";
            Dispatcher.UIThread.RunJobs();
            Assert.Same(filtered, search.FilteredItems);
            Assert.Equal(items, filtered.Cast<string>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Search_refresh_replaces_an_equal_reference_object_in_place()
    {
        var previous = new SearchItem(1);
        var replacement = new SearchItem(1);
        var items = new List<SearchItem> { previous };
        var search = new CupertinoSearchView
        {
            ItemsSource = items,
            SelectedItem = previous,
        };
        var filtered = search.FilteredItems;

        items[0] = replacement;
        search.Refresh();

        Assert.Same(filtered, search.FilteredItems);
        Assert.Same(replacement, search.FilteredItems[0]);
        Assert.Same(replacement, search.SelectedItem);
    }

    [AvaloniaFact]
    public void Search_observable_replace_selects_the_equal_replacement_instance()
    {
        var previous = new SearchItem(1);
        var replacement = new SearchItem(1);
        var items = new ObservableCollection<SearchItem> { previous };
        var search = new CupertinoSearchView
        {
            ItemsSource = items,
            SelectedItem = previous,
        };
        var window = Show(search);
        try
        {
            items[0] = replacement;

            Assert.Same(replacement, search.FilteredItems[0]);
            Assert.Same(replacement, search.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Search_source_swap_selects_the_equal_replacement_instance()
    {
        var previous = new SearchItem(1);
        var replacement = new SearchItem(1);
        var search = new CupertinoSearchView
        {
            ItemsSource = new[] { previous },
            SelectedItem = previous,
        };

        search.ItemsSource = new[] { replacement };

        Assert.Same(replacement, search.FilteredItems[0]);
        Assert.Same(replacement, search.SelectedItem);
    }

    [AvaloniaFact]
    public void Closing_time_picker_releases_retired_wheel_resource_bindings()
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var picker = new CupertinoTimePicker();
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var layer = OverlayLayer.GetOverlayLayer(picker)!;
            var retired = layer.GetVisualDescendants().OfType<CupertinoWheel>().First();

            picker.IsDropDownOpen = false;
            Dispatcher.UIThread.RunJobs();
            var replacement = new SolidColorBrush(Colors.Magenta);
            picker.Resources["CupertinoLabelBrush"] = replacement;
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(replacement, retired.Foreground);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = previousMotion;
        }
    }

    [AvaloniaFact]
    public void Pulse_behind_reaches_an_earlier_sibling_glass_surface()
    {
        var glass = new GlassSurface { IsBackdropFrozen = true };
        var animatedContent = new Border();
        var panel = new Grid { Width = 200, Height = 100 };
        panel.Children.Add(glass);
        panel.Children.Add(new Border { Child = animatedContent });
        var window = Show(panel);
        try
        {
            Assert.False(glass.HasActivePulse);

            GlassSurface.PulseBehind(animatedContent);

            Assert.True(glass.HasActivePulse);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Glass_fading_in_from_transparent_resumes_sampling()
    {
        var glass = new GlassSurface { Width = 200, Height = 100, Opacity = 0 };
        var window = Show(glass);
        try
        {
            Thread.Sleep(400);
            Assert.False(glass.HasActivePulse);

            glass.Opacity = 1;

            Assert.True(glass.HasActivePulse);
        }
        finally { window.Close(); }
    }

    private sealed class CountingGlass : GlassSurface
    {
        public int Renders;

        public override void Render(DrawingContext context)
        {
            Renders++;
            base.Render(context);
        }
    }

    [AvaloniaFact]
    public void Glass_under_a_transparent_ancestor_does_not_repaint_on_activity()
    {
        var glass = new CountingGlass { Width = 100, Height = 40 };
        var window = Show(new Border { Opacity = 0, Child = glass });
        try
        {
            Thread.Sleep(400);
            for (var i = 0; i < 5; i++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            var before = glass.Renders;

            window.MouseDown(new Point(10, 10), MouseButton.Left);
            window.MouseUp(new Point(10, 10), MouseButton.Left);
            for (var i = 0; i < 10; i++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(before, glass.Renders);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Badge_maps_ascii_number_formatting_to_the_cultures_native_digits()
    {
        var previous = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NativeDigits =
            ["٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩"];
        try
        {
            CultureInfo.CurrentCulture = culture;
            var badge = new CupertinoBadge { Value = 42 };
            Assert.Equal("٤٢", badge.Text);

            badge.Value = 120;
            Assert.Equal("٩٩+", badge.Text);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(".AppleSystemUIFont", true)]
    [InlineData("System Font", true)]
    [InlineData("SF Pro", false)]
    [InlineData("SF Pro Text", false)]
    [InlineData("SF Pro Display", false)]
    public void CoreText_positioning_is_limited_to_the_system_font_face(
        string family, bool expected) =>
        Assert.Equal(expected, global::Cupertino.AppleSystemTextShaper.IsAppleSystemFamily(family));

    [Theory]
    [InlineData(7.0, 5, 5)]
    [InlineData(7.5, 5, 5)]
    [InlineData(8.0, 5, 10)]
    [InlineData(57.5, 5, 55)]
    [InlineData(59.0, 5, 60)]
    [InlineData(59.0, 7, 60)]
    [InlineData(59.0, 8, 60)]
    [InlineData(59.75, 59, 60)]
    [InlineData(22.5, 15, 15)]
    [InlineData(23.0, 15, 30)]
    public void Time_values_round_to_the_nearest_increment_with_ties_settling_lower(
        double minute, int increment, int expectedMinute)
    {
        var value = TimeSpan.FromHours(9) + TimeSpan.FromMinutes(minute);
        var result = TimeMath.Normalize(
            value, increment, TimeSpan.Zero, TimeSpan.FromHours(24) - TimeSpan.FromTicks(1));

        Assert.Equal(TimeSpan.FromHours(9) + TimeSpan.FromMinutes(expectedMinute), result);
    }

    [Fact]
    public void Frozen_backdrop_releases_its_snapshot_only_after_retirement()
    {
        using var surface = SKSurface.Create(new SKImageInfo(4, 4));
        var backdrop = new FrozenBackdrop();

        backdrop.AddHolder();
        var first = backdrop.Capture(surface);
        Assert.Same(first, backdrop.Capture(surface));

        backdrop.ReleaseIfRetired();
        Assert.NotEqual(IntPtr.Zero, first.Handle);
        Assert.Same(first, backdrop.Capture(surface));

        backdrop.AddHolder();
        backdrop.Retire();
        Assert.NotEqual(IntPtr.Zero, first.Handle);
        backdrop.ReleaseIfRetired();
        Assert.Equal(IntPtr.Zero, first.Handle);
    }

    [AvaloniaFact]
    public void Frozen_backdrop_is_retired_when_the_top_level_resizes()
    {
        var glass = new GlassSurface
        {
            IsBackdropFrozen = true,
            Width = 120,
            Height = 60,
        };
        var window = Show(glass);
        try
        {
            var before = glass.BackdropCapture;

            window.Width = 700;
            window.Height = 900;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(before, glass.BackdropCapture);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Pulsing_behind_an_animating_control_repaints_the_backdrop_in_the_same_frame()
    {
        var glass = new GlassSurface { Width = 120, Height = 60 };
        var animating = new Border { Width = 20, Height = 20 };
        var panel = new Grid { Children = { glass, animating } };
        var window = Show(panel);
        try
        {
            var before = GlassSurface.GetBackdropInvalidationCount(window);

            GlassSurface.PulseBehind(animating);

            Assert.True(
                GlassSurface.GetBackdropInvalidationCount(window) > before,
                "PulseBehind must repaint the backdrop now, not in a later frame callback.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Pulsing_on_its_own_defers_the_backdrop_repaint_to_the_frame_callback()
    {
        var glass = new GlassSurface { Width = 120, Height = 60 };
        var window = Show(glass);
        try
        {
            var before = GlassSurface.GetBackdropInvalidationCount(window);

            glass.Pulse();

            Assert.Equal(before, GlassSurface.GetBackdropInvalidationCount(window));
        }
        finally { window.Close(); }
    }

    private sealed record SearchItem(int Id);
}
