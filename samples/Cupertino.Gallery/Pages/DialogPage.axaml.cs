using Avalonia.Controls;
using Avalonia.Interactivity;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class DialogPage : UserControl, IGalleryCaptureState
{
    public DialogPage() => InitializeComponent();

    public void ApplyGalleryCaptureState() =>
        _ = Dialog.ShowAsync(this,
            "Delete Photo?",
            "This photo will be deleted from all your devices.",
            new DialogAction("Cancel", DialogActionRole.Cancel),
            new DialogAction("Delete", DialogActionRole.Destructive));

    private void Report(DialogAction? chosen) =>
        this.FindControl<TextBlock>("Result")!.Text = chosen?.Title ?? "dismissed";

    private async void OnInfo(object? sender, RoutedEventArgs e) =>
        Report(await Dialog.ShowAsync(this,
            "Backup Complete",
            "Your library is safely backed up."));

    private async void OnAlert(object? sender, RoutedEventArgs e) =>
        Report(await Dialog.ShowAsync(this,
            "Delete Photo?",
            "This photo will be deleted from all your devices.",
            new DialogAction("Cancel", DialogActionRole.Cancel),
            new DialogAction("Delete", DialogActionRole.Destructive)));

    private async void OnThreeActions(object? sender, RoutedEventArgs e) =>
        Report(await Dialog.ShowAsync(this,
            "Unsaved Changes",
            "Do you want to save your changes before closing?",
            new DialogAction("Save"),
            new DialogAction("Don't Save", DialogActionRole.Destructive),
            new DialogAction("Cancel", DialogActionRole.Cancel)));

    private async void OnSheet(object? sender, RoutedEventArgs e) =>
        Report(await Dialog.ShowSheetAsync(this,
            "Photo options",
            null,
            new DialogAction("Save to Files"),
            new DialogAction("Duplicate"),
            new DialogAction("Delete", DialogActionRole.Destructive),
            new DialogAction("Cancel", DialogActionRole.Cancel)));
}
