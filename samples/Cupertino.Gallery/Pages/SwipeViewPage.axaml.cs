using Avalonia.Controls;
using Avalonia.Interactivity;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class SwipeViewPage : UserControl
{
    public SwipeViewPage() => InitializeComponent();

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        Report(sender);
        CloseHost(sender);
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        Report(sender);
        CloseHost(sender);
    }

    private void Report(object? sender)
    {
        var label = (sender as Button)?.Content?.ToString() ?? "?";
        this.FindControl<TextBlock>("Log")!.Text = $"\"{label}\" fired";
    }

    private static void CloseHost(object? sender)
    {
        for (var c = sender as Control; c is not null; c = c.Parent as Control)
            if (c is CupertinoSwipeView swipe)
            {
                swipe.Close();
                return;
            }
    }
}
