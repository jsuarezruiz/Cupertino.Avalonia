using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Cupertino.Gallery;

public partial class MatteWindow : Window
{
    public MatteWindow()
    {
        InitializeComponent();
        if (Program.ScreenshotPath is { } path)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(900);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var size = new PixelSize((int)(Bounds.Width * 3), (int)(Bounds.Height * 3));
                    using var rtb = new RenderTargetBitmap(size, new Vector(288, 288));
                    rtb.Render(this);
                    rtb.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                });
                Close();
            };
        }
    }
}
