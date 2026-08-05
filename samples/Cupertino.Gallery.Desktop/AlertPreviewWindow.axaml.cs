using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class AlertPreviewWindow : Window
{
    public AlertPreviewWindow()
    {
        InitializeComponent();
        var alert = this.FindControl<Dialog>("Alert")!;
        if (!Program.NoActions)
            alert.Actions = new[]
            {
                new DialogAction("Cancel", DialogActionRole.Cancel),
                new DialogAction("Delete", DialogActionRole.Destructive),
            };

        if (Program.ScreenshotPath is { } path)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(800);
                if (Program.HoverAction)
                {
                    var btn = alert.GetVisualDescendants().OfType<Button>().FirstOrDefault();
                    if (btn is not null)
                    {
                        ((Avalonia.Controls.IPseudoClasses)btn.Classes).Add(":pointerover");
                        btn.Focus(Avalonia.Input.NavigationMethod.Tab);
                    }
                    await System.Threading.Tasks.Task.Delay(300);
                }
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
