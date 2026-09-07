using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// A cylindrical picker column.
/// </summary>
public class CupertinoWheel : Control
{
    /// <summary>
    /// Gets or sets the cylinder radius.
    /// </summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<CupertinoWheel, double>(nameof(Radius), 86.0);

    /// <summary>
    /// Gets or sets the row arc length.
    /// </summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<CupertinoWheel, double>(nameof(ItemHeight), 32.0);

    /// <summary>
    /// Identifies the <see cref="Items"/> property.
    /// </summary>
    public static readonly StyledProperty<IList<string>?> ItemsProperty =
        AvaloniaProperty.Register<CupertinoWheel, IList<string>?>(nameof(Items));

    /// <summary>
    /// Identifies the <see cref="SelectedIndex"/> property.
    /// </summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CupertinoWheel, int>(
            nameof(SelectedIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Gets or sets whether the wheel wraps.
    /// </summary>
    public static readonly StyledProperty<bool> ShouldLoopProperty =
        AvaloniaProperty.Register<CupertinoWheel, bool>(nameof(ShouldLoop), true);

    /// <summary>
    /// Identifies the <see cref="FontSize"/> property.
    /// </summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<CupertinoWheel, double>(nameof(FontSize), 23.0);

    /// <summary>
    /// Identifies the <see cref="Foreground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<CupertinoWheel, IBrush>(nameof(Foreground), Brushes.Black);

    /// <summary>
    /// Identifies the <see cref="TextAlignment"/> property.
    /// </summary>
    public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<CupertinoWheel, TextAlignment>(
            nameof(TextAlignment), TextAlignment.Center);

    /// <summary>
    /// The wheel projection radius in logical pixels, clamped to 1–1000; nonfinite values use 86.
    /// </summary>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    /// <summary>
    /// The row height in logical pixels, clamped to 1–1000; nonfinite values use 32.
    /// </summary>
    public double ItemHeight { get => GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    /// <summary>
    /// The strings displayed on the wheel. Observable collection changes are tracked while attached; replacing the list resets its motion.
    /// </summary>
    public IList<string>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    /// <summary>
    /// The selected row index. Values wrap when looping and otherwise clamp; external updates cancel an active drag or settle.
    /// </summary>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    /// <summary>
    /// Whether row indices wrap around at the beginning and end of the item list.
    /// </summary>
    public bool ShouldLoop { get => GetValue(ShouldLoopProperty); set => SetValue(ShouldLoopProperty, value); }
    /// <summary>
    /// The text size in logical pixels, clamped to 1–1000; nonfinite values use 23.
    /// </summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    /// <summary>
    /// The brush used to draw wheel row labels.
    /// </summary>
    public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    /// <summary>
    /// Horizontal alignment of each row within the wheel.
    /// </summary>
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>
    /// Raised after the wheel settles.
    /// </summary>
    public event EventHandler? SelectionSettled;

    // Integral offsets are settled rows.
    private double _offset;
    private bool _dragging;
    private double _lastY;
    private readonly Rendering.FormattedTextCache _textCache = new();
    private double? _measuredWidth;
    private (System.Globalization.CultureInfo Culture, FontFamily Family, double Size, FlowDirection Direction)? _measurementStyle;
    private double _pressY;
    private double _travelled;
    private double _velocity;
    private DateTime _lastMove;
    private DispatcherTimer? _timer;
    private double _settleTo;
    private double _springVel;
    private bool _settling;
    private bool _committingSelection;
    private int _lastTickRow = int.MinValue;
    private bool _isAttached;
    private INotifyCollectionChanged? _observableItems;
    private IPointer? _capturedPointer;

    static CupertinoWheel()
    {
        AffectsRender<CupertinoWheel>(ItemsProperty, SelectedIndexProperty, ForegroundProperty,
                                      FontSizeProperty, RadiusProperty, ItemHeightProperty,
                                      ShouldLoopProperty);
        AffectsMeasure<CupertinoWheel>(ItemsProperty, FontSizeProperty, RadiusProperty,
                                       ItemHeightProperty);
        FocusableProperty.OverrideDefaultValue<CupertinoWheel>(true);
    }

    /// <summary>
    /// Creates a CupertinoWheel with its default settings.
    /// </summary>
    public CupertinoWheel()
    {
        ClipToBounds = true;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty || change.Property == FontSizeProperty ||
            change.Property == TextElement.FontFamilyProperty || change.Property == FlowDirectionProperty)
        {
            _measuredWidth = null;
            _textCache.Clear();
            InvalidateMeasure();
        }

        if (change.Property == RadiusProperty || change.Property == ItemHeightProperty
            || change.Property == FontSizeProperty)
        {
            var property = (StyledProperty<double>)change.Property;
            var value = GetValue(property);
            var fallback = property == RadiusProperty ? 86.0 : property == ItemHeightProperty ? 32.0 : 23.0;
            var normalized = double.IsFinite(value) && value > 0
                ? Math.Clamp(value, 1.0, 1000.0)
                : fallback;
            if (value != normalized)
            {
                SetCurrentValue(property, normalized);
                return;
            }
        }

        // An external selection supersedes any gesture or spring still in flight.
        if (change.Property == SelectedIndexProperty)
        {
            var normalized = NormalizeIndex(SelectedIndex);
            if (normalized != SelectedIndex)
            {
                SetCurrentValue(SelectedIndexProperty, normalized);
                return;
            }
            if (!_committingSelection)
            {
                _timer?.Stop();
                _settling = false;
                _dragging = false;
                var pointer = _capturedPointer;
                _capturedPointer = null;
                pointer?.Capture(null);
                _offset = normalized;
                InvalidateVisual();
            }
        }
        else if (change.Property == ItemsProperty || change.Property == ShouldLoopProperty)
        {
            if (change.Property == ItemsProperty)
            {
                DisconnectItems();
                ConnectItems();
            }
            _timer?.Stop();
            _settling = false;
            var normalized = NormalizeIndex(SelectedIndex);
            if (normalized != SelectedIndex)
                SetCurrentValue(SelectedIndexProperty, normalized);
            _offset = normalized;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        ConnectItems();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        DisconnectItems();
        _textCache.Clear();
        _measuredWidth = null;
        _timer?.Stop();
        _settling = false;
        _dragging = false;
        var pointer = _capturedPointer;
        _capturedPointer = null;
        pointer?.Capture(null);
        _offset = NormalizeIndex(SelectedIndex);
        InvalidateVisual();
        base.OnDetachedFromVisualTree(e);
    }

    private void ConnectItems()
    {
        if (!_isAttached || _observableItems is not null)
            return;
        _observableItems = Items as INotifyCollectionChanged;
        if (_observableItems is not null)
            _observableItems.CollectionChanged += OnItemsCollectionChanged;
    }

    private void DisconnectItems()
    {
        if (_observableItems is not null)
            _observableItems.CollectionChanged -= OnItemsCollectionChanged;
        _observableItems = null;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _measuredWidth = null;
        _textCache.Clear();
        _timer?.Stop();
        _settling = false;
        _dragging = false;
        var pointer = _capturedPointer;
        _capturedPointer = null;
        pointer?.Capture(null);
        var normalized = NormalizeIndex(SelectedIndex);
        if (normalized != SelectedIndex)
            SetCurrentValue(SelectedIndexProperty, normalized);
        _offset = normalized;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var h = Math.Min(Radius * 2.0 + ItemHeight, 216.0);
        var style = (System.Globalization.CultureInfo.CurrentCulture, TextElement.GetFontFamily(this), FontSize, FlowDirection);
        if (_measurementStyle != style)
        {
            _measurementStyle = style;
            _measuredWidth = null;
            _textCache.Clear();
        }
        if (_measuredWidth is null)
        {
            var width = 0.0;
            if (Items is { Count: > 0 })
                foreach (var text in Items)
                    width = Math.Max(width, Measure(text).Width);
            _measuredWidth = width;
        }
        return new Size(Math.Min(_measuredWidth.Value, availableSize.Width), h);
    }

    private FormattedText Measure(string text) => _textCache.Get(
        text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection,
        new Typeface(TextElement.GetFontFamily(this)), FontSize, Foreground);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;

        // Fill the bounds for hit testing.
        context.FillRectangle(Brushes.Transparent, new Rect(bounds.Size));

        var items = Items;
        if (items is null || items.Count == 0)
            return;

        var cy = bounds.Height / 2;
        var r = Radius;
        var anglePerRow = ItemHeight / r;

        var span = (int)Math.Ceiling(Math.PI / 2 / anglePerRow) + 1;
        var centre = (int)Math.Round(_offset);

        for (var k = -span; k <= span; k++)
        {
            var index = centre + k;
            var text = TextAt(index, items);
            if (text is null)
                continue;

            var theta = (index - _offset) * anglePerRow;
            if (Math.Abs(theta) >= Math.PI / 2)
                continue;

            var y = cy + r * Math.Sin(theta);
            var scaleY = Math.Cos(theta);
            if (scaleY <= 0.01)
                continue;

            var ft = Measure(text);
            var x = TextAlignment switch
            {
                TextAlignment.Left => 0,
                TextAlignment.Right => bounds.Width - ft.Width,
                _ => (bounds.Width - ft.Width) / 2,
            };

            // Fade continuously as rows move away from the selection.
            var distance = Math.Abs(index - _offset);
            var opacity = 1.0 / (1.0 + 0.85 * Math.Pow(distance, 1.3));

            using (context.PushOpacity(opacity))
            using (context.PushTransform(
                       Matrix.CreateTranslation(0, -y) *
                       Matrix.CreateScale(1, scaleY) *
                       Matrix.CreateTranslation(0, y)))
            {
                // The wheel's layout follows RTL, while its glyphs stay upright.
                var scaleX = FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
                using (context.PushTransform(Matrix.CreateScale(scaleX, 1) *
                           Matrix.CreateTranslation(x + ft.Width / 2, y)))
                    context.DrawText(ft, new Point(-ft.Width / 2, -ft.Height / 2));
            }
        }
    }

    private void Tick()
    {
        var row = (int)Math.Round(_offset);
        if (row == _lastTickRow)
            return;

        // Skip the initial observation.
        if (_lastTickRow != int.MinValue)
            CupertinoHaptics.Play(HapticFeedback.Selection);

        _lastTickRow = row;
    }

    private string? TextAt(int index, IList<string> items)
    {
        if (ShouldLoop)
        {
            var n = items.Count;
            return items[((index % n) + n) % n];
        }
        return index >= 0 && index < items.Count ? items[index] : null;
    }

    private double Clamp(double offset)
    {
        if (ShouldLoop || Items is null || Items.Count == 0)
            return offset;
        return Math.Clamp(offset, 0, Items.Count - 1);
    }

    private int NormalizeIndex(int index)
    {
        if (Items is not { Count: > 0 } items)
            return 0;
        if (!ShouldLoop)
            return Math.Clamp(index, 0, items.Count - 1);
        return ((index % items.Count) + items.Count) % items.Count;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Items is null || Items.Count == 0)
            return;

        _timer?.Stop();
        _dragging = true;
        _settling = false;
        _velocity = 0;
        _lastY = e.GetPosition(this).Y;
        _pressY = _lastY;
        _travelled = 0;
        _lastMove = DateTime.UtcNow;
        e.PreventGestureRecognition();
        e.Pointer.Capture(this);
        _capturedPointer = e.Pointer;
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_dragging)
            return;

