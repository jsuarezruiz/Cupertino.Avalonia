using Avalonia.Controls;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class CalLabWindow : Window
{
    public CalLabWindow()
    {
        InitializeComponent();
        this.FindControl<CupertinoCalendarView>("Cal")!.SelectedDate =
            new DateTimeOffset(new DateTime(2026, 7, 30));
    }
}
