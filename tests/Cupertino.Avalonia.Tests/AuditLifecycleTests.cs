using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class AuditLifecycleTests
{
    private static Window Show(Control content)
    {
        var window = new Window { Width = 500, Height = 800, Content = content };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaTheory]
    [InlineData("root")]
    [InlineData("route")]
    [InlineData("restore")]
    public void Removing_multiple_routes_releases_only_retired_pages(string operation)
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var root = new Border();
        var retained = new Border();
        var retired = new[] { new Border(), new Border() };
        var nav = new CupertinoNavigationPage { RootContent = root };
        var window = Show(nav);
        try
        {
            nav.Push("retained", retained);
            nav.Push("one", retired[0]);
            nav.Push("two", retired[1]);
            if (operation == "route")
                Assert.True(nav.PopToRoute("retained"));
            else if (operation == "restore")
                Assert.True(nav.RestoreState(new CupertinoNavigationState([]), _ => null));
            else
                Assert.True(nav.TryPopToRoot());
            foreach (var page in retired)
            {
                Assert.Null(page.Parent);
                Assert.True(nav.TryPush("reuse", "Reuse", page));
                Assert.True(nav.TryPop());
            }
            Assert.NotNull(root.Parent);
            Assert.Equal(operation == "route", retained.Parent is not null);
        }
        finally { window.Close(); CupertinoAccessibility.ReduceMotion = previousMotion; }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Abandoned_restore_releases_factory_pages(bool throwFromFactory)
    {
        var nav = new CupertinoNavigationPage { RootContent = new Border() };
        var window = Show(nav);
        var page = new Border();
        var state = new CupertinoNavigationState([
            new("one", "One", null), new("two", "Two", null)]);
        Control? Factory(CupertinoNavigationStateEntry entry) => entry.Route == "one" ? page :
            throwFromFactory ? throw new InvalidOperationException("Factory failed") : null;
        try
        {
            if (throwFromFactory)
                Assert.Throws<InvalidOperationException>(() => nav.RestoreState(state, Factory));
            else
                Assert.False(nav.RestoreState(state, Factory));
            Assert.Null(page.Parent);
            Assert.Empty(nav.Stack);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Replacing_root_releases_the_previous_root()
    {
        var root = new Border();
        var nav = new CupertinoNavigationPage { RootContent = root };
        var window = Show(nav);
        try
        {
            nav.Push("page", new Border());
            nav.RootContent = new Border();
            Assert.Null(root.Parent);
            Assert.True(nav.TryPush("reuse", "Reuse", root));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Sheet_cycles_focus_dismisses_with_escape_and_restores_focus()
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var behind = new TextBox();
        var window = Show(behind);
        behind.Focus();
        var first = new TextBox();
        var last = new Button { Content = "Last" };
        var content = new StackPanel { Children = { first, last } };
        try
        {
            var completion = CupertinoSheet.ShowAsync(behind, content);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.IsFocused);
            last.Focus();
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.True(first.IsFocused);
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            Assert.True(last.IsFocused);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.True(completion.IsCompletedSuccessfully);
            Assert.True(behind.IsFocused);
            Assert.Null(content.Parent);
        }
        finally { window.Close(); CupertinoAccessibility.ReduceMotion = previousMotion; }
    }

    [AvaloniaFact]
    public void Date_popup_tracks_bounds_weekday_and_rapid_reopen()
    {
        var picker = new CupertinoDatePicker { SelectedDate = new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero) };
        var window = Show(picker);
        try
        {
            picker.IsDropDownOpen = true;
            var calendar = OverlayLayer.GetOverlayLayer(picker)!.GetVisualDescendants().OfType<CupertinoCalendarView>().Single();
            picker.MinimumDate = picker.SelectedDate;
            picker.MaximumDate = picker.SelectedDate.Value.AddDays(10);
            picker.FirstDayOfWeek = DayOfWeek.Wednesday;
            Assert.Equal(picker.MinimumDate, calendar.MinimumDate);
            Assert.Equal(picker.MaximumDate, calendar.MaximumDate);
            Assert.Equal(DayOfWeek.Wednesday, calendar.FirstDayOfWeek);
            picker.IsDropDownOpen = false;
            picker.IsDropDownOpen = true;
            Assert.Single(OverlayLayer.GetOverlayLayer(picker)!.GetVisualDescendants().OfType<CupertinoCalendarView>());
            Assert.True(picker.IsDropDownOpen);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Initially_open_time_picker_rebuilds_visible_wheels_when_configuration_changes()
    {
        var picker = new CupertinoTimePicker { IsDropDownOpen = true, ClockIdentifier = "12HourClock" };
        var window = Show(picker);
        try
        {
            var layer = OverlayLayer.GetOverlayLayer(picker)!;
            Assert.Equal(3, layer.GetVisualDescendants().OfType<CupertinoWheel>().Count());
            picker.MinuteIncrement = 15;
            picker.ClockIdentifier = "24HourClock";
            window.UpdateLayout();
            var visible = layer.GetVisualDescendants().OfType<CupertinoWheel>().Where(w => w.IsVisible).ToArray();
            Assert.Equal(2, visible.Length);
            Assert.Contains(visible, w => w.Items!.Count == 24);
            Assert.Contains(visible, w => w.Items!.SequenceEqual(new[] { "00", "15", "30", "45" }));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Incremental_search_preserves_order_duplicates_and_selection()
    {
        var items = new ObservableCollection<string?> { "a", null, "b", "a", "c" };
        var search = new CupertinoSearchView { ItemsSource = items, Text = "a", SelectedItem = "a" };
        var window = Show(search);
        void Check() => Assert.Equal(items.Where(i => i?.Contains('a', StringComparison.Ordinal) == true).Cast<object>(), search.FilteredItems);
        try
        {
            Check();
            items.Insert(1, "aa"); Check();
            items.Move(4, 0); Check();
            items[2] = "ba"; Check();
            items.RemoveAt(0); Check();
            Assert.Equal("a", search.SelectedItem);
            items.Clear(); Check();
            items.Add("after reset"); Check();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Explicit_accessible_name_survives_title_updates_and_can_be_cleared()
    {
        var cell = new CupertinoListCell { Title = "First" };
        AutomationProperties.SetName(cell, "Custom");
        cell.Title = "Second";
        Assert.Equal("Custom", AutomationProperties.GetName(cell));
        cell.ClearValue(AutomationProperties.NameProperty);
        Assert.Equal("Second", AutomationProperties.GetName(cell));
    }

    [AvaloniaFact]
    public void Attached_button_transitions_follow_reduce_motion_changes()
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = false;
        var button = new Button { Content = "Button" };
        var window = Show(button);
        try
        {
            Assert.Contains(button.Transitions!, t => ((TransitionBase)t).Duration > TimeSpan.Zero);
            CupertinoAccessibility.ReduceMotion = true;
            Dispatcher.UIThread.RunJobs();
            Assert.All(button.Transitions!, t => Assert.Equal(TimeSpan.Zero, ((TransitionBase)t).Duration));
            CupertinoAccessibility.ReduceMotion = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(button.Transitions!, t => ((TransitionBase)t).Duration > TimeSpan.Zero);
        }
        finally { window.Close(); CupertinoAccessibility.ReduceMotion = previousMotion; }
    }

    [AvaloniaFact]
    public void Glass_coerces_nonfinite_and_excessive_sampling_values()
    {
        var glass = new GlassSurface
        {
            BlurRadius = double.PositiveInfinity,
            RefractionStrength = double.MaxValue,
            ShadowBlur = double.NaN,
            ShadowOpacity = 10,
            Magnification = -1,
        };
        Assert.Equal(20, glass.BlurRadius);
        Assert.Equal(256, glass.RefractionStrength);
        Assert.Equal(14, glass.ShadowBlur);
        Assert.Equal(1, glass.ShadowOpacity);
        Assert.Equal(0.01, glass.Magnification);
    }

    [AvaloniaTheory]
    [InlineData(1, 1, 1, -1)]
    [InlineData(9999, 12, 31, 1)]
    public void Date_time_picker_accepts_offset_extremes(int year, int month, int day, int offsetHours)
    {
        var value = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.FromHours(offsetHours));
        var picker = new CupertinoDateTimePicker { SelectedDateTime = value };
        Assert.Equal(value, picker.SelectedDateTime);
    }
}
