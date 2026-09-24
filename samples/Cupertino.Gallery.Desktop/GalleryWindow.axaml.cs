using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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

        if (Program.ScreenshotPath is { } path)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(1000);
                await ShowRequestedOverlay();
                await Dispatcher.UIThread.InvokeAsync(() => SaveScreenshot(path, Program.ShowAlert));
                Close();
            };
        }
        else if (Program.ShowAlert)
        {
            Opened += async (_, _) =>
            {
                await System.Threading.Tasks.Task.Delay(700);
                await ShowRequestedOverlay();
            };
        }
    }

    private async System.Threading.Tasks.Task ShowRequestedOverlay()
    {
        if (!Program.ShowAlert)
            return;

        var title = Program.ShowSheet ? "Photo options" : "Delete Photo?";
        var message = Program.ShowSheet
            ? "Choose what to do with this photo."
            : "This photo will be deleted from all your devices.";

        if (Program.NoActions)
        {
            // ShowAsync adds a default action, so build the dialog directly.
            var bare = new Dialog { Title = title, Message = message };
            if (Program.ShowSheet)
            {
                bare.ActionsLayout = DialogActionsLayout.Stack;
                bare.Width = 240;
            }
            _ = bare.ShowAsync(this);
        }
        else if (Program.ShowSheet)
        {
            _ = Dialog.ShowSheetAsync(this, title, message,
                new DialogAction("Save to Files"),
                new DialogAction("Duplicate"),
                new DialogAction("Delete", DialogActionRole.Destructive),
                new DialogAction("Cancel", DialogActionRole.Cancel));
        }
        else
        {
            _ = Dialog.ShowAsync(this, title, message,
                new DialogAction("Cancel", DialogActionRole.Cancel),
                new DialogAction("Delete", DialogActionRole.Destructive));
        }

        if (Program.HoverAction)
        {
            await System.Threading.Tasks.Task.Delay(600);
            var button = OverlayLayer.GetOverlayLayer(this)?
                .GetVisualDescendants().OfType<Button>().FirstOrDefault();
            if (button is not null)
            {
                ((IPseudoClasses)button.Classes).Add(":pointerover");
                button.Focus(NavigationMethod.Tab);
            }
            await System.Threading.Tasks.Task.Delay(300);
        }
        else
        {
            await System.Threading.Tasks.Task.Delay(600);
        }
    }

    private void SaveScreenshot(string path, bool overlay)
    {
        var size = new PixelSize((int)(Bounds.Width * 2), (int)(Bounds.Height * 2));
        using var rtb = new RenderTargetBitmap(size, new Vector(192, 192));
        rtb.Render(this);
        rtb.Save(path, new PngBitmapEncoderOptions());

        // RenderTargetBitmap excludes the overlay layer.
        if (overlay && OverlayLayer.GetOverlayLayer(this) is { } layer)
        {
            using var orb = new RenderTargetBitmap(size, new Vector(192, 192));
            orb.Render(layer);
            orb.Save(path.Replace(".png", "_overlay.png"), new PngBitmapEncoderOptions());
        }
    }
}
