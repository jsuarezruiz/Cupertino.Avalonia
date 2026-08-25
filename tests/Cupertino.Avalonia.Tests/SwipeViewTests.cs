using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
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
    private sealed class SwipeStateSource
    {
        public SwipeViewState State { get; set; }
    }

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

    private static void Drag(Window window, double fromX, double toX, double y = 20)
    {
        window.MouseDown(new Point(fromX, y), MouseButton.Left);
        var steps = 8;
        for (var i = 1; i <= steps; i++)
            window.MouseMove(new Point(fromX + (toX - fromX) * i / steps, y));
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
            Assert.Equal(SwipeViewState.Closed, swipe.SwipeState);
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
            Assert.Equal(-delete.Bounds.Width, shift.X, 1);
            Assert.Equal(SwipeViewState.TrailingVisible, swipe.SwipeState);
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
    public void Swipe_state_can_be_set_programmatically_for_representative_states()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, swipe, delete) = Show();
            swipe.SwipeState = SwipeViewState.TrailingVisible;
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.Equal(-delete.Bounds.Width, shift.X, 1);
            Assert.Equal(SwipeViewState.TrailingVisible, swipe.SwipeState);

            swipe.Close();
            Assert.Equal(0, shift.X, 3);
            Assert.Equal(SwipeViewState.Closed, swipe.SwipeState);

            var read = new Button { Content = "Read", Classes = { "swipe" } };
            swipe.LeadingActions = read;
            swipe.SwipeState = SwipeViewState.LeadingVisible;
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(read.Bounds.Width, shift.X, 1);
            Assert.Equal(SwipeViewState.LeadingVisible, swipe.SwipeState);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void Detaching_does_not_overwrite_a_bound_swipe_state()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var source = new SwipeStateSource { State = SwipeViewState.TrailingVisible };
            var swipe = new CupertinoSwipeView
            {
                Height = 44,
                TrailingActions = new Button { Content = "Delete", Classes = { "swipe" } },
                Content = new TextBlock { Text = "row" },
            };
            swipe.Bind(CupertinoSwipeView.SwipeStateProperty, new Binding(nameof(SwipeStateSource.State))
            {
                Source = source,
                Mode = BindingMode.TwoWay,
            });
            var window = new Window { Width = 320, Height = 200, Content = swipe };
            window.Show();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            window.Content = null;
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(SwipeViewState.TrailingVisible, source.State);
            Assert.Equal(SwipeViewState.TrailingVisible, swipe.SwipeState);

            window.Content = swipe;
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var content = swipe.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(p => p.Name == "PART_Content");
            var shift = Assert.IsType<TranslateTransform>(content.RenderTransform);
            Assert.True(shift.X < 0);
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

            Drag(window, 300, 40);

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
}
