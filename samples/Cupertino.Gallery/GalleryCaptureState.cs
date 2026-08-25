using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Cupertino.Gallery;

/// <summary>
/// Supplies the representative interaction state used by gallery screenshots.
/// </summary>
public interface IGalleryCaptureState
{
    void ApplyGalleryCaptureState();
}

internal static class GalleryCaptureState
{
    private const string EnvironmentVariable = "GALLERY_CAPTURE_STATE";

    public static DateTime CaptureDate { get; } = new(2026, 8, 25);

    public static DateTimeOffset CaptureDateTime { get; } =
        new(2026, 8, 25, 10, 10, 0, TimeSpan.FromHours(2));

    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "1",
            StringComparison.Ordinal);

    public static void Attach(Control content)
    {
        if (!IsEnabled || content is not IGalleryCaptureState captureState)
            return;

        EventHandler<RoutedEventArgs>? applyOnce = null;
        applyOnce = (_, _) =>
        {
            content.Loaded -= applyOnce;
            // Popup anchors need their final navigation-transition bounds.
            DispatcherTimer.RunOnce(captureState.ApplyGalleryCaptureState,
                TimeSpan.FromMilliseconds(350), DispatcherPriority.Loaded);
        };
        content.Loaded += applyOnce;
    }
}
