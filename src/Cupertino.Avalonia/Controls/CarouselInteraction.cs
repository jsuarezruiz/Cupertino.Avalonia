using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.GestureRecognizers;

namespace Cupertino.Controls;

public static class CarouselInteraction
{
    public static readonly AttachedProperty<bool> IsMouseEnabledProperty =
        AvaloniaProperty.RegisterAttached<Carousel, bool>("IsMouseEnabled", typeof(CarouselInteraction));

    static CarouselInteraction()
    {
        IsMouseEnabledProperty.Changed.AddClassHandler<Carousel>((carousel, e) =>
        {
            carousel.Loaded -= OnLoaded;
            if (e.GetNewValue<bool>())
            {
                carousel.Loaded += OnLoaded;
                SetMouseEnabled(carousel, true);
            }
            else
            {
                SetMouseEnabled(carousel, false);
            }
        });
    }

    public static bool GetIsMouseEnabled(Carousel carousel) =>
        carousel.GetValue(IsMouseEnabledProperty);

    public static void SetIsMouseEnabled(Carousel carousel, bool value) =>
        carousel.SetValue(IsMouseEnabledProperty, value);

    private static void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Carousel carousel)
            SetMouseEnabled(carousel, true);
    }

    private static void SetMouseEnabled(Carousel carousel, bool enabled)
    {
        if (carousel.ItemsPanelRoot is not { } panel)
            return;

        foreach (var recognizer in panel.GestureRecognizers.OfType<SwipeGestureRecognizer>())
            recognizer.IsMouseEnabled = enabled;
    }
}
