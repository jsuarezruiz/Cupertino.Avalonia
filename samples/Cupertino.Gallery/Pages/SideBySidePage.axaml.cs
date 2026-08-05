using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class SideBySidePage : UserControl
{
    private static readonly (string Kind, string Host)[] Pairs =
    [
        ("switch", "NativeSwitch"),
        ("slider", "NativeSlider"),
        ("segmented", "NativeSegmented"),
        ("progress", "NativeProgress"),
        ("spinner", "NativeSpinner"),
        ("button-prominent", "NativeFilledButton"),
        ("button-plain", "NativePlainButton"),
        ("stepper", "NativeStepper"),
        ("tabbar", "NativeTabBar"),
        ("navbar", "NativeNavigationBar"),
        ("toolbar", "NativeToolbar"),
        ("textfield", "NativeTextField"),
        ("searchfield", "NativeSearchField"),
        ("date", "NativeDate"),
        ("time", "NativeTime"),
    ];

    private bool _filled;

    public SideBySidePage()
    {
        InitializeComponent();

        this.FindControl<CupertinoDatePicker>("OurDate")!.SelectedDate = DateTime.Today;
        this.FindControl<CupertinoTimePicker>("OurTime")!.SelectedTime = new TimeSpan(9, 41, 0);

        // Wait for the navigation transition.
        AttachedToVisualTree += (_, _) =>
            DispatcherTimer.RunOnce(Fill, TimeSpan.FromMilliseconds(600));

        // Poll because hosted views ignore transformed clipping.
        _trim = new DispatcherTimer(
            TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => TrimToViewport());
        AttachedToVisualTree += (_, _) => _trim.Start();
        DetachedFromVisualTree += (_, _) => _trim.Stop();
    }

    private readonly DispatcherTimer _trim;
    private int _tick;

    private void Fill()
    {
        if (_filled)
            return;
        _filled = true;

        foreach (var (kind, host) in Pairs)
            this.FindControl<ContentControl>(host)!.Content =
                NativeComparison.CreateNativeControl?.Invoke(kind);
        TrimToViewport();
    }

    private void TrimToViewport()
    {
        if (!_filled)
            return;

        var overlay = Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(this);
        var covered = overlay is { Children.Count: > 0 };

        if (TopLevel.GetTopLevel(this) is not { } root)
            return;
        foreach (var (_, name) in Pairs)
        {
            var host = this.FindControl<ContentControl>(name)!;
            var y = host.TranslatePoint(new Point(0, 0), root)?.Y;
            if (y is null)
                continue;
            var visible = !covered && y > 100;
            if (host.Content is Control control && control.IsVisible != visible)
                control.IsVisible = visible;
        }

        if (Environment.GetEnvironmentVariable("GALLERY_DUMP") == "1" && ++_tick % 20 == 0)
            DumpGeometry();
    }

    private void DumpGeometry()
    {
        if (TopLevel.GetTopLevel(this) is not { } root)
            return;
        foreach (var (kind, name) in Pairs)
        {
            var host = this.FindControl<ContentControl>(name)!;
            var grid = (Grid)host.Parent!;
            var ours = grid.Children.OfType<Control>().FirstOrDefault(c => Grid.GetColumn(c) == 2);
            var np = host.TranslatePoint(new Point(0, 0), root);
            var op = ours?.TranslatePoint(new Point(0, 0), root);
            if (np is null || op is null || ours is null)
                continue;
            Console.WriteLine(
                $"[geom] {kind} N {np.Value.X:F0},{np.Value.Y:F0},{host.Bounds.Width:F0},{host.Bounds.Height:F0}" +
                $" A {op.Value.X:F0},{op.Value.Y:F0},{ours.Bounds.Width:F0},{ours.Bounds.Height:F0}");
        }
        Console.WriteLine("[geom] end");
    }
}
