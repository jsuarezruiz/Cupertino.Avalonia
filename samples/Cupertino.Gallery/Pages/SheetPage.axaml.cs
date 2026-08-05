using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class SheetPage : UserControl
{
    public SheetPage()
    {
        InitializeComponent();
    }

    private static StackPanel SheetBody(string title) => new()
    {
        Spacing = 8,
        Children =
        {
            new TextBlock
            {
                Text = title,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 4, 0, 0),
            },
            new TextBlock
            {
                Text = "Drag the top to move between detents.\nFlick down or tap outside to dismiss.",
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                Opacity = 0.55,
            },
        },
    };

    private async void OnMediumLarge(object? sender, RoutedEventArgs e) =>
        await CupertinoSheet.ShowAsync(this, SheetBody("Medium and large"));

    private async void OnLarge(object? sender, RoutedEventArgs e) =>
        await CupertinoSheet.ShowAsync(this, SheetBody("Large only"), SheetDetents.Large);
}
