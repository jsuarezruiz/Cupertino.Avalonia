using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class DropDownButtonPage : UserControl, IGalleryCaptureState
{
    public DropDownButtonPage() => InitializeComponent();

    public void ApplyGalleryCaptureState()
    {
        var button = this.FindControl<DropDownButton>("CaptureDropDown")!;
        button.Flyout?.ShowAt(button);
    }
}
