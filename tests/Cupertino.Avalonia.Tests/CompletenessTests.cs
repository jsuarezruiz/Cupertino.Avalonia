using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Cupertino.Themes;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class NewPrimitiveTests
{
    [AvaloniaFact]
    public void Search_view_filters_and_respects_scopes()
    {
        var search = new CupertinoSearchView
        {
            ItemsSource = new[] { "Ada", "Alan", "Grace", "Design notes" },
            Text = "a",
            Scopes = new[] { "All", "People" },
            SelectedScopeIndex = 1,
        };
        search.Filter = (item, query, scope) =>
            item.ToString()!.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            (scope == 0 || !item.ToString()!.Contains(' '));
        search.Refresh();

        Assert.Equal(new[] { "Ada", "Alan", "Grace" }, search.FilteredItems.Cast<string>());

        search.Text = "zz";
        Assert.Empty(search.FilteredItems);
        search.Cancel();
        Assert.Equal(string.Empty, search.Text);
    }

    [AvaloniaFact]
    public void Search_view_observes_scope_mutations_and_clamps_selection()
    {
        var scopes = new ObservableCollection<string> { "All", "People" };
        var search = new CupertinoSearchView
        {
            ItemsSource = new[] { "Ada", "Design notes" },
            Scopes = scopes,
            SelectedScopeIndex = 1,
            Filter = (item, _, scope) => scope == 0 || !item.ToString()!.Contains(' '),
        };
        var window = new Window { Width = 400, Height = 300, Content = search };
        window.Show();
        window.UpdateLayout();

        Assert.Single(search.FilteredItems);
        scopes.RemoveAt(1);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, search.SelectedScopeIndex);
        Assert.Equal(2, search.FilteredItems.Count);
    }

    [AvaloniaFact]
    public void Search_view_uses_text_selector()
    {
        var items = new[] { new SearchItem("Ada"), new SearchItem("Grace") };
        var search = new CupertinoSearchView
        {
            ItemsSource = items,
            Text = "ada",
            SearchTextSelector = item => ((SearchItem)item).Name,
        };

        Assert.Same(items[0], Assert.Single(search.FilteredItems));
    }

    [AvaloniaFact]
    public void Page_control_clamps_its_state_and_measures_all_dots()
    {
        var pages = new CupertinoPageControl
        {
            NumberOfPages = 4,
            CurrentPage = 99,
            DotSize = 7,
            DotSpacing = 9,
        };
        var window = new Window { Width = 200, Height = 80, Content = pages };
        window.Show();
        window.UpdateLayout();

        Assert.Equal(3, pages.CurrentPage);
        Assert.True(pages.DesiredSize.Width >= 4 * 7 + 3 * 9);

        pages.NumberOfPages = 1;
        window.UpdateLayout();
        Assert.Equal(0, pages.CurrentPage);
        Assert.Equal(0, pages.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void Page_control_raises_one_change_for_a_coerced_value()
    {
        var pages = new CupertinoPageControl { NumberOfPages = 4 };
        var changes = 0;
        pages.CurrentPageChanged += (_, _) => changes++;

        pages.CurrentPage = 99;

        Assert.Equal(3, pages.CurrentPage);
        Assert.Equal(1, changes);
    }

    [AvaloniaFact]
    public void List_and_form_rows_build_their_cupertino_templates()
    {
        var panel = new StackPanel
        {
            Children =
            {
                new CupertinoListCell { Title = "Wi-Fi", Detail = "Studio", AccessoryKind = CupertinoListAccessory.Disclosure },
                new CupertinoFormRow { Label = "Name", HelpText = "Required", Content = new TextBox() },
            },
        };
        var window = new Window { Width = 360, Height = 180, Content = panel };
        window.Show();
        window.UpdateLayout();

        Assert.All(panel.Children, child => Assert.True(child.GetVisualChildren().Any()));
        Assert.All(panel.Children, child => Assert.True(child.Bounds.Height >= 44));
    }
}

file sealed record SearchItem(string Name);

public class AccessibilityAndDirectionTests
{
    [AvaloniaFact]
    public async Task Accessibility_notifications_are_marshaled_to_the_ui_thread()
    {
        var original = CupertinoAccessibility.ReduceTransparency;
        var uiThread = Environment.CurrentManagedThreadId;
        var notificationThread = -1;
        void Handler(object? sender, EventArgs e) =>
            notificationThread = Environment.CurrentManagedThreadId;

        CupertinoAccessibility.Changed += Handler;
        try
        {
            await Task.Run(() => CupertinoAccessibility.ReduceTransparency = !original);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(!original, CupertinoAccessibility.ReduceTransparency);
            Assert.Equal(uiThread, notificationThread);
        }
        finally
        {
            CupertinoAccessibility.Changed -= Handler;
            CupertinoAccessibility.ReduceTransparency = original;
        }
    }

    [AvaloniaFact]
    public void Dynamic_type_updates_the_theme_tokens()
    {
        var old = CupertinoAccessibility.TextScaleFactor;
        try
        {
            CupertinoAccessibility.TextScaleFactor = 1.5;
            var theme = new CupertinoTheme();
            Assert.Equal(25.5, Assert.IsType<double>(theme.Resources["CupertinoFontSize17"]), 3);

            CupertinoAccessibility.TextScaleFactor = 2;
            Assert.Equal(34, Assert.IsType<double>(theme.Resources["CupertinoFontSize17"]), 3);
        }
        finally
        {
            CupertinoAccessibility.TextScaleFactor = old;
        }
    }

    [AvaloniaTheory]
    [InlineData(DayOfWeek.Monday, 4)]
    [InlineData(DayOfWeek.Sunday, 3)]
    public void Rtl_calendar_mirrors_visual_columns_and_hit_testing(DayOfWeek firstDayOfWeek, int column)
    {
        var grid = new CupertinoMonthGrid
        {
            Width = 298,
            DisplayMonth = new DateTime(2026, 7, 1),
            FirstDayOfWeek = firstDayOfWeek,
            FlowDirection = FlowDirection.RightToLeft,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
        };
        var window = new Window { Width = 330, Height = 360, Content = grid };
        window.Show();
        window.UpdateLayout();

        var cell = 298 / 7.0;
        var weekday = cell * 0.314;
        DateTime? picked = null;
        grid.DayPicked += (_, date) => picked = date;
        grid.RaiseEvent(new PointerReleasedEventArgs(
            grid, new Pointer(8, PointerType.Mouse, true), grid,
            new Point(cell * (column + .5), weekday + cell * .5), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));

        Assert.Equal(new DateTime(2026, 7, 1), picked);
    }

    [AvaloniaTheory]
    [InlineData("Light", "LeftToRight", 1.0)]
    [InlineData("Dark", "LeftToRight", 1.35)]
    [InlineData("Light", "RightToLeft", 1.35)]
    [InlineData("Dark", "RightToLeft", 2.0)]
    public void New_controls_layout_across_theme_direction_and_scale(
        string themeName, string directionName, double scale)
    {
        var old = CupertinoAccessibility.TextScaleFactor;
        try
        {
            CupertinoAccessibility.TextScaleFactor = scale;
            var panel = new StackPanel
            {
                FlowDirection = directionName == "RightToLeft" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                Children =
                {
                    new CupertinoListCell { Title = "Title", Subtitle = "Secondary", AccessoryKind = CupertinoListAccessory.Disclosure },
                    new CupertinoFormRow { Label = "Name", Content = new TextBox { Text = "Value" } },
                    new CupertinoPageControl { NumberOfPages = 4, CurrentPage = 1 },
                    new CupertinoSearchView { ItemsSource = new[] { "One", "Two" }, Text = "o" },
                    new CupertinoDateTimePicker { SelectedDateTime = DateTimeOffset.Now },
                },
            };
            var window = new Window
            {
                Width = 430,
                Height = 700,
                RequestedThemeVariant = themeName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = panel,
            };
            window.Show();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.All(panel.Children, child => Assert.True(child.Bounds.Width > 0 && child.Bounds.Height > 0,
                $"{child.GetType().Name} laid out empty in {themeName}/{directionName}/{scale}"));
        }
        finally
        {
            CupertinoAccessibility.TextScaleFactor = old;
        }
    }
}

public class PickerCompletenessTests
{
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Time_picker_wheels_use_the_theme_foreground(string themeName)
    {
        var variant = themeName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var time = new CupertinoTimePicker
        {
            DisplayMode = CupertinoPickerDisplayMode.Inline,
            SelectedTime = new TimeSpan(10, 30, 0),
        };
        var window = new Window
        {
            Width = 400,
            Height = 300,
            RequestedThemeVariant = variant,
            Content = time,
        };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(window.TryFindResource("CupertinoLabelBrush", variant, out var resource));
        var expected = Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color;
        var wheels = time.GetVisualDescendants().OfType<CupertinoWheel>().ToList();

        Assert.True(wheels.Count >= 2);
        Assert.All(wheels, wheel =>
            Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(wheel.Foreground).Color));
    }

    [AvaloniaFact]
    public void Date_and_time_constraints_coerce_external_values()
    {
        var minimum = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var maximum = minimum.AddDays(5);
        var date = new CupertinoDatePicker { MinimumDate = minimum, MaximumDate = maximum, SelectedDate = minimum.AddDays(-3) };
        Assert.Equal(minimum.Date, date.SelectedDate?.Date);

        var time = new CupertinoTimePicker
        {
            MinimumTime = TimeSpan.FromHours(9),
            MaximumTime = TimeSpan.FromHours(17),
            SelectedTime = TimeSpan.FromHours(20),
        };
        Assert.Equal(TimeSpan.FromHours(17), time.SelectedTime);
    }

    [AvaloniaFact]
    public void Countdown_mode_supports_elapsed_hours_and_caps_duration()
    {
        var picker = new CupertinoTimePicker
        {
            Mode = CupertinoTimePickerMode.CountdownDuration,
            MaximumDuration = TimeSpan.FromHours(3),
            SelectedTime = TimeSpan.FromMinutes(250),
        };
        Assert.Equal(TimeSpan.FromHours(3), picker.SelectedTime);
        Assert.StartsWith("3 hr", picker.DisplayText);
    }

    [AvaloniaFact]
    public void Countdown_mode_normalizes_unsafe_ranges_before_building_wheels()
    {
        var picker = new CupertinoTimePicker
        {
            Mode = CupertinoTimePickerMode.CountdownDuration,
            MaximumDuration = TimeSpan.MaxValue,
            MinuteIncrement = 100,
            DisplayMode = CupertinoPickerDisplayMode.Inline,
        };
        var window = new Window { Width = 320, Height = 300, Content = picker };
        window.Show();
        window.UpdateLayout();

        Assert.Equal(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59), picker.MaximumDuration);
        Assert.Equal(59, picker.MinuteIncrement);
        var wheels = picker.GetVisualDescendants().OfType<CupertinoWheel>().ToList();
        Assert.Contains(wheels, wheel => wheel.Items?.Count == 24);
        Assert.Contains(wheels, wheel => wheel.Items?.SequenceEqual(new[] { "00", "59" }) == true);
    }

    [AvaloniaFact]
    public async Task Popover_animation_preserves_content_and_reverses_before_closing()
    {
        var reduceMotion = CupertinoAccessibility.ReduceMotion;
        var anchor = new Button { Width = 120, Height = 34 };
        var window = new Window { Width = 400, Height = 600, Content = anchor };
        window.Show();
        window.UpdateLayout();

        try
        {
            CupertinoAccessibility.ReduceMotion = false;
            CupertinoPopover.Show(anchor, new Border { Width = 330, Height = 300 }, 30, null);

            var overlay = OverlayLayer.GetOverlayLayer(anchor)!;
            var host = Assert.IsAssignableFrom<Panel>(overlay.Children[^1]);
            var panel = Assert.IsType<Grid>(host.Children[^1]);
            var glass = Assert.IsType<GlassSurface>(panel.Children[0]);
            var content = Assert.IsAssignableFrom<Border>(panel.Children[1]);
            var transform = glass.RenderTransform!.Value;

            Assert.Equal(anchor.Bounds.Width / panel.Bounds.Width, transform.M11, 3);
            Assert.Equal(anchor.Bounds.Height / panel.Bounds.Height, transform.M22, 3);
            Assert.NotEqual(0, transform.M31);
            Assert.NotEqual(0, transform.M32);
            Assert.False(content.RenderTransform!.Value.IsIdentity);
            Assert.Equal(0, content.Opacity);
            Assert.Equal(1, host.Opacity);
            Assert.Equal(1, anchor.Opacity);

            await Task.Delay(500);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(glass.RenderTransform!.Value.IsIdentity);
            Assert.True(content.RenderTransform!.Value.IsIdentity);
            Assert.Equal(1, content.Opacity);

            CupertinoPopover.Close(anchor);

            Assert.True(CupertinoPopover.IsOpen(anchor));
            Assert.Same(panel, host.Children[^1]);
            Assert.Equal(1, anchor.Opacity);

            await Task.Delay(300);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(CupertinoPopover.IsOpen(anchor));
            Assert.DoesNotContain(host, overlay.Children);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = true;
            CupertinoPopover.Close(anchor);
            CupertinoAccessibility.ReduceMotion = reduceMotion;
            window.Close();
        }
    }
}

