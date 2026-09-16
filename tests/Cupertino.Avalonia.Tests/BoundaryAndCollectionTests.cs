using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class BoundaryAndCollectionTests
{
    [AvaloniaTheory]
    [InlineData(1750, CupertinoPickerDisplayMode.Compact)]
    [InlineData(2400, CupertinoPickerDisplayMode.Compact)]
    [InlineData(1750, CupertinoPickerDisplayMode.Inline)]
    [InlineData(2400, CupertinoPickerDisplayMode.Inline)]
    public void Date_picker_template_preserves_dates_outside_the_default_calendar_year_range(int year, CupertinoPickerDisplayMode mode)
    {
        var selected = new DateTimeOffset(year, 9, 5, 0, 0, 0, TimeSpan.Zero);
        var picker = new CupertinoDatePicker { SelectedDate = selected, DisplayMode = mode };
        var window = new Window { Width = 400, Height = 400, Content = picker };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.Equal(selected, picker.SelectedDate);
            var calendar = picker.GetVisualDescendants().OfType<CupertinoCalendarView>().Single();
            Assert.Equal(selected, calendar.SelectedDate);
            calendar.SelectedDate = selected.AddDays(1);
            Assert.Equal(selected.AddDays(1), picker.SelectedDate);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Popover_takes_keyboard_focus_and_escape_restores_the_anchor()
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var anchor = new Button { Content = "Open" };
        var first = new TextBox();
        var last = new Button { Content = "Last" };
        var content = new StackPanel { Children = { first, last } };
        var window = new Window { Width = 400, Height = 400, Content = anchor };
        try
        {
            window.Show();
            window.UpdateLayout();
            anchor.Focus();
            CupertinoPopover.Show(anchor, content, 24, null);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.IsFocused);
            last.Focus();
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.True(first.IsFocused);
            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
            Assert.True(last.IsFocused);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.False(CupertinoPopover.IsOpen(anchor));
            Assert.True(anchor.IsFocused);
            Assert.Null(content.Parent);
        }
        finally
        {
            CupertinoPopover.Close(anchor);
            window.Close();
            CupertinoAccessibility.ReduceMotion = previousMotion;
        }
    }

    [AvaloniaFact]
    public void Open_popover_repositions_when_its_content_height_changes()
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var anchor = new Button { Width = 80, Height = 36 };
        Canvas.SetLeft(anchor, 200);
        Canvas.SetTop(anchor, 700);
        var canvas = new Canvas { Children = { anchor } };
        var content = new Border { Width = 160, Height = 100 };
        var window = new Window { Width = 500, Height = 800, Content = canvas };
        try
        {
            window.Show();
            window.UpdateLayout();
            CupertinoPopover.Show(anchor, content, 20, null);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var layer = OverlayLayer.GetOverlayLayer(anchor)!;
            var glass = layer.GetVisualDescendants().OfType<GlassSurface>().Single();
            var panel = Assert.IsAssignableFrom<Control>(glass.GetVisualParent());
            var initialHeight = panel.Bounds.Height;

            content.Height = 260;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var panelOrigin = panel.TranslatePoint(default, window)!.Value;
            var anchorOrigin = anchor.TranslatePoint(default, window)!.Value;
            Assert.True(panel.Bounds.Height > initialHeight);
            Assert.True(panelOrigin.Y + panel.Bounds.Height <= anchorOrigin.Y - 7);
        }
        finally
        {
            CupertinoPopover.CloseImmediately(anchor);
            window.Close();
            CupertinoAccessibility.ReduceMotion = previousMotion;
        }
    }

    [Fact]
    public void Disabled_page_control_rejects_automation_value_changes()
    {
        var control = new CupertinoPageControl
        {
            NumberOfPages = 5,
            CurrentPage = 1,
            IsEnabled = false,
        };
        var peer = new CupertinoPageControlAutomationPeer(control);

        peer.SetValue(4);

        Assert.True(peer.IsReadOnly);
        Assert.Equal(1, control.CurrentPage);
    }

    [AvaloniaFact]
    public void Reattached_switch_observes_the_current_motion_preference()
    {
        var previousMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = false;
        var control = new ToggleSwitch();
        var window = new Window { Width = 400, Height = 400, Content = control };
        try
        {
            window.Show();
            window.UpdateLayout();
            window.Content = null;
            CupertinoAccessibility.ReduceMotion = true;
            window.Content = control;
            window.UpdateLayout();
            var knobs = control.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "PART_MovingKnobs");
            Assert.Null(knobs.Transitions);
            CupertinoAccessibility.ReduceMotion = false;
            Assert.NotNull(knobs.Transitions);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = previousMotion;
        }
    }

    [AvaloniaFact]
    public async Task Detached_switch_is_not_retained_by_accessibility_preferences()
    {
        var reference = CreateDetachedSwitch();
        // Flush callbacks already queued by the glass and compositor before detachment.
        await Task.Delay(450);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 3 && reference.IsAlive; ++i)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(reference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDetachedSwitch()
    {
        var control = new ToggleSwitch();
        var window = new Window { Width = 400, Height = 400, Content = control };
        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            return new WeakReference(control);
        }
        finally
        {
            window.Close();
            window.Content = null;
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 30, 59)]
    [InlineData(false, 345, 59)]
    [InlineData(true, -30, 59)]
    [InlineData(true, -345, 59)]
    public void Minute_rounding_respects_offset_adjusted_date_time_limits(bool maximum, int offsetMinutes, int increment)
    {
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var value = (maximum ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue).ToOffset(offset);
        var picker = new CupertinoDateTimePicker { MinuteIncrement = increment, SelectedDateTime = value };
        Assert.NotNull(picker.SelectedDateTime);
        Assert.Equal(offset, picker.SelectedDateTime.Value.Offset);
        Assert.InRange(picker.SelectedDateTime.Value, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        Assert.Equal(0, picker.SelectedDateTime.Value.Minute % increment);
    }

    [AvaloniaFact]
    public void Moving_a_selected_search_result_preserves_selection()
    {
        var items = new ObservableCollection<string> { "alpha", "beta", "gamma" };
        var search = new CupertinoSearchView { ItemsSource = items, SelectedItem = "beta" };
        var window = new Window { Width = 400, Height = 400, Content = search };
        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("beta", search.SelectedItem);
            items.Move(1, 2);
            Assert.Equal(items, search.FilteredItems.Cast<string>());
            Assert.Equal("beta", search.SelectedItem);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public void Moving_a_search_result_range_preserves_selection_without_refiltering(int oldIndex, int newIndex)
    {
        var items = new RangeCollection { "match one", "skip", "match two", "match three" };
        var calls = 0;
        var search = new CupertinoSearchView
        {
            ItemsSource = items,
            Filter = (item, _, _) => { ++calls; return ((string)item).StartsWith("match", StringComparison.Ordinal); },
            SelectedItem = "match two",
        };
        var window = new Window { Width = 400, Height = 400, Content = search };
        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var before = calls;
            items.MoveRange(oldIndex, newIndex, 2);
            Assert.Equal(items.Where(i => i.StartsWith("match", StringComparison.Ordinal)), search.FilteredItems.Cast<string>());
            Assert.Equal("match two", search.SelectedItem);
            Assert.Equal(before, calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Date_time_limits_are_compared_in_the_selected_offset()
    {
        var picker = new CupertinoDateTimePicker
        {
            Minimum = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero),
            SelectedDateTime = new DateTimeOffset(2026, 9, 5, 6, 0, 0, TimeSpan.FromHours(-4)),
        };
        Assert.Equal(6, picker.SelectedDateTime!.Value.Hour);
        Assert.Equal(TimeSpan.FromHours(-4), picker.SelectedDateTime.Value.Offset);
    }

    [AvaloniaFact]
    public void Date_part_uses_the_selected_local_day_for_absolute_bounds()
    {
        var selected = new DateTimeOffset(2026, 9, 4, 22, 0, 0, TimeSpan.FromHours(-4));
        var picker = new CupertinoDateTimePicker
        {
            Minimum = new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero),
            SelectedDateTime = selected,
        };
        var window = new Window { Width = 400, Height = 400, Content = picker };
        try
        {
            window.Show();
            window.UpdateLayout();
            var date = picker.GetVisualDescendants().OfType<CupertinoDatePicker>().Single();
            Assert.Equal(selected.Date, date.MinimumDate!.Value.Date);
            Assert.Equal(selected, date.SelectedDate);
            Assert.Equal(selected, picker.SelectedDateTime);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Time_part_cannot_select_a_time_outside_the_offset_adjusted_last_day()
    {
        var picker = new CupertinoDateTimePicker
        {
            MinuteIncrement = 59,
            SelectedDateTime = DateTimeOffset.MaxValue.ToOffset(TimeSpan.FromMinutes(-30)),
        };
        var window = new Window { Width = 400, Height = 400, Content = picker };
        try
        {
            window.Show();
            window.UpdateLayout();
            var time = picker.GetVisualDescendants().OfType<CupertinoTimePicker>().Single();
            time.SelectedTime = new TimeSpan(23, 59, 0);
            Assert.Equal(new TimeSpan(23, 0, 0), picker.SelectedDateTime!.Value.TimeOfDay);
            Assert.Equal(TimeSpan.FromMinutes(-30), picker.SelectedDateTime.Value.Offset);
        }
        finally { window.Close(); }
    }

    private sealed class RangeCollection : ObservableCollection<string>
    {
        public void MoveRange(int oldIndex, int newIndex, int count)
        {
            var moved = this.Skip(oldIndex).Take(count).ToList();
            for (var i = 0; i < count; i++)
                Items.RemoveAt(oldIndex);
            for (var i = 0; i < count; i++)
                Items.Insert(newIndex + i, moved[i]);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, moved, newIndex, oldIndex));
        }
    }
}

public class DatePickerFormatTests
{
    [Theory]
    [InlineData("en-US", "Aug 3, 2026")]
    [InlineData("en-GB", "3 Aug 2026")]
    [InlineData("es-ES", "3 ago 2026")]
    [InlineData("ja-JP", "2026 8月 3")]
    public void Default_date_format_follows_the_culture_field_order(string culture, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var picker = new CupertinoDatePicker
            {
                SelectedDate = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
            };
            Assert.Equal(expected, picker.DisplayText);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [AvaloniaFact]
    public void Explicit_date_format_overrides_the_culture_default()
    {
        var picker = new CupertinoDatePicker
        {
            SelectedDate = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
            DateFormat = "yyyy-MM-dd",
        };
        Assert.Equal("2026-08-03", picker.DisplayText);
    }
}
