using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Cupertino.Gallery.Pages;

public partial class NotificationsPage : UserControl
{
    private WindowNotificationManager? _manager;

    public NotificationsPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Install once for this page.
        _manager ??= new WindowNotificationManager(TopLevel.GetTopLevel(this))
        {
            Position = NotificationPosition.TopCenter,
            MaxItems = 3,
        };
    }

    private void Show(string title, string message, NotificationType type) =>
        _manager?.Show(new Notification(title, message, type));

    private void OnInfo(object? sender, RoutedEventArgs e) =>
        Show("Now Playing", "Glass on every surface.", NotificationType.Information);

    private void OnSuccess(object? sender, RoutedEventArgs e) =>
        Show("Saved", "The document is safely on disk.", NotificationType.Success);

    private void OnWarning(object? sender, RoutedEventArgs e) =>
        Show("Storage low", "Only 500 MB remain.", NotificationType.Warning);

    private void OnError(object? sender, RoutedEventArgs e) =>
        Show("Sync failed", "The server did not respond.", NotificationType.Error);

    private void OnQuick(object? sender, RoutedEventArgs e) =>
        _manager?.Show(new Notification("Quick", "Gone in two seconds.",
            NotificationType.Information, TimeSpan.FromSeconds(2)));

    private void OnSlow(object? sender, RoutedEventArgs e) =>
        _manager?.Show(new Notification("Slow", "Stays for ten seconds.",
            NotificationType.Information, TimeSpan.FromSeconds(10)));

    private void OnSticky(object? sender, RoutedEventArgs e) =>
        _manager?.Show(new Notification("Sticky", "Stays until you close or click it.",
            NotificationType.Warning, TimeSpan.Zero));

    private void OnCustomContent(object? sender, RoutedEventArgs e)
    {
        var art = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new Avalonia.CornerRadius(8),
            Background = Avalonia.Media.Brush.Parse("#FF5E5CE6"),
            Child = new TextBlock
            {
                Text = "♪",
                FontSize = 20,
                Foreground = Avalonia.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var text = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Deep Focus", FontSize = 15, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock { Text = "Ambient · 3:42", FontSize = 13 },
            },
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { art, text },
        };
        _manager?.Show(row, NotificationType.Information, TimeSpan.FromSeconds(6));
    }

    private void OnCallbacks(object? sender, RoutedEventArgs e)
    {
        var result = this.FindControl<TextBlock>("CallbackResult")!;
        _manager?.Show(new Notification("Meeting in 5", "Design review, Studio 2.",
            NotificationType.Information, TimeSpan.FromSeconds(8),
            onClick: () => result.Text = "clicked",
            onClose: () => result.Text = "closed"));
    }
}
