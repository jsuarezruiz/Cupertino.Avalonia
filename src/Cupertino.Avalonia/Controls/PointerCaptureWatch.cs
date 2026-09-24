using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cupertino.Controls;

// Capture-lost is a direct event raised on the captured element, often a template part.
internal static class PointerCaptureWatch
{
    public static void OnLost(IPointer pointer, Action lost)
    {
        if (pointer.Captured is not InputElement captured)
            return;

        void Handler(object? sender, PointerCaptureLostEventArgs e)
        {
            captured.RemoveHandler(InputElement.PointerCaptureLostEvent, Handler);
            lost();
        }

        captured.AddHandler(InputElement.PointerCaptureLostEvent, Handler,
            RoutingStrategies.Direct, handledEventsToo: true);
    }
}
