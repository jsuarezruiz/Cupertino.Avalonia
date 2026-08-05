using System.Globalization;
using Avalonia.Controls;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class WheelLabWindow : Window
{
    public WheelLabWindow()
    {
        InitializeComponent();

        var days = this.FindControl<CupertinoWheel>("Days")!;
        days.Items = Enumerable.Range(1, 31)
            .Select(day => day.ToString(CultureInfo.CurrentCulture))
            .ToList();
        days.SelectedIndex = 29;                       // 30th

        var months = this.FindControl<CupertinoWheel>("Months")!;
        months.Items = CultureInfo.CurrentCulture
            .DateTimeFormat.MonthNames.Take(12).ToList();
        months.SelectedIndex = 6;                      // July

        var years = this.FindControl<CupertinoWheel>("Years")!;
        years.Items = Enumerable.Range(1900, 300)
            .Select(year => year.ToString(CultureInfo.CurrentCulture))
            .ToList();
        years.SelectedIndex = 2026 - 1900;
    }
}
