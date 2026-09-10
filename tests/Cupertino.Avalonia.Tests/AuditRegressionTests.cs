using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Cupertino.Controls;
using Cupertino.Themes;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class AuditRegressionTests
{
    private readonly Xunit.ITestOutputHelper _output;
    public AuditRegressionTests(Xunit.ITestOutputHelper output) => _output = output;
    private static Window Show(Control content)
    {
        var w = new Window { Width = 500, Height = 800, Content = content };
        w.Show(); w.UpdateLayout(); Dispatcher.UIThread.RunJobs(); return w;
    }

    [AvaloniaFact]
    public void Canceled_push_does_not_parent_the_rejected_page()
    {
        var nav = new CupertinoNavigationPage { RootContent = new Border() };
        var w = Show(nav);
        var page = new Border();
        EventHandler<CupertinoNavigatingEventArgs> cancel = (_, e) => e.Cancel = true;
        nav.Navigating += cancel;
        Assert.False(nav.TryPush("page", "Page", page));
        nav.Navigating -= cancel;
        try { Assert.True(nav.TryPush("page", "Page", page)); }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Popped_page_can_be_pushed_again()
    {
        CupertinoAccessibility.ReduceMotion = true;
        var nav = new CupertinoNavigationPage { RootContent = new Border() };
        var w = Show(nav); var page = new Border();
        try
        {
            Assert.True(nav.TryPush("page", "Page", page)); Assert.True(nav.TryPop());
            Assert.True(nav.TryPush("page", "Page", page));
        }
        finally { w.Close(); CupertinoAccessibility.ReduceMotion = false; }
    }

    [AvaloniaFact]
    public void Month_grid_remeasures_when_row_count_changes()
    {
        CupertinoAccessibility.ReduceMotion = true;
        var grid = new CupertinoMonthGrid { FirstDayOfWeek = DayOfWeek.Monday, DisplayMonth = new DateTime(2026, 2, 1), Width = 350 };
        var w = Show(new StackPanel { Children = { grid } });
        try
        {
            var oldHeight = grid.DesiredSize.Height;
            grid.DisplayMonth = new DateTime(2026, 3, 1);
            w.UpdateLayout();
            Assert.True(grid.DesiredSize.Height > oldHeight, $"Expected 6 rows instead of 5; before={oldHeight}, after={grid.DesiredSize.Height}");
        }
        finally { w.Close(); CupertinoAccessibility.ReduceMotion = false; }
    }

    [AvaloniaFact]
    public void Date_time_picker_accepts_last_supported_day()
    {
        var picker = new CupertinoDateTimePicker();
        picker.SelectedDateTime = DateTimeOffset.MaxValue;
        Assert.NotNull(picker.SelectedDateTime);
    }

    [AvaloniaFact]
    public void Date_and_time_fields_use_the_same_native_height()
    {
        var picker = new CupertinoDateTimePicker
        {
            SelectedDateTime = new DateTimeOffset(2026, 9, 10, 9, 41, 0, TimeSpan.Zero),
        };
        var window = Show(picker);
        try
        {
            var fields = picker.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Name == "PART_FlyoutButton").ToArray();
            Assert.Equal(2, fields.Length);
            Assert.All(fields, field => Assert.Equal(36, field.Bounds.Height));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Date_popup_tracks_selection_changed_while_open()
    {
        var picker = new CupertinoDatePicker { SelectedDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) };
        var w = Show(picker);
        try
        {
            picker.IsDropDownOpen = true; w.UpdateLayout();
            var calendar = OverlayLayer.GetOverlayLayer(picker)!.GetVisualDescendants().OfType<CupertinoCalendarView>().Single();
            picker.SelectedDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.Equal(picker.SelectedDate, calendar.SelectedDate);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Initially_open_picker_opens_after_template_is_applied()
    {
        var picker = new CupertinoDatePicker { IsDropDownOpen = true };
        var w = Show(picker);
        try { Assert.Contains(w.GetVisualDescendants(), c => c is CupertinoCalendarView && c.IsVisible); }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Replacing_dialog_actions_updates_buttons()
    {
        var dialog = new Dialog { Actions = new[] { new DialogAction("Before") } };
        var w = Show(dialog);
        try
        {
            var actions = new[] { new DialogAction("After") }; dialog.Actions = actions;
            var items = dialog.GetVisualDescendants().OfType<ItemsControl>().Single(c => c.Name == "PART_Actions");
            Assert.Same(actions, items.ItemsSource);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Sheet_moves_keyboard_focus_into_its_content()
    {
        CupertinoAccessibility.ReduceMotion = true;
        var behind = new TextBox { Text = "background" }; var w = Show(behind); behind.Focus();
        var inside = new TextBox { Text = "sheet" };
        try
        {
            _ = CupertinoSheet.ShowAsync(behind, inside); w.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.False(behind.IsFocused, "Keyboard focus remains in the obscured background TextBox");
        }
        finally { w.Close(); CupertinoAccessibility.ReduceMotion = false; }
    }

    private sealed class CountCommand : System.Windows.Input.ICommand
    {
        public int Calls;
        public bool Enabled = true;
        public object? Parameter;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? p) => Enabled;
        public void Execute(object? p) { Calls++; Parameter = p; }
    }

    private static void FullSwipe(Window w)
    {
        w.MouseDown(new Point(490, 20), MouseButton.Left);
        for (var i = 1; i <= 8; i++) w.MouseMove(new Point(490 - 480 * i / 8.0, 20));
        w.MouseUp(new Point(10, 20), MouseButton.Left); Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Full_swipe_executes_bound_command()
    {
        var command = new CountCommand();
        var action = new Button { Content = "Delete", Command = command };
        var swipe = new CupertinoSwipeView { Height = 44, TrailingActions = action, Content = new TextBlock { Text = "Row" } };
        var w = Show(new StackPanel { Children = { swipe } });
        try { FullSwipe(w); Assert.Equal(1, command.Calls); } finally { w.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 0)]
    public void Full_swipe_honors_command_parameters_eligibility_and_handled_clicks(bool canExecute, bool handleClick, int expected)
    {
        var parameter = new object();
        var command = new CountCommand { Enabled = canExecute };
        var action = new Button { Content = "Delete", Command = command, CommandParameter = parameter };
        action.Click += (_, e) => e.Handled = handleClick;
        var swipe = new CupertinoSwipeView { Height = 44, TrailingActions = action, Content = new TextBlock { Text = "Row" } };
        var window = Show(new StackPanel { Children = { swipe } });
        try
        {
            FullSwipe(window);
            Assert.Equal(expected, command.Calls);
            if (expected == 1)
                Assert.Same(parameter, command.Parameter);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Full_swipe_does_not_invoke_a_disabled_action()
    {
        var calls = 0;
        var action = new Button { Content = "Delete", IsEnabled = false };
        action.Click += (_, _) => calls++;
        var swipe = new CupertinoSwipeView { Height = 44, TrailingActions = action, Content = new TextBlock { Text = "Row" } };
        var w = Show(new StackPanel { Children = { swipe } });
        try { FullSwipe(w); Assert.Equal(0, calls); } finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Renamed_list_cell_updates_its_automatic_accessible_name()
    {
        var cell = new CupertinoListCell { Title = "Before" };
        cell.Title = "After";
        Assert.Equal("After", AutomationProperties.GetName(cell));
    }

    [AvaloniaFact]
    public void Dialog_respects_reduce_motion()
    {
        CupertinoAccessibility.ReduceMotion = true;
        var anchor = new Button(); var w = Show(anchor); var dialog = new Dialog();
        try
        {
            _ = dialog.ShowAsync(anchor);
            Assert.True(dialog.Transitions is null || dialog.Transitions.All(t => (TimeSpan?)t.GetType().GetProperty("Duration")?.GetValue(t) == TimeSpan.Zero),
                "Dialog installs non-zero transitions while ReduceMotion=true");
        }
        finally { w.Close(); CupertinoAccessibility.ReduceMotion = false; }
    }

    [AvaloniaFact]
    public void Toolbar_can_reapply_its_template()
    {
        var toolbar = new CupertinoToolbar(); toolbar.Items.Add(new Button { Content = "Action" });
        var w = Show(toolbar);
        try
        {
            toolbar.Template = new FuncControlTemplate<CupertinoToolbar>((_, scope) =>
            {
                var grid = new Grid { Name = "PART_Groups" }; scope.Register("PART_Groups", grid); return grid;
            });
            toolbar.ApplyTemplate();
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void Search_filters_each_added_item_once()
    {
        var items = new System.Collections.ObjectModel.ObservableCollection<string>();
        long visits = 0;
        var search = new CupertinoSearchView { ItemsSource = items, Filter = (_, _, _) => { visits++; return false; } };
        var w = Show(search);
        try
        {
            visits = 0;
            for (var i = 0; i < 1000; i++) items.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _output.WriteLine($"1000 additions caused {visits:N0} filter evaluations");
            Assert.InRange(visits, 0, 1000);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact]
    public void External_wheel_selection_survives_an_active_settle()
    {
        CupertinoAccessibility.ReduceMotion = false;
        var wheel = new CupertinoWheel { ShouldLoop = false, Items = Enumerable.Range(0, 10).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList() };
        var w = Show(wheel);
        try
        {
            wheel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            Assert.Equal(1, wheel.SelectedIndex);
            wheel.SelectedIndex = 5;
            var tick = typeof(CupertinoWheel).GetMethod("OnSettleTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            for (var i = 0; i < 300; i++) tick.Invoke(wheel, new object?[] { null, EventArgs.Empty });
            Assert.Equal(5, wheel.SelectedIndex);
        }
        finally { w.Close(); }
    }
}
