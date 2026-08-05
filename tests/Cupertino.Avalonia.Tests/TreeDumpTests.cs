using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;


namespace Cupertino.Avalonia.Tests;

public class TreeDumpTests
{
    private readonly ITestOutputHelper _out;

    public TreeDumpTests(ITestOutputHelper output) => _out = output;

    [AvaloniaFact]
    public void Dump_dialog_action_button_tree()
    {
        var window = new Window { Width = 400, Height = 800, Content = new TextBlock() };
        window.Show();
        window.UpdateLayout();

        _ = Dialog.ShowAsync(window, "T", "M",
            new DialogAction("Cancel", DialogActionRole.Cancel),
            new DialogAction("Delete", DialogActionRole.Destructive));
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var layer = OverlayLayer.GetOverlayLayer(window)!;
        var dialog = layer.GetVisualDescendants().OfType<Dialog>().First();
        var button = dialog.GetVisualDescendants().OfType<Button>().First();

        _out.WriteLine($"button theme: {button.Theme?.GetType().Name} key-corner {button.CornerRadius}");
        ((global::Avalonia.Controls.IPseudoClasses)button.Classes).Add(":pointerover");
        window.UpdateLayout();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        _out.WriteLine("--- HOVERED: every background in the dialog ---");
        foreach (var v in dialog.GetSelfAndVisualDescendants())
        {
            var b = (v as Border)?.Background ?? (v as Panel)?.Background
                    ?? (v as ContentPresenter)?.Background ?? (v as TemplatedControl)?.Background;
            if (b is not null)
                _out.WriteLine($"  {v.GetType().Name,-20} {v.Bounds} bg={Describe(b)}");
        }
        _out.WriteLine("--- button subtree ---");
        foreach (var v in button.GetSelfAndVisualDescendants())
        {
            var bg = (v as Border)?.Background ?? (v as Panel)?.Background
                     ?? (v as ContentPresenter)?.Background ?? (v as TemplatedControl)?.Background;
            var cr = (v as Border)?.CornerRadius ?? (v as ContentPresenter)?.CornerRadius;
            _out.WriteLine($"  {v.GetType().Name,-22} bounds={v.Bounds} bg={Describe(bg)} radius={cr}");
        }
    }

    private static string Describe(IBrush? b) => b switch
    {
        null => "-",
        ISolidColorBrush s => s.Color.ToString(),
        _ => b.GetType().Name,
    };
}
