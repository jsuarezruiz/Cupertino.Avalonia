using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class CalendarDatePickerPage : UserControl
{
    public CalendarDatePickerPage()
    {
        InitializeComponent();

        this.FindControl<CalendarDatePicker>("PresetPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("LongPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("IsoPicker")!.SelectedDate = DateTime.Today;
        this.FindControl<CalendarDatePicker>("DisabledPreset")!.SelectedDate = DateTime.Today;
    }
}
