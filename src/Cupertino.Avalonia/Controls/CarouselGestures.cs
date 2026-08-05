using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cupertino.Controls;

/// <summary>
/// Adds pointer paging to a carousel.
/// </summary>
public static class CarouselGestures
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(CarouselGestures));

    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, State?>("State", typeof(CarouselGestures));

    static CarouselGestures()
    {
        IsEnabledProperty.Changed.AddClassHandler<Carousel>((carousel, e) =>
        {
            carousel.GetValue(StateProperty)?.Dispose();
            carousel.SetValue(StateProperty,
                e.GetNewValue<bool>() ? new State(carousel) : null);
        });
    }

    public static void SetIsEnabled(Control element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(Control element) =>
        element.GetValue(IsEnabledProperty);

    private sealed class State : IDisposable
    {
        private const double Threshold = 40;
        private const double FlickVelocity = 300;

        private readonly Carousel _owner;
        private bool _pressed;
        private bool _swiping;
        private double _startX;
        private double _startY;
        private double _lastX;
        private double _velocity;
        private DateTime _lastMove;

        public State(Carousel owner)
        {
            _owner = owner;
            _owner.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
            _owner.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
            _owner.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
            _owner.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost,
                RoutingStrategies.Bubble, handledEventsToo: true);
            _owner.DetachedFromVisualTree += OnDetached;
        }

        private void OnPressed(object? sender, PointerPressedEventArgs e)
        {
            _pressed = true;
            _swiping = false;
            _velocity = 0;
            var point = e.GetPosition(_owner);
            _startX = _lastX = point.X;
            _startY = point.Y;
            _lastMove = DateTime.UtcNow;
        }

        private void OnMoved(object? sender, PointerEventArgs e)
        {
            if (!_pressed)
                return;
            var point = e.GetPosition(_owner);
            var x = point.X;
            var dx = x - _startX;
            var dy = point.Y - _startY;
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastMove).TotalSeconds;
            if (elapsed > 0.001)
                _velocity = _velocity * 0.7 + (x - _lastX) / elapsed * 0.3;
            _lastX = x;
            _lastMove = now;
            if (!_swiping && Math.Max(Math.Abs(dx), Math.Abs(dy)) > 8)
            {
                if (Math.Abs(dx) <= Math.Abs(dy))
                {
                    _pressed = false;
                    return;
                }
                _swiping = true;
                e.Pointer.Capture(_owner);
            }
        }

        private void OnReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_pressed)
                return;
            _pressed = false;
            if (!_swiping)
                return;
            _swiping = false;
            e.Pointer.Capture(null);
            var dx = _lastX - _startX;
            if (dx < -Threshold || _velocity < -FlickVelocity)
                _owner.Next();
            else if (dx > Threshold || _velocity > FlickVelocity)
                _owner.Previous();
        }

        private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_pressed || _swiping)
                Reset();
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Reset();

        private void Reset()
        {
            _pressed = false;
            _swiping = false;
            _velocity = 0;
        }

        public void Dispose()
        {
            _owner.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
            _owner.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
            _owner.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
            _owner.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
            _owner.DetachedFromVisualTree -= OnDetached;
            Reset();
        }
    }
}
