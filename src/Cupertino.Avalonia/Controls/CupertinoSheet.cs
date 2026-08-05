using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cupertino.Controls;

public enum SheetDetents
{
    Large,
    MediumAndLarge,
}

/// <summary>
/// The sheet's visual container: grabber, rounded card, content.
/// </summary>
public sealed class CupertinoSheetPresenter : ContentControl
{
}

/// <summary>
/// Presents a draggable sheet with medium and large detents.
/// </summary>
public static class CupertinoSheet
{
    public static Task ShowAsync(Visual anchor, object content,
        SheetDetents detents = SheetDetents.MediumAndLarge)
    {
        var layer = OverlayLayer.GetOverlayLayer(anchor)
            ?? throw new InvalidOperationException("CupertinoSheet needs a TopLevel with an overlay layer.");
        var top = TopLevel.GetTopLevel(anchor)
            ?? throw new InvalidOperationException("CupertinoSheet needs an attached anchor.");
        return new Session(layer, top, content, detents).Completion;
    }

    private sealed class Session
    {
        private const double Tau = 100;
        private const double ScrimOpacity = 0.20;
        private const double FloatInset = 8;
        private const double LargeTop = 10;
        private const double MediumTopFraction = 0.475;
        private const double DismissVelocity = 900;

        private readonly OverlayLayer _layer;
        private readonly TopLevel _host;
        private readonly SheetDetents _detents;
        private readonly Panel _root;
        private readonly Border _scrim;
        private readonly CupertinoSheetPresenter _presenter;
        private readonly TranslateTransform _translate = new();
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly TaskCompletionSource _done = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private Size _hostSize;
        private double _y;          // sheet top, host coordinates
        private double _target;
        private bool _dismissing;
        private bool _dragging;
        private double _grabDy;
        private double _lastY;
        private double _velocity;   // pt per tick during drags
        private bool _tornDown;
        private IPointer? _activePointer;

        public Task Completion => _done.Task;

        public Session(OverlayLayer layer, TopLevel host, object content, SheetDetents detents)
        {
            _layer = layer;
            _host = host;
            _detents = detents;
            _hostSize = host.ClientSize;

            _presenter = new CupertinoSheetPresenter
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(FloatInset, 0, FloatInset, 0),
                RenderTransform = _translate,
            };
            _scrim = new Border
            {
                Background = Brushes.Black,
                Opacity = 0,
            };
            _root = new Panel { Children = { _scrim, _presenter } };
            _layer.Children.Add(_root);
            ResizeRoot();

            _presenter.Height = Math.Max(0, HostHeight - MediumTopGeometry - FloatInset);

