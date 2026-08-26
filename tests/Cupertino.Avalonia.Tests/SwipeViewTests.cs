using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class SwipeViewTests
{
    private static (Window Window, CupertinoSwipeView Swipe, Button Delete) Show()
    {
        var delete = new Button { Content = "Delete", Classes = { "swipe", "swipe-destructive" } };
        var swipe = new CupertinoSwipeView
        {
            Height = 44,
            TrailingActions = delete,
            Content = new TextBlock { Text = "row" },
        };
        var window = new Window { Width = 320, Height = 200, Content = new StackPanel { Children = { swipe } } };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, swipe, delete);
    }

    private static void Drag(
        Window window,
        double fromX,
        double toX,
        double y = 20,
        int moveDelayMilliseconds = 0)
    {
        window.MouseDown(new Point(fromX, y), MouseButton.Left);
        var steps = 8;
        for (var i = 1; i <= steps; i++)
        {
            if (moveDelayMilliseconds > 0)
                Thread.Sleep(moveDelayMilliseconds);
            window.MouseMove(new Point(fromX + (toX - fromX) * i / steps, y));
        }
        window.MouseUp(new Point(toX, y), MouseButton.Left);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void A_short_drag_left_springs_shut()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, _) = Show();
            Drag(window, 250, 230);
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.Equal(0, shift.X, 3);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_half_drag_opens_to_the_actions_width()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            Drag(window, 280, 170);
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.True(shift.X < 0, $"expected the row open to the left, got {shift.X}");
            Assert.Equal(-(delete.Bounds.Width + 8), shift.X, 1);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void Repeated_overdrags_do_not_increase_the_resting_action_width()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, _) = Show();
            Drag(window, 280, 190);
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            var firstRestingPosition = shift.X;

            swipe.Close();
            Drag(window, 280, 130);

            Assert.Equal(firstRestingPosition, shift.X, 1);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void Actions_can_be_opened_programmatically()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            swipe.OpenTrailingActions();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.Equal(-(delete.Bounds.Width + 8), shift.X, 1);

            swipe.Close();
            Assert.Equal(0, shift.X, 3);

            var read = new Button { Content = "Read", Classes = { "swipe" } };
            swipe.LeadingActions = read;
            swipe.OpenLeadingActions();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(read.Bounds.Width + 8, shift.X, 1);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void The_badge_caps_at_99_and_dots_below_zero()
    {
        var badge = new CupertinoBadge { Value = 120 };
        Assert.Equal("99+", badge.Text);
        Assert.DoesNotContain(":dot", badge.Classes);

        badge.Value = 3;
        Assert.Equal("3", badge.Text);

        badge.Value = -1;
        Assert.Equal("", badge.Text);
        Assert.Contains(":dot", badge.Classes);
    }

    [AvaloniaFact]
    public void A_full_swipe_fires_the_outermost_action_and_shuts()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            var fired = false;
            delete.Click += (_, _) => fired = true;

            Drag(window, 310, 10);

            Assert.True(fired, "the full swipe must raise the outermost action's Click");
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.Equal(0, shift.X, 3);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_fast_partial_swipe_reveals_actions_without_committing()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            var fired = false;
            delete.Click += (_, _) => fired = true;

            Drag(window, 310, 130, moveDelayMilliseconds: 2);

            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.False(fired);
            Assert.Equal(-(delete.Bounds.Width + 8), shift.X, 1);
            window.Close();
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_fast_short_swipe_uses_release_velocity_to_reveal_actions()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            swipe.OpenTrailingActions();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            var reach = Math.Abs(shift.X);
            swipe.Close();

            Drag(window, 310, 310 - reach * 0.35, moveDelayMilliseconds: 2);

            Assert.Equal(-reach, shift.X, 1);
            window.Close();
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_slow_short_swipe_returns_to_the_closed_position()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            swipe.OpenTrailingActions();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            var reach = Math.Abs(shift.X);
            swipe.Close();

            Drag(window, 310, 310 - reach * 0.35, moveDelayMilliseconds: 75);

            Assert.Equal(0, shift.X, 3);
            window.Close();
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void A_deep_reveal_keeps_the_action_at_its_natural_width()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            swipe.OpenTrailingActions();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var naturalWidth = delete.Bounds.Width;
            swipe.Close();

            window.MouseDown(new Point(310, 20), MouseButton.Left);
            window.MouseMove(new Point(70, 20));
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var trailing = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Trailing");
            Assert.InRange(trailing.Bounds.Width, naturalWidth, naturalWidth + 10);
            Assert.Equal(naturalWidth, delete.Bounds.Width, 1);
            Assert.False(delete.IsSet(Button.WidthProperty));

            window.MouseUp(new Point(70, 20), MouseButton.Left);
            window.Close();
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public async Task A_fast_close_does_not_overshoot_past_the_closed_position()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var (window, swipe, _) = Show();
        try
        {
            swipe.OpenTrailingActions();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            var openPosition = shift.X;

            CupertinoAccessibility.ReduceMotion = false;
            Drag(window, 100, 100 + Math.Abs(openPosition) * 0.7, moveDelayMilliseconds: 2);

            for (var i = 0; i < 25; i++)
            {
                await Task.Delay(20);
                global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(shift.X <= 0.5, $"the closing row crossed to {shift.X:F2}");
            }

            Assert.Equal(0, shift.X, 1);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = true;
            swipe.Close();
            CupertinoAccessibility.ReduceMotion = old;
            window.Close();
        }
    }
}
