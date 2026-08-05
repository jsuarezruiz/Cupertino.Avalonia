using Avalonia.Controls;

namespace Cupertino.Gallery.Pages;

public partial class CalendarPage : UserControl
{
    public CalendarPage()
    {
        InitializeComponent();
        this.FindControl<Calendar>("DemoCalendar")!.SelectedDate = DateTime.Today;

        this.FindControl<Calendar>("RangeCalendar")!
            .SelectedDates.AddRange(DateTime.Today.AddDays(2), DateTime.Today.AddDays(6));

        var bounded = this.FindControl<Calendar>("BoundedCalendar")!;
        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        bounded.DisplayDateStart = firstOfMonth;
        bounded.DisplayDateEnd = firstOfMonth.AddMonths(1).AddDays(-1);

        this.FindControl<Calendar>("DisabledCalendar")!.SelectedDate = DateTime.Today;
    }
}
