using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cupertino.Controls;

/// <summary>
/// A row with leading, trailing and full-swipe actions.
/// </summary>
public class CupertinoSwipeView : ContentControl
{
    public static readonly StyledProperty<object?> LeadingActionsProperty =
        AvaloniaProperty.Register<CupertinoSwipeView, object?>(nameof(LeadingActions));

    public static readonly StyledProperty<object?> TrailingActionsProperty =
        AvaloniaProperty.Register<CupertinoSwipeView, object?>(nameof(TrailingActions));

    public object? LeadingActions
    {
        get => GetValue(LeadingActionsProperty);
        set => SetValue(LeadingActionsProperty, value);
    }

    public object? TrailingActions
    {
        get => GetValue(TrailingActionsProperty);
        set => SetValue(TrailingActionsProperty, value);
    }

    private const double DragThreshold = 3;
    private const double CommitFraction = 0.9;
    private const double ReleaseProjectionSeconds = 0.2;
    private const double SettleOmega = 14;

    private ContentPresenter? _content;
    private ContentPresenter? _leading;
    private ContentPresenter? _trailing;
    private readonly TranslateTransform _shift = new();
    private readonly DispatcherTimer _settle;

    private bool _pressed;
    private bool _dragging;
    private Point _press;
    private double _position;
    private double _rawPosition;
    private double _openAt;
    private double _target;
    private double _settleVelocity;
    private double _dragVelocity;
    private double _lastDragX;
    private DateTime _lastDragMove;
    private double _leadingNaturalWidth = double.NaN;
    private double _trailingNaturalWidth = double.NaN;

    // Only one row can remain open.
    private static CupertinoSwipeView? s_open;