public class NavigationLifecycleTests
{
    private static CupertinoNavigationPage Show()
    {
        var nav = new CupertinoNavigationPage { RootTitle = "Root", RootContent = new TextBlock { Text = "root" } };
        var window = new Window { Width = 402, Height = 600, Content = nav };
        window.Show();
        window.UpdateLayout();
        return nav;
    }

    [AvaloniaFact]
    public void Navigation_can_be_cancelled_and_state_round_trips()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var nav = Show();
            Assert.True(nav.TryPush("first", "First", new TextBlock(), 42));
            Assert.True(nav.TryPush("second", "Second", new TextBlock(), "payload"));
            Assert.Equal("second", nav.CurrentEntry?.Route);

            nav.Navigating += CancelPop;
            Assert.False(nav.TryPop());
            Assert.Equal(2, nav.Depth);
            nav.Navigating -= CancelPop;

            var state = nav.CaptureState();
            Assert.True(nav.TryPopToRoot());
            Assert.True(nav.RestoreState(state, saved => new TextBlock { Text = saved.Route }));
            Assert.Equal(new[] { "first", "second" }, nav.Stack.Select(entry => entry.Route));
            Assert.Equal("payload", nav.CurrentEntry?.Parameter);
            Assert.True(nav.PopToRoute("first"));
            Assert.Equal("first", nav.CurrentEntry?.Route);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = old;
        }

        static void CancelPop(object? sender, CupertinoNavigatingEventArgs args)
        {
            if (args.Kind == CupertinoNavigationKind.Pop)
                args.Cancel = true;
        }
    }

    [AvaloniaFact]
    public void Pop_to_root_without_root_content_does_not_clear_the_stack()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var nav = new CupertinoNavigationPage { RootTitle = "Root" };
            var window = new Window { Width = 402, Height = 600, Content = nav };
            window.Show();
            window.UpdateLayout();

            Assert.True(nav.TryPush("detail", "Detail", new TextBlock()));
            Assert.False(nav.TryPopToRoot());
            Assert.Equal(1, nav.Depth);
            Assert.Equal("detail", nav.CurrentEntry?.Route);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    [AvaloniaFact]
    public void Restoring_during_a_transition_completes_it_before_rebuilding_the_host()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        try
        {
            CupertinoAccessibility.ReduceMotion = true;
            var nav = Show();
            Assert.True(nav.TryPush("first", "First", new TextBlock()));
            var state = nav.CaptureState();

            var completed = new List<CupertinoNavigationKind>();
            nav.NavigationCompleted += (_, args) => completed.Add(args.Kind);

            CupertinoAccessibility.ReduceMotion = false;
            Assert.True(nav.TryPush("second", "Second", new TextBlock()));
            Assert.True(nav.RestoreState(state, saved => new TextBlock { Text = saved.Route }));
            Assert.Equal(new[] { CupertinoNavigationKind.Push, CupertinoNavigationKind.Restore }, completed);

            CupertinoAccessibility.ReduceMotion = true;
            Assert.True(nav.TryPush("third", "Third", new TextBlock()));
            Assert.Equal(new[]
            {
                CupertinoNavigationKind.Push,
                CupertinoNavigationKind.Restore,
                CupertinoNavigationKind.Push,
            }, completed);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    [AvaloniaFact]
    public void Pop_to_route_during_a_transition_does_not_leave_a_stale_completion()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        try
        {
            CupertinoAccessibility.ReduceMotion = true;
            var nav = Show();
            Assert.True(nav.TryPush("first", "First", new TextBlock()));
            Assert.True(nav.TryPush("second", "Second", new TextBlock()));

            var completed = new List<CupertinoNavigationKind>();
            nav.NavigationCompleted += (_, args) => completed.Add(args.Kind);

            CupertinoAccessibility.ReduceMotion = false;
            Assert.True(nav.TryPush("third", "Third", new TextBlock()));
            Assert.True(nav.PopToRoute("first"));
            Assert.Equal(new[] { CupertinoNavigationKind.Push, CupertinoNavigationKind.PopToRoute }, completed);

            CupertinoAccessibility.ReduceMotion = true;
            Assert.True(nav.TryPush("fourth", "Fourth", new TextBlock()));
            Assert.Equal(new[]
            {
                CupertinoNavigationKind.Push,
                CupertinoNavigationKind.PopToRoute,
                CupertinoNavigationKind.Push,
            }, completed);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = old;
        }
    }
}
