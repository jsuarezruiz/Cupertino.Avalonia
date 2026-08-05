using Avalonia.Controls;
using Avalonia.Interactivity;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class ControlsPage : UserControl
{
    public ControlsPage()
    {
        InitializeComponent();

        var today = new DateTimeOffset(DateTime.Today);
        var now = new TimeSpan(10, 24, 0);
        foreach (var name in new[] { "DemoDate", "DemoBothDate", "DemoDateOff" })
            this.FindControl<CupertinoDatePicker>(name)!.SelectedDate = today;
        foreach (var name in new[] { "DemoTime", "DemoBothTime", "DemoTime24" })
            this.FindControl<CupertinoTimePicker>(name)!.SelectedTime = now;

        this.FindControl<Calendar>("DemoCalendar")!.SelectedDate = DateTime.Today;
    }

    private async void OnAlert(object? sender, RoutedEventArgs e) =>
        await Dialog.ShowAsync(this,
            "Delete Photo?",
            "This photo will be deleted from all your devices.",
            new DialogAction("Cancel", DialogActionRole.Cancel),
            new DialogAction("Delete", DialogActionRole.Destructive));

    private async void OnSheet(object? sender, RoutedEventArgs e) =>
        await Dialog.ShowSheetAsync(this,
            "Photo options",
            "Choose what to do with this photo.",
            new DialogAction("Save to Files"),
            new DialogAction("Duplicate"),
            new DialogAction("Delete", DialogActionRole.Destructive),
            new DialogAction("Cancel", DialogActionRole.Cancel));
}
