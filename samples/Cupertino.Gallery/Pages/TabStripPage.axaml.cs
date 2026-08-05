using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Cupertino.Gallery.Pages;

public partial class TabStripPage : UserControl
{
    private static readonly string[] Modes =
    [
        "The overview: one line standing in for a summary.",
        "The detail: the strip changed, the stage cross-faded.",
    ];

    public TabStripPage()
    {
        InitializeComponent();
        this.FindControl<TransitioningContentControl>("ModeStage")!.Content = Modes[0];
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged can fire before initialization completes.
        if (!IsInitialized || sender is not TabStrip strip)
            return;
        var stage = this.FindControl<TransitioningContentControl>("ModeStage");
        if (stage is not null && strip.SelectedIndex >= 0)
            stage.Content = Modes[Math.Clamp(strip.SelectedIndex, 0, Modes.Length - 1)];
    }
}
