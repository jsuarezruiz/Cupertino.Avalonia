using Avalonia.Controls;

namespace Cupertino.Gallery;

public partial class NavLabWindow : Window
{
    public NavLabWindow()
    {
        InitializeComponent();
        if (Program.ScrollTo > 0)
            Opened += (_, _) => this.FindControl<ScrollViewer>("Scroller")!.Offset =
                new Avalonia.Vector(0, Program.ScrollTo);
    }
}
