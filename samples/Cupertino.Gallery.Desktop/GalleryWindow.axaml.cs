using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class GalleryWindow : Window
{
    public GalleryWindow()
    {
        InitializeComponent();

        if (Program.ScreenshotPath is null && Program.ShowAlert)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(700);
                if (Program.ShowSheet)
                {
                    _ = Dialog.ShowSheetAsync(this,
                        "Photo options",
                        "Choose what to do with this photo.",
                        new DialogAction("Save to Files"),
                        new DialogAction("Duplicate"),
                        new DialogAction("Delete", DialogActionRole.Destructive),
                        new DialogAction("Cancel", DialogActionRole.Cancel));
                    return;
                }

                _ = Dialog.ShowAsync(this,
                    "Delete Photo?",
                    "This photo will be deleted from all your devices.",
                    new DialogAction("Cancel", DialogActionRole.Cancel),
                    new DialogAction("Delete", DialogActionRole.Destructive));

                if (Program.HoverAction)
                {
                    await System.Threading.Tasks.Task.Delay(600);
                    var layer = Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(this);
                    var btn = layer?.GetVisualDescendants().OfType<Button>().FirstOrDefault();
                    if (btn is not null)
                        ((Avalonia.Controls.IPseudoClasses)btn.Classes).Add(":pointerover");
                }
            };
        }

        if (Program.ScreenshotPath is { } path)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(1000);
                if (Program.ShowAlert)
                {
                    _ = Dialog.ShowAsync(this,
                        "Delete Photo?",
                        "This photo will be deleted from all your devices.",
                        new DialogAction("Cancel", DialogActionRole.Cancel),
                        new DialogAction("Delete", DialogActionRole.Destructive));
                    await System.Threading.Tasks.Task.Delay(600);
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var size = new PixelSize((int)(Bounds.Width * 2), (int)(Bounds.Height * 2));
                    using var rtb = new RenderTargetBitmap(size, new Vector(192, 192));
                    rtb.Render(this);
                    rtb.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

                    // RenderTargetBitmap excludes the overlay layer.
                    if (Program.ShowAlert &&
                        Avalonia.Controls.Primitives.OverlayLayer.GetOverlayLayer(this) is { } layer)
                    {
                        using var orb = new RenderTargetBitmap(size, new Vector(192, 192));
                        orb.Render(layer);
                        orb.Save(path.Replace(".png", "_overlay.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    }
                });
                Close();
            };
        }
    }
}