    public CupertinoSwipeView()
    {
        _settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _settle.Tick += OnSettleTick;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _content = e.NameScope.Find<ContentPresenter>("PART_Content");
        _leading = e.NameScope.Find<ContentPresenter>("PART_Leading");
        _trailing = e.NameScope.Find<ContentPresenter>("PART_Trailing");
        ResetNaturalWidths();
        if (_content is not null)
            _content.RenderTransform = _shift;
        UpdateDirectionHosts();
        if (_openAt == 0)
        {
            ApplyPosition(0);
            SetRevealed(0);
        }
        else
        {
            OpenActions(leading: _openAt > 0);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pressed = false;
        _dragging = false;
        _settle.Stop();
        ApplyPosition(0);
        _openAt = 0;
        SetRevealed(0);
        if (s_open == this)
            s_open = null;
    }

    /// <summary>
    /// Closes the revealed actions.
    /// </summary>
    public void Close()
    {
        if (s_open == this)
            s_open = null;
        _openAt = 0;
        StartSettle(0);
    }

    /// <summary>
    /// Reveals the leading actions.
    /// </summary>
    public void OpenLeadingActions() => OpenActions(leading: true);

    /// <summary>
    /// Reveals the trailing actions.
    /// </summary>
    public void OpenTrailingActions() => OpenActions(leading: false);

    private void OpenActions(bool leading)
    {
        if (leading ? LeadingActions is null : TrailingActions is null)
        {
            if (s_open == this)
                s_open = null;
            _openAt = 0;
            StartSettle(0);
            return;
        }

        if (s_open != this)
            s_open?.Close();
        s_open = this;
        ResetNaturalWidths();
        _openAt = leading ? LeadingWidth : -TrailingWidth;
        StartSettle(_openAt);
    }

    private static double NaturalWidth(ContentPresenter? host, ref double cachedWidth, double height)
    {
        if (double.IsFinite(cachedWidth))
            return cachedWidth;
        if (host is null)
            return cachedWidth = 74;

        // Measure without the temporary reveal width; hidden hosts measure as zero.
        var wasVisible = host.IsVisible;
        if (!wasVisible)
            host.IsVisible = true;
        host.ClearValue(WidthProperty);
        host.ApplyTemplate();
        var child = host.Child;
        child?.InvalidateMeasure();
        host.InvalidateMeasure();
        host.Measure(new Size(double.PositiveInfinity, Math.Max(1, height)));
        var width = Math.Max(74, Math.Max(
            host.DesiredSize.Width, child?.DesiredSize.Width ?? 0));
        if (!wasVisible)
            host.IsVisible = false;
        return cachedWidth = width;
    }

    private double LeadingWidth => NaturalWidth(_leading, ref _leadingNaturalWidth, Bounds.Height);
    private double TrailingWidth => NaturalWidth(_trailing, ref _trailingNaturalWidth, Bounds.Height);
    // Avalonia already mirrors pointer and child coordinates in RTL.
    private const double DirectionSign = 1;

    private void ApplyPosition(double logicalPosition)
    {
        _position = logicalPosition;
        _shift.X = logicalPosition * DirectionSign;
    }

    private void UpdateDirectionHosts()
    {
        if (_leading is not null)
        {
            _leading.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            _leading.HorizontalContentAlignment = _leading.HorizontalAlignment;
        }
        if (_trailing is not null)
        {
            _trailing.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            _trailing.HorizontalContentAlignment = _trailing.HorizontalAlignment;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FlowDirectionProperty)
        {
            UpdateDirectionHosts();
            ApplyPosition(_position);
        }
        else if (change.Property == LeadingActionsProperty || change.Property == TrailingActionsProperty)
        {
            ResetNaturalWidths();
            // TemplateBinding updates the child after this callback.
            Dispatcher.UIThread.Post(() =>
            {
                ResetNaturalWidths();
                if (_openAt == 0)
                    SetRevealed(_position);
                else
                    OpenActions(leading: _openAt > 0);
            }, DispatcherPriority.Loaded);
        }
    }

    private void ResetNaturalWidths()
    {
        _leadingNaturalWidth = double.NaN;
        _trailingNaturalWidth = double.NaN;
        _leading?.ClearValue(WidthProperty);
        _trailing?.ClearValue(WidthProperty);
    }

    private void SetRevealed(double x)
    {
        if (_leading is not null)
        {
            _leading.IsVisible = x > 0.5;
            if (x > 0.5)
            {
                _leading.Width = LeadingWidth;
                SetRevealHint(_leading, x / Math.Max(1, LeadingWidth), trailing: false);
            }
            else
            {
                _leading.ClearValue(WidthProperty);
            }
        }
        if (_trailing is not null)
        {
            _trailing.IsVisible = x < -0.5;
            if (x < -0.5)
            {
                _trailing.Width = TrailingWidth;
                SetRevealHint(_trailing, -x / Math.Max(1, TrailingWidth), trailing: true);
            }
            else
            {
                _trailing.ClearValue(WidthProperty);
            }
        }
    }

    // Actions materialise while the reveal grows, as UIKit's do.
    private static void SetRevealHint(ContentPresenter host, double t, bool trailing)
    {
        if (CupertinoAccessibility.ReduceMotion || t >= 1)
        {
            host.Opacity = 1;
            host.RenderTransform = null;
            return;
        }
        t = Math.Clamp(t, 0, 1);
        host.Opacity = Math.Pow(t, 0.7);
        host.RenderTransformOrigin = new RelativePoint(trailing ? 1 : 0, 0.5, RelativeUnit.Relative);
        var scale = 0.8 + 0.2 * t;
        host.RenderTransform = new ScaleTransform(scale, scale);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = true;
        _dragging = false;
        _press = e.GetPosition(this);
        _rawPosition = _openAt;
        _dragVelocity = 0;
        _lastDragX = _position;
        _lastDragMove = DateTime.UtcNow;
        _settle.Stop();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pressed)
            return;

        var p = e.GetPosition(this);
        var dx = (p.X - _press.X) * DirectionSign;
        if (!_dragging)
        {
            if (Math.Abs(dx) < DragThreshold || Math.Abs(dx) < Math.Abs(p.Y - _press.Y))
                return;
            _dragging = true;
            if (s_open != this)
                s_open?.Close();
            s_open = this;
            e.PreventGestureRecognition();
            e.Pointer.Capture(this);
        }

        var x = _openAt + dx;
        var limit = x > 0 ? (LeadingActions is null ? 0 : Bounds.Width)
                          : (TrailingActions is null ? 0 : Bounds.Width);
        x = Math.Clamp(x, -limit, limit);
        _rawPosition = x;
        var reach = x > 0 ? LeadingWidth : TrailingWidth;
        if (Math.Abs(x) > reach)
            x = Math.Sign(x) * (reach + (Math.Abs(x) - reach) * 0.55);

        var now = DateTime.UtcNow;
        var dt = (now - _lastDragMove).TotalSeconds;
        if (dt > 0.001)
            _dragVelocity = _dragVelocity * 0.7 + (x - _lastDragX) / dt * 0.3;
        _lastDragX = x;
        _lastDragMove = now;

        ApplyPosition(x);
        SetRevealed(x);
    }

