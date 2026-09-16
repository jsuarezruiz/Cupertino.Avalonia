using Avalonia.Controls;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class TimePickerPage : UserControl
{
    public TimePickerPage()
    {
        InitializeComponent();
        var time = GalleryCaptureState.IsEnabled
            ? GalleryCaptureState.CaptureDateTime.TimeOfDay
            : DateTime.Now.TimeOfDay;
        this.FindControl<CupertinoTimePicker>("DemoTime")!.SelectedTime = time;
        this.FindControl<CupertinoTimePicker>("DemoTime24")!.SelectedTime = time;
        this.FindControl<CupertinoTimePicker>("DemoInlineTime")!.SelectedTime = time;
        this.FindControl<CupertinoTimePicker>("DemoDuration")!.SelectedTime = TimeSpan.FromMinutes(95);
    }
}
