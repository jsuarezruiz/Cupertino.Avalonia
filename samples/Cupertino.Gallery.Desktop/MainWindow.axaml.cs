using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class MainWindow : Window
{
    private Point _dragStart;
    private Point _heroOffset;
    private bool _dragging;

    public MainWindow()
    {
        InitializeComponent();

        var hero = this.FindControl<GlassSurface>("Hero")!;
        hero.RenderTransform = new TranslateTransform();
        hero.PointerPressed += OnHeroPressed;
        hero.PointerMoved += OnHeroMoved;
        hero.PointerReleased += OnHeroReleased;

        if (Program.RecordDir is { } dir)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(1200);
                await RecordToggleMotion(dir);
                Close();
            };
        }
        else if (Program.ScreenshotPath is { } path)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(900);
                await Dispatcher.UIThread.InvokeAsync(() => SaveScreenshot(path));
                Close();
            };
        }
    }

    private async System.Threading.Tasks.Task RecordToggleMotion(string dir)
    {
        var bt = this.FindControl<ToggleSwitch>("BtSwitch")!;
        var card = this.FindControl<Cupertino.Controls.GlassSurface>("SwitchCard")!;
        var size = new PixelSize((int)(card.Bounds.Width * 2), (int)(card.Bounds.Height * 2));
        System.IO.Directory.CreateDirectory(dir);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        bt.IsChecked = true;
        while (clock.ElapsedMilliseconds < 550)
        {
            var ms = clock.ElapsedMilliseconds;
            using (var rtb = new RenderTargetBitmap(size, new Vector(192, 192)))
            {
                rtb.Render(card);
                rtb.Save(System.IO.Path.Combine(dir, $"f_{ms:D4}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            await System.Threading.Tasks.Task.Delay(8);
        }
    }

    private void SaveScreenshot(string path)
    {
        var root = this.FindControl<Panel>("Root")!;
        var size = new PixelSize((int)(root.Bounds.Width * 2), (int)(root.Bounds.Height * 2));
        using var rtb = new RenderTargetBitmap(size, new Vector(192, 192));
        rtb.Render(root);
        rtb.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private void OnHeroPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragging = true;
        _dragStart = e.GetPosition(this);
        e.Pointer.Capture((GlassSurface)sender!);
    }

    private void OnHeroMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
            return;
        var pos = e.GetPosition(this);
        var t = (TranslateTransform)((GlassSurface)sender!).RenderTransform!;
        t.X = _heroOffset.X + (pos.X - _dragStart.X);
        t.Y = _heroOffset.Y + (pos.Y - _dragStart.Y);
    }

    private void OnHeroReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        var t = (TranslateTransform)((GlassSurface)sender!).RenderTransform!;
        _heroOffset = new Point(t.X, t.Y);
        e.Pointer.Capture(null);
    }
}
