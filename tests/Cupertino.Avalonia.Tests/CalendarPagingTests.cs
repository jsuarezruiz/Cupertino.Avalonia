using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Point = Avalonia.Point;

namespace Cupertino.Avalonia.Tests;

public class CalendarPagingTests
{
    private static (Window Window, Calendar Calendar) Show()
    {
        var calendar = new Calendar { DisplayDate = new DateTime(2026, 8, 1) };
        var window = new Window { Width = 400, Height = 500, Content = calendar };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, calendar);
    }

    private static Button FindPart(Calendar calendar, string name) =>
        calendar.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Name == name);

    [AvaloniaFact]
    public void The_calendar_item_is_the_root_templates_direct_child()
    {
        var (_, calendar) = Show();
        var root = calendar.GetVisualDescendants().OfType<Panel>()
            .Single(panel => panel.Name == "PART_Root");

        Assert.IsType<CalendarItem>(root.Children[0]);
    }

    [AvaloniaFact]
    public void The_next_arrow_advances_the_month()
    {
        var (_, calendar) = Show();
        FindPart(calendar, "PART_NextButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(9, calendar.DisplayDate.Month);
    }

    [AvaloniaFact]
    public void The_next_arrow_advances_the_month_through_hit_testing()
    {
        var (window, calendar) = Show();
        var next = FindPart(calendar, "PART_NextButton");
        var centre = next.TranslatePoint(
            new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(9, calendar.DisplayDate.Month);
    }

    [AvaloniaFact]
    public void The_next_arrow_hit_tests_inside_a_scrolling_page()
    {
        var calendar = new Calendar { DisplayDate = new DateTime(2026, 8, 1) };
        var window = new Window
        {
            Width = 400,
            Height = 500,
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Children = { new Panel { Children = { calendar } } },
                },
            },
        };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var next = FindPart(calendar, "PART_NextButton");
        var centre = next.TranslatePoint(
            new Point(next.Bounds.Width / 2, next.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(9, calendar.DisplayDate.Month);
    }

    [AvaloniaFact]
    public void A_day_can_be_selected_through_hit_testing()
    {
        var (window, calendar) = Show();
        var expected = new DateTime(2026, 8, 12);
        var day = calendar.GetVisualDescendants().OfType<CalendarDayButton>()
            .Single(button => button.DataContext is DateTime date && date == expected);
        var centre = day.TranslatePoint(
            new Point(day.Bounds.Width / 2, day.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(expected, calendar.SelectedDate);
    }

    [AvaloniaFact]
    public void The_header_zooms_out_to_the_year_view()
    {
        var (_, calendar) = Show();
        FindPart(calendar, "PART_HeaderButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CalendarMode.Year, calendar.DisplayMode);
    }
}
