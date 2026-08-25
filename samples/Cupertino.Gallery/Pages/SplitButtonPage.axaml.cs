using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class SplitButtonPage : UserControl, IGalleryCaptureState
{
    public SplitButtonPage() => InitializeComponent();

    public void ApplyGalleryCaptureState()
    {
        var button = this.FindControl<SplitButton>("CaptureSplit")!;
        button.Flyout?.ShowAt(button);
    }
}
