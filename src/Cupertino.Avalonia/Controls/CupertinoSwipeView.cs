using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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

    public static readonly StyledProperty<SwipeViewState> SwipeStateProperty =
        AvaloniaProperty.Register<CupertinoSwipeView, SwipeViewState>(
            nameof(SwipeState),
            SwipeViewState.Closed,
            defaultBindingMode: BindingMode.TwoWay);

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

    /// <summary>
    /// Gets or sets which edge's actions are visible at rest.
    /// </summary>
    public SwipeViewState SwipeState
    {
        get => GetValue(SwipeStateProperty);
        set => SetValue(SwipeStateProperty, value);
    }

    private const double DragThreshold = 3;
    private const double CommitFraction = 0.6;
    private const double SettleRate = 14;

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
        ApplySwipeState(SwipeState);
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplySwipeState(SwipeState);
    }

    /// <summary>
    /// Closes the revealed actions.
    /// </summary>
    public void Close() => SetSwipeState(SwipeViewState.Closed);

    private void SetSwipeState(SwipeViewState state)
    {
        if (SwipeState == state)
            ApplySwipeState(state);
        else
            SetCurrentValue(SwipeStateProperty, state);
    }

    private void ApplySwipeState(SwipeViewState state)
    {
        if (state == SwipeViewState.Closed)
        {
            if (s_open == this)
                s_open = null;
            _openAt = 0;
            StartSettle(0);
            return;
        }

        OpenActions(leading: state == SwipeViewState.LeadingVisible);
    }

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

        // Measure without the temporary reveal width.
        host.ClearValue(WidthProperty);
        host.Measure(new Size(double.PositiveInfinity, Math.Max(1, height)));
        return cachedWidth = Math.Max(74, host.DesiredSize.Width);
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
        else if (change.Property == SwipeStateProperty)
        {
            ApplySwipeState(change.GetNewValue<SwipeViewState>());
        }
        else if (change.Property == LeadingActionsProperty || change.Property == TrailingActionsProperty)
        {
            ResetNaturalWidths();
            // TemplateBinding updates the child after this callback.
            Dispatcher.UIThread.Post(() =>
            {
                ResetNaturalWidths();
                ApplySwipeState(SwipeState);
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
                _leading.Width = Math.Max(LeadingWidth, x);
                _leading.Background = FindOutermostButton(_leading, trailing: false)?.Background;
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
                _trailing.Width = Math.Max(TrailingWidth, -x);
                _trailing.Background = FindOutermostButton(_trailing, trailing: true)?.Background;
            }
            else
            {
                _trailing.ClearValue(WidthProperty);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = true;
        _dragging = false;
        _press = e.GetPosition(this);
        _rawPosition = _openAt;
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

        ApplyPosition(x);
        SetRevealed(x);
    }

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
        if (Math.Abs(_rawPosition) > Bounds.Width * CommitFraction)
        {
            var host = x < 0 ? _trailing : _leading;
            if (FindOutermostButton(host, trailing: x < 0) is { } button)
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Close();
            return;
        }

        var reach = x > 0 ? LeadingWidth : TrailingWidth;
        var state = Math.Abs(x) > reach * 0.5
            ? (x > 0 ? SwipeViewState.LeadingVisible : SwipeViewState.TrailingVisible)
            : SwipeViewState.Closed;
        SetSwipeState(state);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_pressed && !_dragging)
            return;

        _pressed = false;
        _dragging = false;
        SetSwipeState(SwipeState);
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

    private void StartSettle(double target)
    {
        _target = target;
        if (CupertinoAccessibility.ReduceMotion)
        {
            ApplyPosition(target);
            SetRevealed(target);
            return;
        }
        _settle.Start();
    }

    private void OnSettleTick(object? sender, EventArgs e)
    {
        var next = _position + (_target - _position) * (1 - Math.Exp(-SettleRate * 0.016));
        ApplyPosition(next);
        if (Math.Abs(_position - _target) < 0.5)
        {
            ApplyPosition(_target);
            _settle.Stop();
        }
        SetRevealed(_position);
    }
}
