using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class ScrollBounceTests
{
    private static (Window Window, ScrollViewer Viewer) Show()
    {
        var viewer = new ScrollViewer { Content = new Border { Height = 2000 } };
        ScrollBounce.SetIsEnabled(viewer, true);
        var window = new Window { Width = 400, Height = 400, Content = viewer };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, viewer);
    }

    [AvaloniaFact]
    public void Wheel_past_the_top_stretches_and_reverse_unwinds()
    {
        var (window, viewer) = Show();

        window.MouseWheel(new Point(200, 200), new Vector(0, 1));
        var shift = Assert.IsType<TranslateTransform>(viewer.Presenter!.RenderTransform);
        Assert.True(shift.Y > 0, "a wheel past the top edge must stretch the content down");

        window.MouseWheel(new Point(200, 200), new Vector(0, -5));
        Assert.Equal(0, shift.Y, 3);
    }

    [AvaloniaFact]
    public void Mid_list_wheels_do_not_stretch()
    {
        var (window, viewer) = Show();
        viewer.Offset = new Vector(0, 500);
        window.UpdateLayout();

        window.MouseWheel(new Point(200, 200), new Vector(0, 1));

        var shift = viewer.Presenter!.RenderTransform as TranslateTransform;
        Assert.True(shift is null || shift.Y == 0);
    }

    [AvaloniaFact]
    public void Reduce_motion_leaves_the_edge_rigid()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var (window, viewer) = Show();
            window.MouseWheel(new Point(200, 200), new Vector(0, 1));

            var shift = viewer.Presenter!.RenderTransform as TranslateTransform;
            Assert.True(shift is null || shift.Y == 0);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void Existing_presenter_transform_is_restored_when_the_behavior_stops()
    {
        var (window, viewer) = Show();
        var original = new ScaleTransform(0.95, 0.95);
        viewer.Presenter!.RenderTransform = original;

        window.MouseWheel(new Point(200, 200), new Vector(0, 1));
        Assert.NotSame(original, viewer.Presenter.RenderTransform);

        ScrollBounce.SetIsEnabled(viewer, false);
        Assert.Same(original, viewer.Presenter.RenderTransform);
    }
}
