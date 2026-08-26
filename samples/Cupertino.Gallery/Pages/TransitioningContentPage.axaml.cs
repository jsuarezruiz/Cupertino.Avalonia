using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cupertino.Gallery.Pages;

public partial class TransitioningContentPage : UserControl
{
    private static readonly string[] Lines =
    [
        "Every surface is glass.",
        "The material samples the app behind it.",
        "Motion uses Cupertino timing.",
    ];

    private int _defaultLine;
    private int _customizedLine;

    public TransitioningContentPage()
    {
        InitializeComponent();
        foreach (var name in (string[])["Stage", "Fade", "Slide"])
            this.FindControl<TransitioningContentControl>(name)!.Content = Lines[0];
    }

    private void OnDefaultSwap(object? sender, RoutedEventArgs e)
    {
        _defaultLine = (_defaultLine + 1) % Lines.Length;
        this.FindControl<TransitioningContentControl>("Stage")!.Content = Lines[_defaultLine];
    }

    private void OnCustomizedSwap(object? sender, RoutedEventArgs e)
    {
        _customizedLine = (_customizedLine + 1) % Lines.Length;
        foreach (var name in (string[])["Fade", "Slide"])
            this.FindControl<TransitioningContentControl>(name)!.Content = Lines[_customizedLine];
    }
}