            _scrim.PointerPressed += OnScrimPressed;
            _presenter.AddHandler(InputElement.PointerPressedEvent, OnSheetPressed,
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
            _presenter.AddHandler(InputElement.PointerMovedEvent, OnSheetMoved,
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
            _presenter.AddHandler(InputElement.PointerReleasedEvent, OnSheetReleased,
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
            _presenter.AddHandler(InputElement.PointerCaptureLostEvent, OnSheetCaptureLost,
                Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            _root.DetachedFromVisualTree += OnRootDetached;
            _host.PropertyChanged += OnHostPropertyChanged;
            _timer.Tick += OnTick;

            var opening = _detents == SheetDetents.MediumAndLarge ? MediumTop : LargeTop;
            _target = opening;
            if (CupertinoAccessibility.ReduceMotion)
            {
                SetY(opening);
                return;
            }
            SetY(HostHeight);
            _timer.Start();
        }

        private double HostHeight => _hostSize.Height;
        private double MediumTop => HostHeight * MediumTopFraction;
        private double MediumTopGeometry => HostHeight * MediumTopFraction;

        private void ResizeRoot()
        {
            _root.Width = _hostSize.Width;
            _root.Height = _hostSize.Height;
        }

        private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_tornDown || e.Property != TopLevel.ClientSizeProperty)
                return;

            var nextSize = _host.ClientSize;
            if (nextSize == _hostSize)
                return;

            var oldHeight = HostHeight;
            var oldMedium = oldHeight * MediumTopFraction;
            var targetsMedium = _detents == SheetDetents.MediumAndLarge &&
                                Math.Abs(_target - oldMedium) <= Math.Abs(_target - LargeTop);
            var wasAnimating = _timer.IsEnabled;
            var wasDragging = _dragging;
            var scaledY = oldHeight > 0 ? _y * nextSize.Height / oldHeight : _y;

            _hostSize = nextSize;
            ResizeRoot();
            SetY(scaledY);

            if (wasDragging)
            {
                _dragging = false;
                var pointer = _activePointer;
                _activePointer = null;
                pointer?.Capture(null);
                SettleToNearest(_y);
                return;
            }

            if (_dismissing)
                _target = HostHeight + 40;
            else
                _target = targetsMedium ? MediumTop : LargeTop;

            if (!wasAnimating || CupertinoAccessibility.ReduceMotion)
                SetY(_target);
            else
                _timer.Start();
        }

        private void SetY(double y)
        {
            _y = y;
            _translate.Y = y;
            // Interpolate from an inset card to a full-bleed sheet.
            if (!_dismissing)
            {
                var geom = MediumTopGeometry;
                var p = Math.Clamp((geom - y) / Math.Max(1, geom - LargeTop), 0, 1);
                var inset = FloatInset * (1 - p);
                _presenter.Margin = new Thickness(inset, 0, inset, 0);
                _presenter.Height = y >= geom
                    ? Math.Max(0, HostHeight - geom - FloatInset)
                    : Math.Max(0, HostHeight - inset - y);
            }
            _scrim.Opacity = ScrimOpacity * Math.Clamp(
                (HostHeight - y) / Math.Max(1, HostHeight - MediumTop), 0, 1);
        }

        private void OnTick(object? sender, EventArgs e)
        {
            SetY(_y + (_target - _y) * (1 - Math.Exp(-16.0 / Tau)));
            if (Math.Abs(_y - _target) < 0.5)
            {
                SetY(_target);
                _timer.Stop();
                if (_dismissing)
                    Teardown();
            }
        }

        private void OnScrimPressed(object? sender, PointerPressedEventArgs e) => Dismiss();

        private void OnSheetPressed(object? sender, PointerPressedEventArgs e)
        {
            // Only the grabber starts a sheet drag.
            var p = e.GetPosition(_presenter);
            if (p.Y > 48)
                return;
            _dragging = true;
            _grabDy = e.GetPosition(_root).Y - _y;
            _lastY = _y;
            _velocity = 0;
            _timer.Stop();
            e.Pointer.Capture(_presenter);
            _activePointer = e.Pointer;
            e.Handled = true;
        }

        private void OnSheetMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragging)
                return;
            var y = e.GetPosition(_root).Y - _grabDy;
            // Add resistance above the tallest detent.
            if (y < LargeTop)
                y = LargeTop - (LargeTop - y) * 0.4;
            _velocity = _velocity * 0.7 + (y - _lastY) * 0.3;
            _lastY = y;
            SetY(y);
        }

        private void OnSheetReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_dragging)
                return;
            _dragging = false;
            e.Pointer.Capture(null);
            _activePointer = null;

            var flick = _velocity * 60;   // pt/s
            var biased = _y + _velocity * 6;
            if (flick > DismissVelocity || biased > (MediumTopIfAny() + HostHeight) / 2)
            {
                Dismiss();
                return;
            }
            _target = _detents == SheetDetents.MediumAndLarge
                && Math.Abs(biased - MediumTop) < Math.Abs(biased - LargeTop)
                    ? MediumTop
                    : LargeTop;
            if (CupertinoAccessibility.ReduceMotion)
                SetY(_target);
            else
                _timer.Start();
        }

        private void OnSheetCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (!_dragging)
                return;

            _dragging = false;
            _activePointer = null;
            SettleToNearest(_y);
        }

        private void SettleToNearest(double position)
        {
            _target = _detents == SheetDetents.MediumAndLarge
                && Math.Abs(position - MediumTop) < Math.Abs(position - LargeTop)
                    ? MediumTop
                    : LargeTop;
            if (CupertinoAccessibility.ReduceMotion)
                SetY(_target);
            else
                _timer.Start();
        }

        private double MediumTopIfAny() =>
            _detents == SheetDetents.MediumAndLarge ? MediumTop : LargeTop;

        private void Dismiss()
        {
            if (_tornDown || _dismissing)
                return;
            _dismissing = true;
            _target = HostHeight + 40;
            if (CupertinoAccessibility.ReduceMotion)
            {
                SetY(_target);
                Teardown();
                return;
            }
            _timer.Start();
        }

        private void Teardown()
        {
            if (_tornDown)
                return;
            _tornDown = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _scrim.PointerPressed -= OnScrimPressed;
            _presenter.RemoveHandler(InputElement.PointerPressedEvent, OnSheetPressed);
            _presenter.RemoveHandler(InputElement.PointerMovedEvent, OnSheetMoved);
            _presenter.RemoveHandler(InputElement.PointerReleasedEvent, OnSheetReleased);
            _presenter.RemoveHandler(InputElement.PointerCaptureLostEvent, OnSheetCaptureLost);
            _root.DetachedFromVisualTree -= OnRootDetached;
            _host.PropertyChanged -= OnHostPropertyChanged;
            _layer.Children.Remove(_root);
            _done.TrySetResult();
        }

        private void OnRootDetached(object? sender, VisualTreeAttachmentEventArgs e) => Teardown();
    }
}
