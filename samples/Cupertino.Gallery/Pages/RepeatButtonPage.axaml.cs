using System.Globalization;
using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class RepeatButtonPage : UserControl
{
    private int _count;

    public RepeatButtonPage()
    {
        InitializeComponent();

        var readout = this.FindControl<TextBlock>("RepeatReadout")!;
        this.FindControl<RepeatButton>("RepeatDown")!.Click += (_, _) =>
            readout.Text = (--_count).ToString(CultureInfo.CurrentCulture);
        this.FindControl<RepeatButton>("RepeatUp")!.Click += (_, _) =>
            readout.Text = (++_count).ToString(CultureInfo.CurrentCulture);
    }
}
