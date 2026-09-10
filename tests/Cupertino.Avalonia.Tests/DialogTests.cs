using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class DialogTests
{
    [AvaloniaFact]
    public void Dialogs_own_independent_default_action_collections()
    {
        var first = new Dialog();
        var second = new Dialog();

        first.Actions.Add(new DialogAction("Only first"));

        Assert.Single(first.Actions);
        Assert.Empty(second.Actions);
        Assert.NotSame(first.Actions, second.Actions);
    }

    [AvaloniaFact]
    public void Glass_shader_compiles_on_this_platform()
    {
        Assert.True(global::Cupertino.Rendering.LiquidGlassShader.IsSupported,
            "Shader failed to compile: " + global::Cupertino.Rendering.LiquidGlassShader.CompileError);
    }

    [AvaloniaFact]
    public void Dialog_shows_in_the_overlay_layer_and_lays_out()
    {
        var window = new Window { Width = 400, Height = 800, Content = new TextBlock { Text = "app" } };
        window.Show();
        window.UpdateLayout();

        var task = Dialog.ShowAsync(window, "Title", "Message",
            new DialogAction("Cancel", DialogActionRole.Cancel),
            new DialogAction("Delete", DialogActionRole.Destructive));

        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var layer = OverlayLayer.GetOverlayLayer(window);
        Assert.NotNull(layer);

        var dialog = layer!.GetVisualDescendants().OfType<Dialog>().FirstOrDefault();
        Assert.True(dialog is not null, "Dialog was never added to the overlay layer.");
        Assert.True(dialog!.Bounds.Width > 0 && dialog.Bounds.Height > 0,
            $"Dialog laid out empty: {dialog.Bounds}");

        var buttons = dialog.GetVisualDescendants().OfType<Button>().ToList();
        Assert.True(buttons.Count == 2, $"Expected 2 action buttons, found {buttons.Count}.");
        var material = Assert.Single(dialog.GetVisualDescendants().OfType<GlassSurface>());
        Assert.True(material.IsBackdropFrozen,
            "Modal glass must keep its clean backdrop while focus and motion invalidate partial regions.");

        Assert.False(task.IsCompleted);
        dialog.Close(null);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompleted);
    }

    [AvaloniaFact]
    public async Task Dialog_moves_keyboard_focus_inside_and_restores_it_after_close()
    {
        var owner = new Button { Content = "Owner" };
        var window = new Window { Width = 400, Height = 800, Content = owner };
        window.Show();
        window.UpdateLayout();
        Assert.True(owner.Focus());

        var dialog = new Dialog
        {
            Title = "Title",
            Actions = new[] { new DialogAction("OK") },
        };
        _ = dialog.ShowAsync(window);
        window.UpdateLayout();
        await Task.Delay(30);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var focused = window.FocusManager?.GetFocusedElement();
        Assert.Contains(focused as Visual, dialog.GetVisualDescendants().Prepend(dialog));

        dialog.Close(null);
        await Task.Delay(180);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Same(owner, window.FocusManager?.GetFocusedElement());
    }

    [AvaloniaFact]
    public void A_dialog_cannot_be_presented_twice_at_the_same_time()
    {
        var window = new Window { Width = 400, Height = 800, Content = new Border() };
        window.Show();
        window.UpdateLayout();
        var dialog = new Dialog();
        _ = dialog.ShowAsync(window);

        Assert.Throws<InvalidOperationException>(() => { _ = dialog.ShowAsync(window); });
        dialog.Close(null);
    }

    [AvaloniaFact]
    public void Removing_the_overlay_completes_a_presented_dialog()
    {
        var window = new Window { Width = 400, Height = 800, Content = new Border() };
        window.Show();
        window.UpdateLayout();
        var dialog = new Dialog();
        var task = dialog.ShowAsync(window);

        window.Close();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(task.IsCompleted);
        Assert.Null(task.Result);
    }

    [AvaloniaFact]
    public void A_closed_dialog_can_be_shown_again_before_its_fade_cleanup_runs()
    {
        var window = new Window { Width = 400, Height = 800, Content = new Border() };
        window.Show();
        window.UpdateLayout();
        var dialog = new Dialog();

        var first = dialog.ShowAsync(window);
        dialog.Close(null);
        var second = dialog.ShowAsync(window);

        Assert.True(first.IsCompleted);
        Assert.False(second.IsCompleted);
        dialog.Close(null);
        Assert.True(second.IsCompleted);
    }
}
