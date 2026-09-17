using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cupertino.Controls;

/// <summary>
/// Adds rubber-band overscroll to a <see cref="ScrollViewer"/>.
/// </summary>
public sealed class ScrollBounce
{
    // Wheel streams have no end event, so an idle gap ends the gesture.
    private const double Resistance = 0.55;
    private const double LineHeight = 50;
    private const int IdleMs = 90;
    private const double DecayRate = 12;

    private ScrollBounce()
    {
    }

    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollBounce, ScrollViewer, bool>("IsEnabled");

    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<ScrollBounce, ScrollViewer, State?>("State");
    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(ScrollViewer viewer) => viewer.GetValue(IsEnabledProperty);
    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(ScrollViewer viewer, bool value) => viewer.SetValue(IsEnabledProperty, value);

    static ScrollBounce()
    {
        IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>((viewer, e) =>
        {
            if (e.GetNewValue<bool>())
                viewer.SetValue(StateProperty, new State(viewer));
            else if (viewer.GetValue(StateProperty) is { } state)
            {
                state.Dispose();
                viewer.SetValue(StateProperty, null);
            }
        });
    }

    private sealed class State : IDisposable
    {
        private readonly ScrollViewer _viewer;
        private readonly TranslateTransform _shift = new();
        private readonly DispatcherTimer _timer;
        private Control? _presenter;
        private ITransform? _originalTransform;
        private ITransform? _appliedTransform;
        private double _pull;
        private long _lastWheel;
        private long _lastTick;

        public State(ScrollViewer viewer)
        {
            _viewer = viewer;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTick;
            viewer.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
            viewer.DetachedFromVisualTree += OnDetached;
        }

        public void Dispose()
        {
            _viewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
            _viewer.DetachedFromVisualTree -= OnDetached;
            _timer.Tick -= OnTick;
            _timer.Stop();
            _pull = 0;
            RestoreTransform();
        }

        // Stop timers that would retain detached pages.
        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _timer.Stop();
            _pull = 0;
            RestoreTransform();
        }

        private void OnWheel(object? sender, PointerWheelEventArgs e)
        {
            if (CupertinoAccessibility.ReduceMotion || Math.Abs(e.Delta.Y) < Math.Abs(e.Delta.X))
                return;

            var px = e.Delta.Y * LineHeight;
            var atTop = _viewer.Offset.Y <= 0.5;
            var atBottom = _viewer.Offset.Y >= _viewer.ScrollBarMaximum.Y - 0.5;

            if (_pull != 0)
            {
                var next = _pull + px;
                _pull = Math.Sign(next) == Math.Sign(_pull) ? next : 0;
                e.Handled = true;
            }
            else if ((px > 0 && atTop) || (px < 0 && atBottom))
            {
                _pull = px;
                e.Handled = true;
            }
            else
            {
                return;
            }

            _lastWheel = MotionClock.Now;
            Apply();
            _lastTick = MotionClock.Now;
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var elapsed = MotionClock.TakeElapsedSeconds(ref _lastTick);
            if (MotionClock.MillisecondsSince(_lastWheel) < IdleMs)
                return;

            _pull *= Math.Exp(-DecayRate * elapsed);
            if (Math.Abs(Rubber(_pull)) < 0.4)
            {
                _pull = 0;
                _timer.Stop();
                RestoreTransform();
                return;
            }
            Apply();
        }

        private double Rubber(double pull)
        {
            var dim = Math.Max(1, _viewer.Viewport.Height);
            return Math.Sign(pull) * dim * (1 - 1 / (Resistance * Math.Abs(pull) / dim + 1));
        }

        private void Apply()
        {
            if (_viewer.Presenter is { } presenter)
            {
                if (!ReferenceEquals(_presenter, presenter)
                    || !ReferenceEquals(presenter.RenderTransform, _appliedTransform))
                {
                    RestoreTransform();
                    _presenter = presenter;
                    _originalTransform = presenter.RenderTransform;
                    if (_originalTransform is { } original)
                    {
                        var transforms = new TransformGroup();
                        transforms.Children.Add(new MatrixTransform(original.Value));
                        transforms.Children.Add(_shift);
                        _appliedTransform = transforms;
                    }
                    else
                    {
                        _appliedTransform = _shift;
                    }
                    presenter.RenderTransform = _appliedTransform;
                }
                _shift.Y = Rubber(_pull);
            }
        }

        private void RestoreTransform()
        {
            if (_presenter is { } presenter
                && ReferenceEquals(presenter.RenderTransform, _appliedTransform))
            {
                presenter.RenderTransform = _originalTransform;
            }

            _presenter = null;
            _originalTransform = null;
            _appliedTransform = null;
            _shift.Y = 0;
        }
    }
}
