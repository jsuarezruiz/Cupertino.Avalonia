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

    private int _line;

    public TransitioningContentPage()
    {
        InitializeComponent();
        foreach (var name in (string[])["Stage", "Fade", "Slide"])
            this.FindControl<TransitioningContentControl>(name)!.Content = Lines[0];
    }

    private void OnSwap(object? sender, RoutedEventArgs e)
    {
        _line = (_line + 1) % Lines.Length;
        foreach (var name in (string[])["Stage", "Fade", "Slide"])
            this.FindControl<TransitioningContentControl>(name)!.Content = Lines[_line];
    }
}
