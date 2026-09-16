using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Displays and selects pages using dots.
/// </summary>
public class CupertinoPageControl : Control
{
    /// <summary>
    /// Identifies the <see cref="NumberOfPages"/> property.
    /// </summary>
    public static readonly StyledProperty<int> NumberOfPagesProperty =
        AvaloniaProperty.Register<CupertinoPageControl, int>(nameof(NumberOfPages), 1);

    /// <summary>
    /// Identifies the <see cref="CurrentPage"/> property.
    /// </summary>
    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<CupertinoPageControl, int>(
            nameof(CurrentPage), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="HidesForSinglePage"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> HidesForSinglePageProperty =
        AvaloniaProperty.Register<CupertinoPageControl, bool>(nameof(HidesForSinglePage), true);

    /// <summary>
    /// Identifies the <see cref="AllowsContinuousInteraction"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> AllowsContinuousInteractionProperty =
        AvaloniaProperty.Register<CupertinoPageControl, bool>(nameof(AllowsContinuousInteraction), true);

    /// <summary>
    /// Identifies the <see cref="DotSize"/> property.
    /// </summary>
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<CupertinoPageControl, double>(nameof(DotSize), 7);

    /// <summary>
    /// Identifies the <see cref="DotSpacing"/> property.
    /// </summary>
    public static readonly StyledProperty<double> DotSpacingProperty =
        AvaloniaProperty.Register<CupertinoPageControl, double>(nameof(DotSpacing), 9);

    /// <summary>
    /// Identifies the <see cref="ActiveBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> ActiveBrushProperty =
        AvaloniaProperty.Register<CupertinoPageControl, IBrush>(nameof(ActiveBrush), Brushes.Black);

    /// <summary>
    /// Identifies the <see cref="InactiveBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> InactiveBrushProperty =
        AvaloniaProperty.Register<CupertinoPageControl, IBrush>(nameof(InactiveBrush), Brushes.Gray);

    /// <summary>
    /// The page count, normalized to a nonnegative value. Updating it also clamps CurrentPage.
    /// </summary>
    public int NumberOfPages { get => GetValue(NumberOfPagesProperty); set => SetValue(NumberOfPagesProperty, value); }
    /// <summary>
    /// The zero-based selected page, clamped to the available page range.
    /// </summary>
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    /// <summary>
    /// Hides the indicator when there is no more than one page.
    /// </summary>
    public bool HidesForSinglePage { get => GetValue(HidesForSinglePageProperty); set => SetValue(HidesForSinglePageProperty, value); }
    /// <summary>
    /// Updates the selected page continuously while dragging across the dots.
    /// </summary>
    public bool AllowsContinuousInteraction { get => GetValue(AllowsContinuousInteractionProperty); set => SetValue(AllowsContinuousInteractionProperty, value); }
    /// <summary>
    /// The dot diameter in logical pixels.
    /// </summary>
    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }
    /// <summary>
    /// The gap between adjacent dots in logical pixels.
    /// </summary>
    public double DotSpacing { get => GetValue(DotSpacingProperty); set => SetValue(DotSpacingProperty, value); }
    /// <summary>
    /// The brush for the selected page dot.
    /// </summary>
    public IBrush ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    /// <summary>
    /// The brush for the unselected page dots.
    /// </summary>
    public IBrush InactiveBrush { get => GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }

    /// <summary>
    /// Raised when the selected page changes, including changes caused by page-count coercion.
    /// </summary>
    public event EventHandler? CurrentPageChanged;

    private bool _tracking;

