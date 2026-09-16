using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// An eight-spoke activity indicator with a 0.8-second cycle.
/// </summary>
public class CupertinoActivityIndicator : Control
{
    /// <summary>
    /// Identifies the <see cref="IsActive"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CupertinoActivityIndicator, bool>(nameof(IsActive), true);

    /// <summary>
    /// Identifies the <see cref="Foreground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<CupertinoActivityIndicator, IBrush>(
            nameof(Foreground), Brushes.Gray);

    /// <summary>
    /// Identifies the <see cref="SweepFraction"/> property.
    /// </summary>
    public static readonly StyledProperty<double> SweepFractionProperty =
        AvaloniaProperty.Register<CupertinoActivityIndicator, double>(
            nameof(SweepFraction), 1.0, coerce: (_, v) => Math.Clamp(v, 0, 1));

    /// <summary>
    /// Whether the indicator spins and is shown; false hides it, as <c>hidesWhenStopped</c> does.
    /// </summary>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    /// <summary>
    /// The brush used to draw the indicator spokes.
    /// </summary>
    public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    /// <summary>
    /// The fraction of spokes shown while arming a refresh; one enables the normal spin.
    /// </summary>
    public double SweepFraction { get => GetValue(SweepFractionProperty); set => SetValue(SweepFractionProperty, value); }

    private const int Spokes = 8;
    private const double InnerRatio = 0.31;
    private const double WidthRatio = 0.133;
    private const double Floor = 0.35;
    private const double Falloff = 0.185;

    private DispatcherTimer? _timer;
    private int _step;
    private Pen[]? _pens;
    private Color _penColor;
    private double _penThickness;
    private readonly List<Visual> _visibilityAncestors = new();

    static CupertinoActivityIndicator()
    {
        AffectsRender<CupertinoActivityIndicator>(ForegroundProperty, IsActiveProperty,
                                                  SweepFractionProperty);
    }

    /// <summary>
    /// Creates an activity indicator using the secondary label colour.
    /// </summary>
    public CupertinoActivityIndicator()
    {
        this.Bind(ForegroundProperty, this.GetResourceObservable("CupertinoSecondaryLabelBrush"),
                  Avalonia.Data.BindingPriority.Style);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var d = 20.0;
        if (!double.IsNaN(Width))
            d = Width;
        else if (!double.IsInfinity(availableSize.Width)) d = Math.Min(availableSize.Width, d);
        return new Size(d, d);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchAncestors();
        UpdateTimer();
    }

    // IsEffectivelyVisible has no change notification, so watch each ancestor.
    private void WatchAncestors()
    {
        UnwatchAncestors();
        foreach (var ancestor in this.GetVisualAncestors())
        {
            ancestor.PropertyChanged += OnAncestorPropertyChanged;
            _visibilityAncestors.Add(ancestor);
        }
    }

    private void UnwatchAncestors()
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnwatchAncestors();
        // Stop offscreen render ticks.
        _timer?.Stop();
        _timer = null;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty || change.Property == IsVisibleProperty
            || change.Property == SweepFractionProperty)
            UpdateTimer();
    }

    private void UpdateTimer()
    {
        var shouldRun = IsActive && SweepFraction >= 1 && IsEffectivelyVisible
                        && TopLevel.GetTopLevel(this) is not null;

        if (!shouldRun)
        {
            _timer?.Stop();
            return;
        }

        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(100),
                                       DispatcherPriority.Render,
                                       (_, _) =>
                                       {
                                           _step = (_step + 1) % Spokes;
                                           InvalidateVisual();
                                           GlassSurface.PulseBehind(this);
                                       });
        _timer.Start();
    }

    private void OnAncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
            UpdateTimer();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (!IsActive)
            return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
            return;

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2;
        var inner = radius * InnerRatio;
        var thickness = size * WidthRatio;
        var colour = Foreground is ISolidColorBrush s ? s.Color : Colors.Gray;
        EnsurePens(colour, thickness);

        // While arming, spokes materialise clockwise from the top without spinning.
        var arming = SweepFraction < 1;
        var visible = arming ? (int)Math.Ceiling(Spokes * SweepFraction) : Spokes;

        for (var i = 0; i < Spokes; i++)
        {
            var index = arming ? i : ((_step - i) % Spokes + Spokes) % Spokes;
            if (arming && i >= visible)
                continue;
            var angle = -Math.PI / 2 + 2 * Math.PI * index / Spokes;

            var pen = arming ? _pens![Spokes - 1] : _pens![i];

            var dx = Math.Cos(angle);
            var dy = Math.Sin(angle);

            var from = inner + thickness / 2;
            var to = radius - thickness / 2;
            if (to <= from)
                continue;

            context.DrawLine(pen,
                new Point(centre.X + dx * from, centre.Y + dy * from),
                new Point(centre.X + dx * to, centre.Y + dy * to));
        }
    }

    private void EnsurePens(Color colour, double thickness)
    {
        if (_pens is not null && _penColor == colour && Math.Abs(_penThickness - thickness) < 0.001)
            return;

        _penColor = colour;
        _penThickness = thickness;
        _pens = new Pen[Spokes];
        for (var i = 0; i < Spokes; i++)
        {
            var opacity = Math.Max(Floor, 1.0 - i * Falloff);
            _pens[i] = new Pen(
                new SolidColorBrush(colour, opacity),
                thickness,
                lineCap: PenLineCap.Round);
        }
    }
}