        _dragging = false;
        _capturedPointer = null;
        _velocity = 0;
        SettleTo(Math.Round(Clamp(_offset)));
    }

    private double? RowAt(double y)
    {
        var sin = (y - Bounds.Height / 2) / Radius;
        if (Math.Abs(sin) > 1)
            return null;
        return _offset + Math.Asin(sin) / (ItemHeight / Radius);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
            return;

        var y = e.GetPosition(this).Y;
        var dy = y - _lastY;
        var now = DateTime.UtcNow;
        var dt = (now - _lastMove).TotalSeconds;

        // Map linear drag distance to cylinder rotation.
        var dRows = -dy / ItemHeight;
        _offset = Clamp(_offset + dRows);
        _travelled += Math.Abs(dy);
        Tick();

        if (dt > 0.001)
        {
            var instant = dRows / dt;
            _velocity = _velocity * 0.7 + instant * 0.3;
        }

        _lastY = y;
        _lastMove = now;
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;

        _dragging = false;
        _capturedPointer = null;
        e.Pointer.Capture(null);

        if (_travelled < 4)
        {
            if (RowAt(_pressY) is { } row)
                SettleTo(Math.Round(Clamp(row)));
            e.Handled = true;
            return;
        }

        // Ignore stale velocity.
        if ((DateTime.UtcNow - _lastMove).TotalMilliseconds > 80)
            _velocity = 0;

        var projected = _offset + _velocity * 0.25;
        SettleTo(Math.Round(Clamp(projected)), _velocity);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Items is null || Items.Count == 0)
            return;

        var target = Math.Round(Clamp((_settling ? _settleTo : _offset) - e.Delta.Y));
        SettleTo(target);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Items is null || Items.Count == 0)
            return;

        var step = e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (step == 0)
            return;

        SettleTo(Math.Round(Clamp((_settling ? _settleTo : _offset) + step)));
        e.Handled = true;
    }

    private void SettleTo(double target) => SettleTo(target, 0);

    private void SettleTo(double target, double velocity)
    {
        // Skip visual travel when motion is reduced.
        if (CupertinoAccessibility.ReduceMotion)
        {
            _settling = false;
            _offset = target;
            CommitIndex(target);
            InvalidateVisual();
            SelectionSettled?.Invoke(this, EventArgs.Empty);
            return;
        }

        _settleTo = target;
        _springVel = velocity;
        // Set before CommitIndex to preserve the animation offset.
        _settling = true;

        // Commit immediately; animate only the visual offset.
        CommitIndex(target);

        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16),
                                       DispatcherPriority.Render, OnSettleTick);
        _timer.Start();
    }

    private void CommitIndex(double offset)
    {
        var items = Items;
        if (items is not { Count: > 0 })
            return;

        var n = items.Count;
        var idx = (int)Math.Round(offset);
        _committingSelection = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty,
                ShouldLoop ? ((idx % n) + n) % n : Math.Clamp(idx, 0, n - 1));
        }
        finally { _committingSelection = false; }
    }

    private void OnSettleTick(object? sender, EventArgs e)
    {
        if (!_settling)
            return;
        if (CupertinoAccessibility.ReduceMotion)
        {
            _offset = _settleTo;
            _springVel = 0;
        }
        // Critically damped spring carrying the release velocity.
        const double dt = 0.016;
        const double omega = 10.0;
        var d = _offset - _settleTo;
        var accel = -omega * omega * d - 2 * omega * _springVel;
        _springVel += accel * dt;
        _offset += _springVel * dt;
        Tick();
        InvalidateVisual();

        if (Math.Abs(_offset - _settleTo) < 0.005 && Math.Abs(_springVel) < 0.02)
        {
            _timer?.Stop();
            _offset = _settleTo;
            _springVel = 0;
            _settling = false;

            CommitIndex(_offset);
            SelectionSettled?.Invoke(this, EventArgs.Empty);
        }
    }

}
