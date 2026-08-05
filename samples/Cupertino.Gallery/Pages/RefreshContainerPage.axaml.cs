using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Cupertino.Gallery.Pages;

public partial class RefreshContainerPage : UserControl
{
    private int _batch;

    public RefreshContainerPage()
    {
        InitializeComponent();
        Fill("Loaded");
    }

    private void Fill(string label)
    {
        var rows = this.FindControl<StackPanel>("Rows")!;
        rows.Children.Clear();
        for (var i = 1; i <= 12; i++)
        {
            rows.Children.Add(new Border
            {
                Height = 44,
                Child = new TextBlock
                {
                    Text = $"{label} · row {i}",
                    Margin = new Thickness(16, 12),
                },
            });
            rows.Children.Add(new Avalonia.Controls.Shapes.Rectangle
            {
                Height = 0.5,
                Margin = new Thickness(16, 0, 0, 0),
                Fill = this.TryFindResource("CupertinoSeparatorBrush", out var v) && v is IBrush b
                    ? b : Brushes.LightGray,
            });
        }
    }

    private void OnRequestRefresh(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        this.FindControl<RefreshContainer>("Refresher")!.RequestRefresh();

    private void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            _batch++;
            Fill($"Refresh {_batch}");
            deferral.Complete();
        }, TimeSpan.FromSeconds(1.5));
    }
}
