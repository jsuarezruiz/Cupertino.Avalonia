using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class ComboBoxPage : UserControl, IGalleryCaptureState
{
    public ComboBoxPage() => InitializeComponent();

    public void ApplyGalleryCaptureState() =>
        this.FindControl<ComboBox>("CapturePicker")!.IsDropDownOpen = true;
}