    private bool IsCommit() =>
        Math.Abs(_rawPosition) > Bounds.Width * CommitFraction;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
            return;
        _pressed = false;

        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);

        var x = _position;
        if (IsCommit())
        {
            var host = x < 0 ? _trailing : _leading;
            if (FindOutermostButton(host, trailing: x < 0) is { } button)
            {
                CupertinoHaptics.Play(HapticFeedback.ImpactMedium);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            if (s_open == this)
                s_open = null;
            _openAt = 0;
            StartSettle(0, _dragVelocity);
            return;
        }

        var side = Math.Sign(x);
        var reach = side > 0 ? LeadingWidth : TrailingWidth;
        var projectedReveal = (x + _dragVelocity * ReleaseProjectionSeconds) * side;
        _openAt = projectedReveal > reach * 0.5 ? side * reach : 0;
        if (_openAt == 0 && s_open == this)
            s_open = null;
        StartSettle(_openAt, _dragVelocity);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_pressed && !_dragging)
            return;

        _pressed = false;
        _dragging = false;
        if (_openAt == 0 && s_open == this)
            s_open = null;
        StartSettle(_openAt);
    }

    private static Button? FindOutermostButton(ContentPresenter? host, bool trailing)
    {
        if (host?.Child is not { } root)
            return null;
        if (root is Button single)
            return single;
        if (root is Panel panel && panel.Children.Count > 0)
        {
            for (var i = trailing ? panel.Children.Count - 1 : 0;
                 i >= 0 && i < panel.Children.Count;
                 i += trailing ? -1 : 1)
                if (panel.Children[i] is Button b)
                    return b;
        }
        return null;
    }

    private void StartSettle(double target) => StartSettle(target, 0);

    private void StartSettle(double target, double velocity)
    {
        _target = target;
        var distance = target - _position;
        _settleVelocity = velocity * distance > 0
            ? Math.Sign(distance) * Math.Min(Math.Abs(velocity), SettleOmega * Math.Abs(distance))
            : 0;
        if (CupertinoAccessibility.ReduceMotion)
        {
            ApplyPosition(target);
            SetRevealed(target);
        }
        else
        {
            _settle.Start();
        }
        // First measures can run while the host is hidden; re-check once laid out.
        if (target != 0)
            Dispatcher.UIThread.Post(ResyncOpenReach, DispatcherPriority.Loaded);
    }

    private void ResyncOpenReach()
    {
        if (_openAt == 0 || _dragging)
            return;
        var leading = _openAt > 0;
        ResetNaturalWidths();
        var reach = leading ? LeadingWidth : TrailingWidth;
        var target = leading ? reach : -reach;
        if (Math.Abs(target - _openAt) < 0.5)
            return;
        _openAt = target;
        _target = target;
        if (CupertinoAccessibility.ReduceMotion)
        {
            ApplyPosition(target);
            SetRevealed(target);
        }
        else
        {
            _settle.Start();
        }
    }

    private void OnSettleTick(object? sender, EventArgs e)
    {
        // Critically damped spring carrying the release velocity.
        const double dt = 0.016;
        var d = _position - _target;
        var accel = -SettleOmega * SettleOmega * d - 2 * SettleOmega * _settleVelocity;
        _settleVelocity += accel * dt;
        ApplyPosition(_position + _settleVelocity * dt);
        if (Math.Abs(_position - _target) < 0.5 && Math.Abs(_settleVelocity) < 4)
        {
            ApplyPosition(_target);
            _settle.Stop();
            _settleVelocity = 0;
        }
        SetRevealed(_position);
    }
}
