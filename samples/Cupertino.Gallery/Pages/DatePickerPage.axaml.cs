using Avalonia.Controls;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class DatePickerPage : UserControl, IGalleryCaptureState
{
    public DatePickerPage()
    {
        InitializeComponent();
        var now = DateTimeOffset.Now;
        this.FindControl<CupertinoDatePicker>("DemoDate")!.SelectedDate = now;
        this.FindControl<CupertinoDateTimePicker>("DemoDateTime")!.SelectedDateTime = now;
        var inline = this.FindControl<CupertinoDatePicker>("DemoInlineDate")!;
        inline.SelectedDate = now;
        inline.MinimumDate = now.AddDays(-14);
        inline.MaximumDate = now.AddDays(45);
    }

    public void ApplyGalleryCaptureState()
    {
        var date = GalleryCaptureState.CaptureDateTime;
        this.FindControl<CupertinoDatePicker>("DemoDate")!.SelectedDate = date;
        this.FindControl<CupertinoDateTimePicker>("DemoDateTime")!.SelectedDateTime = date;
        var inline = this.FindControl<CupertinoDatePicker>("DemoInlineDate")!;
        inline.SelectedDate = date;
        inline.MinimumDate = date.AddDays(-14);
        inline.MaximumDate = date.AddDays(45);
    }
}
