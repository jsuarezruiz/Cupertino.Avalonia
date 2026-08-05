using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Displays and selects pages using dots.
/// </summary>
public class CupertinoPageControl : Control
{
    public static readonly StyledProperty<int> NumberOfPagesProperty =
        AvaloniaProperty.Register<CupertinoPageControl, int>(nameof(NumberOfPages), 1);

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<CupertinoPageControl, int>(
            nameof(CurrentPage), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> HidesForSinglePageProperty =
        AvaloniaProperty.Register<CupertinoPageControl, bool>(nameof(HidesForSinglePage), true);

    public static readonly StyledProperty<bool> AllowsContinuousInteractionProperty =
        AvaloniaProperty.Register<CupertinoPageControl, bool>(nameof(AllowsContinuousInteraction), true);

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<CupertinoPageControl, double>(nameof(DotSize), 7);

    public static readonly StyledProperty<double> DotSpacingProperty =
        AvaloniaProperty.Register<CupertinoPageControl, double>(nameof(DotSpacing), 9);

    public static readonly StyledProperty<IBrush> ActiveBrushProperty =
        AvaloniaProperty.Register<CupertinoPageControl, IBrush>(nameof(ActiveBrush), Brushes.Black);

    public static readonly StyledProperty<IBrush> InactiveBrushProperty =
        AvaloniaProperty.Register<CupertinoPageControl, IBrush>(nameof(InactiveBrush), Brushes.Gray);

    public int NumberOfPages { get => GetValue(NumberOfPagesProperty); set => SetValue(NumberOfPagesProperty, value); }
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public bool HidesForSinglePage { get => GetValue(HidesForSinglePageProperty); set => SetValue(HidesForSinglePageProperty, value); }
    public bool AllowsContinuousInteraction { get => GetValue(AllowsContinuousInteractionProperty); set => SetValue(AllowsContinuousInteractionProperty, value); }
    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }
    public double DotSpacing { get => GetValue(DotSpacingProperty); set => SetValue(DotSpacingProperty, value); }
    public IBrush ActiveBrush { get => GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public IBrush InactiveBrush { get => GetValue(InactiveBrushProperty); set => SetValue(InactiveBrushProperty, value); }

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

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Math.Max(0, NumberOfPages);
        if (count == 0 || (count == 1 && HidesForSinglePage))
            return default;
        var width = count * DotSize + Math.Max(0, count - 1) * DotSpacing;
        return new Size(Math.Min(width, availableSize.Width), Math.Min(Math.Max(20, DotSize), availableSize.Height));
    }

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
            AutomationProperties.SetHelpText(this, $"{CurrentPage + 1} of {Math.Max(1, NumberOfPages)}");
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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_tracking && AllowsContinuousInteraction)
            SelectAt(e.GetPosition(this));
    }

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

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _tracking = false;
    }

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
