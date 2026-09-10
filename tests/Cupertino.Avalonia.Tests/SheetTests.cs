using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class SheetTests
{
    private static Window Host()
    {
        var window = new Window { Width = 400, Height = 800, Content = new Panel() };
        window.Show();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Sheet_presents_at_medium_detent()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var window = Host();
            var task = CupertinoSheet.ShowAsync(window, new TextBlock { Text = "body" });
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var presenter = FindPresenter(window);
            Assert.NotNull(presenter);
            Assert.False(task.IsCompleted);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void Scrim_tap_dismisses_and_completes()
    {
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var window = Host();
            var task = CupertinoSheet.ShowAsync(window, new TextBlock { Text = "body" }, SheetDetents.Large);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotNull(FindPresenter(window));

            window.MouseDown(new global::Avalonia.Point(200, 3), MouseButton.Left);
            window.MouseUp(new global::Avalonia.Point(200, 3), MouseButton.Left);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(task.IsCompleted);
            Assert.Null(FindPresenter(window));
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = false;
        }
    }

    [AvaloniaFact]
    public void Open_sheet_recomputes_its_geometry_when_the_host_resizes()
    {
        var old = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        try
        {
            var window = Host();
            _ = CupertinoSheet.ShowAsync(window, new TextBlock { Text = "body" });
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var presenter = Assert.IsType<CupertinoSheetPresenter>(FindPresenter(window));
            var root = Assert.IsType<Panel>(presenter.GetVisualParent());

            window.Width = 500;
            window.Height = 600;
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(window.ClientSize.Width, root.Width, 3);
            Assert.Equal(window.ClientSize.Height, root.Height, 3);
            var transform = Assert.IsType<global::Avalonia.Media.TranslateTransform>(presenter.RenderTransform);
            Assert.Equal(window.ClientSize.Height * 0.475, transform.Y, 3);
        }
        finally
        {
            CupertinoAccessibility.ReduceMotion = old;
        }
    }

    private static CupertinoSheetPresenter? FindPresenter(Window window)
    {
        foreach (var d in global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window))
            if (d is CupertinoSheetPresenter p)
                return p;
        return null;
    }
}

public class ToolbarTests
{
    [AvaloniaFact]
    public void Spacers_split_items_into_capsules()
    {
        var toolbar = new CupertinoToolbar();
        toolbar.Items.Add(new Button { Content = "a" });
        toolbar.Items.Add(new Button { Content = "b" });
        toolbar.Items.Add(new ToolbarSpacer());
        toolbar.Items.Add(new Button { Content = "c" });

        var window = new Window { Width = 400, Height = 200, Content = toolbar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var capsules = global::Avalonia.VisualTree.VisualExtensions
            .GetVisualDescendants(toolbar).OfType<GlassSurface>().ToList();
        Assert.Equal(2, capsules.Count);
        Assert.Equal(48, capsules[0].Bounds.Height);
    }

    [AvaloniaFact]
    public void Items_without_a_spacer_share_one_capsule()
    {
        var toolbar = new CupertinoToolbar();
        toolbar.Items.Add(new Button { Content = "a" });
        toolbar.Items.Add(new Button { Content = "b" });

        var window = new Window { Width = 400, Height = 200, Content = toolbar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Single(global::Avalonia.VisualTree.VisualExtensions
            .GetVisualDescendants(toolbar).OfType<GlassSurface>());
    }

    [AvaloniaFact]
    public void Embedded_toolbar_keeps_groups_and_hit_targets_inside_the_available_width()
    {
        var toolbar = new CupertinoToolbar { Width = 160 };
        toolbar.Classes.Add("embedded");
        toolbar.Items.Add(new Button { Content = new CupertinoIcon { Glyph = "chevron.left" } });
        toolbar.Items.Add(new Button { Content = new CupertinoIcon { Glyph = "chevron.right" } });
        toolbar.Items.Add(new ToolbarSpacer());
        toolbar.Items.Add(new Button { Content = new CupertinoIcon { Glyph = "magnifyingglass" } });

        var window = new Window { Width = 240, Height = 120, Content = toolbar };
        window.Show();
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var capsules = global::Avalonia.VisualTree.VisualExtensions
            .GetVisualDescendants(toolbar).OfType<GlassSurface>().ToList();
        Assert.Equal(2, capsules.Count);
        Assert.Equal(new Rect(0, 0, 100, 48), capsules[0].Bounds);
        Assert.Equal(new Rect(112, 0, 48, 48), capsules[1].Bounds);
        Assert.All(capsules, capsule => Assert.Null(capsule.RenderTransform));
        Assert.All(toolbar.Items.OfType<Button>(), button =>
            Assert.Equal(new Size(48, 48), button.Bounds.Size));
    }

    [AvaloniaFact]
    public void Adding_a_spacer_after_template_rebuilds_the_capsules()
    {
        var toolbar = new CupertinoToolbar();
        toolbar.Items.Add(new Button { Content = "a" });
        var window = new Window { Width = 400, Height = 200, Content = toolbar };
        window.Show();
        window.UpdateLayout();

        toolbar.Items.Add(new ToolbarSpacer());
        toolbar.Items.Add(new Button { Content = "b" });
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(2, global::Avalonia.VisualTree.VisualExtensions
            .GetVisualDescendants(toolbar).OfType<GlassSurface>().Count());
    }

    [AvaloniaFact]
    public void Removing_an_item_detaches_it_from_the_generated_capsule()
    {
        var button = new Button { Content = "reusable" };
        var toolbar = new CupertinoToolbar();
        toolbar.Items.Add(button);
        var window = new Window { Width = 400, Height = 200, Content = toolbar };
        window.Show();
        window.UpdateLayout();

        toolbar.Items.Remove(button);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(button.GetVisualParent());
        var destination = new StackPanel();
        destination.Children.Add(button);
        Assert.Same(destination, button.GetVisualParent());
    }
}
