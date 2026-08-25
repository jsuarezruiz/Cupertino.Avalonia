using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class MenuPage : UserControl, IGalleryCaptureState
{
    public MenuPage() => InitializeComponent();

    public void ApplyGalleryCaptureState() =>
        this.FindControl<MenuItem>("CaptureMenu")!.IsSubMenuOpen = true;
}