    static CupertinoPageControl()
    {
        AffectsMeasure<CupertinoPageControl>(NumberOfPagesProperty, HidesForSinglePageProperty,
            DotSizeProperty, DotSpacingProperty);
        AffectsRender<CupertinoPageControl>(NumberOfPagesProperty, CurrentPageProperty,
            HidesForSinglePageProperty, DotSizeProperty, DotSpacingProperty,
            ActiveBrushProperty, InactiveBrushProperty);
        FocusableProperty.OverrideDefaultValue<CupertinoPageControl>(true);
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new CupertinoPageControlAutomationPeer(this);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Math.Max(0, NumberOfPages);
        if (count == 0 || (count == 1 && HidesForSinglePage))
            return default;
        var width = count * DotSize + Math.Max(0, count - 1) * DotSpacing;
        return new Size(Math.Min(width, availableSize.Width), Math.Min(Math.Max(20, DotSize), availableSize.Height));
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var count = Math.Max(0, NumberOfPages);
        if (count == 0 || (count == 1 && HidesForSinglePage))
            return;

        var contentWidth = count * DotSize + Math.Max(0, count - 1) * DotSpacing;
        var origin = (Bounds.Width - contentWidth) / 2;
        var radius = DotSize / 2;
        var selected = Math.Clamp(CurrentPage, 0, count - 1);
        for (var logical = 0; logical < count; logical++)
        {
            var center = new Point(origin + radius + logical * (DotSize + DotSpacing), Bounds.Height / 2);
            context.DrawEllipse(logical == selected ? ActiveBrush : InactiveBrush, null, center, radius, radius);
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NumberOfPagesProperty)
        {
            var pages = Math.Max(0, NumberOfPages);
            if (pages != NumberOfPages)
                SetCurrentValue(NumberOfPagesProperty, pages);
            CoerceCurrentPage(raise: true);
        }
        else if (change.Property == CurrentPageProperty)
        {
            if (CoerceCurrentPage(raise: false))
                return;
            var format = this.TryFindResource("CupertinoPageControlFormat", out var value) && value is string text
                ? text
                : "{0} of {1}";
            AutomationProperties.SetHelpText(this, string.Format(
                System.Globalization.CultureInfo.CurrentCulture, format, CurrentPage + 1, Math.Max(1, NumberOfPages)));
            CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool CoerceCurrentPage(bool raise)
    {
        var next = Math.Clamp(CurrentPage, 0, Math.Max(0, NumberOfPages - 1));
        if (next != CurrentPage)
        {
            SetCurrentValue(CurrentPageProperty, next);
            return true;
        }
        else if (raise)
            InvalidateVisual();
        return false;
    }

    private void SelectAt(Point point)
    {
        if (NumberOfPages <= 0)
            return;
        var contentWidth = NumberOfPages * DotSize + Math.Max(0, NumberOfPages - 1) * DotSpacing;
        var origin = (Bounds.Width - contentWidth) / 2;
        var pitch = Math.Max(1, DotSize + DotSpacing);
        // Pointer coordinates are already logical under Avalonia's RTL transform.
        var logical = Math.Clamp((int)Math.Round((point.X - origin - DotSize / 2) / pitch), 0, NumberOfPages - 1);
        SetCurrentValue(CurrentPageProperty, logical);
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (NumberOfPages <= 1 && HidesForSinglePage)
            return;
        _tracking = true;
        SelectAt(e.GetPosition(this));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_tracking && AllowsContinuousInteraction)
            SelectAt(e.GetPosition(this));
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_tracking)
            return;
        if (!AllowsContinuousInteraction)
            SelectAt(e.GetPosition(this));
        _tracking = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _tracking = false;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var delta = e.Key switch
        {
            Key.Left => FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            Key.Right => FlowDirection == FlowDirection.RightToLeft ? -1 : 1,
            _ => 0,
        };
        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => Math.Max(0, NumberOfPages - 1),
            _ => Math.Clamp(CurrentPage + delta, 0, Math.Max(0, NumberOfPages - 1)),
        };
        if (delta == 0 && e.Key is not (Key.Home or Key.End))
            return;
        SetCurrentValue(CurrentPageProperty, next);
        e.Handled = true;
    }
}

internal sealed class CupertinoPageControlAutomationPeer : ControlAutomationPeer, IRangeValueProvider
{
    public CupertinoPageControlAutomationPeer(CupertinoPageControl owner) : base(owner)
    {
    }

    private new CupertinoPageControl Owner => (CupertinoPageControl)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    public bool IsReadOnly => !Owner.IsEffectivelyEnabled;

    public double Minimum => 0;

    public double Maximum => Math.Max(0, Owner.NumberOfPages - 1);

    public double Value => Owner.CurrentPage;

    public double SmallChange => 1;

    public double LargeChange => 1;

    public void SetValue(double value)
    {
        if (IsReadOnly)
            return;
        Owner.SetCurrentValue(CupertinoPageControl.CurrentPageProperty,
            (int)Math.Clamp(Math.Round(value), Minimum, Maximum));
    }
}
