using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

// An interrupted gesture (pointer cancel, capture taken elsewhere) must reset like a release.
public class CaptureLossTests
{
    private static Func<IPointer?> TrackPointer(Window window)
    {
        IPointer? pointer = null;
        window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        return () => pointer;
    }

    private static Window Show(Control content, double width = 400, double height = 600)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Navigation_back_swipe_ends_when_capture_is_lost()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var nav = new CupertinoNavigationPage { RootTitle = "Root", RootContent = new Border() };
        var window = Show(nav);
        try
        {
            var pointer = TrackPointer(window);
            nav.Push("Detail", new Border { Background = Brushes.White });
            Dispatcher.UIThread.RunJobs();

            window.MouseDown(new Point(6, 300), MouseButton.Left);
            window.MouseMove(new Point(120, 300));
            pointer()!.Capture(window);
            Dispatcher.UIThread.RunJobs();

            Assert.True(nav.TryPush("Next", "Next", new Border()));
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    [AvaloniaFact]
    public void Tab_bar_drag_resets_when_capture_is_lost()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var tabs = new TabControl { Classes = { "bottom" } };
        foreach (var header in new[] { "One", "Two", "Three" })
            tabs.Items.Add(new TabItem { Header = header, Content = new Border() });
        var window = Show(tabs);
        try
        {
            var pointer = TrackPointer(window);
            var item = tabs.GetVisualDescendants().OfType<TabItem>().First();
            var indicator = tabs.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PART_Indicator");
            var start = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(20, 0));
            window.MouseMove(start + new Point(40, 0));
            Assert.True(indicator.IsVisible);

            pointer()!.Capture(window);

            Assert.False(indicator.IsVisible);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    [AvaloniaFact]
    public void Sheet_drag_settles_when_capture_is_lost()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var window = Show(new Border { Background = Brushes.White }, 400, 800);
        try
        {
            var pointer = TrackPointer(window);
            _ = CupertinoSheet.ShowAsync(window, new Border { Height = 600 }, SheetDetents.Large);
            Dispatcher.UIThread.RunJobs();
            var presenter = window.GetVisualDescendants().OfType<CupertinoSheetPresenter>().Single();
            var translate = (TranslateTransform)presenter.RenderTransform!;
            var top = presenter.TranslatePoint(new Point(200, 10), window)!.Value;

            window.MouseDown(top, MouseButton.Left);
            window.MouseMove(top + new Point(0, 40));
            window.MouseMove(top + new Point(0, 80));
            pointer()!.Capture(null);
            Dispatcher.UIThread.RunJobs();
            var settled = translate.Y;

            window.MouseMove(top + new Point(0, 200));

            Assert.Equal(settled, translate.Y);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    [AvaloniaFact]
    public async Task Grabbing_a_dismissing_sheet_does_not_stop_the_dismissal()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = false;
        var window = Show(new Border { Background = Brushes.White }, 400, 800);
        try
        {
            var task = CupertinoSheet.ShowAsync(window, new Border { Height = 600 }, SheetDetents.Large);
            await Pump(TimeSpan.FromMilliseconds(700), () => false);
            var presenter = window.GetVisualDescendants().OfType<CupertinoSheetPresenter>().Single();

            window.MouseDown(new Point(200, 3), MouseButton.Left);
            window.MouseUp(new Point(200, 3), MouseButton.Left);
            await Pump(TimeSpan.FromMilliseconds(60), () => false);
            var grabber = presenter.TranslatePoint(new Point(200, 10), window)!.Value;
            Assert.InRange(grabber.Y, 0, 790);
            window.MouseDown(grabber, MouseButton.Left);
            window.MouseMove(grabber + new Point(0, 20));
            window.MouseUp(grabber + new Point(0, 20), MouseButton.Left);

            await Pump(TimeSpan.FromSeconds(3), () => task.IsCompleted);
            Assert.True(task.IsCompleted);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    private static async Task Pump(TimeSpan duration, Func<bool> done)
    {
        var until = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < until && !done())
            await Task.Delay(10);
    }

    [AvaloniaFact]
    public void Slider_thumb_deactivates_when_capture_is_lost()
    {
        var slider = new Slider { Width = 300, Maximum = 100, Value = 50 };
        var window = Show(slider);
        try
        {
            var pointer = TrackPointer(window);
            var thumb = slider.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Thumb>().First();
            window.MouseDown(thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value,
                MouseButton.Left);
            Assert.Contains(SliderInteraction.ActiveClass, thumb.Classes);

            pointer()!.Capture(window);

            Assert.DoesNotContain(SliderInteraction.ActiveClass, thumb.Classes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Switch_stops_treating_changes_as_user_toggles_when_capture_is_lost()
    {
        var played = 0;
        var oldHandler = CupertinoHaptics.Handler;
        CupertinoHaptics.Handler = _ => played++;
        var toggle = new ToggleSwitch();
        var window = Show(toggle);
        try
        {
            var pointer = TrackPointer(window);
            window.MouseDown(toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value,
                MouseButton.Left);
            pointer()!.Capture(window);
            Dispatcher.UIThread.RunJobs();

            toggle.IsChecked = !toggle.IsChecked;

            Assert.Equal(0, played);
        }
        finally
        {
            window.Close();
            CupertinoHaptics.Handler = oldHandler;
        }
    }
}
