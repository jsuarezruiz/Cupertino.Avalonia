using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class FlyoutPage : UserControl, IGalleryCaptureState
{
    public FlyoutPage() => InitializeComponent();

    public void ApplyGalleryCaptureState()
    {
        var button = this.FindControl<Button>("CaptureFlyout")!;
        button.Flyout?.ShowAt(button);
    }
}
