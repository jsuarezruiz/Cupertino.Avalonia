using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// Drives the tab bar's shared, draggable selection indicator.
/// </summary>
public static class TabBarInteraction
{
    // Measured on iOS 26 at a 402 pt viewport.
    private const double DragThreshold = 3;
    private const double SnapMilliseconds = 320;
    private const double BottomLensOverflow = 3;
    private const double TwoItemLensOverflow = 4;
    private const double BottomItemWidth = 86;
    private const double TwoItemWidth = 51;
    private const double BottomBarMargins = 42;
    private const double BottomBarInsets = 14;
    private const double TwoItemBarInsets = 16;
    private const double BottomAccessoryWidth = 74;
    private const string TwoItemClass = "cupertino-two-item";

    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>("IsEnabled", typeof(TabBarInteraction));

    /// <summary>
    /// Identifies the <see cref="GetIsSegmented"/> attached setting, which selects segmented-control motion.
    /// Changing it rebuilds the interaction state and cancels any gesture in flight.
    /// </summary>
    public static readonly AttachedProperty<bool> IsSegmentedProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>("IsSegmented", typeof(TabBarInteraction));

    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, State?>("State", typeof(TabBarInteraction));

    static TabBarInteraction()
    {
        IsEnabledProperty.Changed.AddClassHandler<TemplatedControl>((c, e) =>
        {
            c.TemplateApplied -= OnTemplateApplied;
            c.GetValue(StateProperty)?.Dispose();
            c.ClearValue(StateProperty);
            if (e.GetNewValue<bool>())
                c.TemplateApplied += OnTemplateApplied;
        });

        // The motion model is fixed per state, so rebuild it.
        IsSegmentedProperty.Changed.AddClassHandler<TemplatedControl>((c, _) =>
        {
            if (c.GetValue(StateProperty) is not { } state)
                return;
            var (indicator, presenter) = state.TemplateParts;
            state.Dispose();
            c.SetValue(StateProperty, new State(c, indicator, presenter));
        });
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(TemplatedControl element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(TemplatedControl element) =>
        element.GetValue(IsEnabledProperty);

    /// <inheritdoc cref="IsSegmentedProperty"/>
    public static void SetIsSegmented(TemplatedControl element, bool value) =>
        element.SetValue(IsSegmentedProperty, value);

    /// <inheritdoc cref="IsSegmentedProperty"/>
    public static bool GetIsSegmented(TemplatedControl element) =>
        element.GetValue(IsSegmentedProperty);

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        var owner = (TemplatedControl)sender!;
        owner.GetValue(StateProperty)?.Dispose();
        owner.ClearValue(StateProperty);

        var indicator = e.NameScope.Find<Control>("PART_Indicator");
        var presenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
        if (indicator is null || presenter is null)
            return;

        var state = new State(owner, indicator, presenter);
        owner.SetValue(StateProperty, state);
    }

    private sealed class State : IDisposable
    {
        private readonly TemplatedControl _owner;
        private readonly Control _indicator;
        private readonly ItemsPresenter _presenter;
        private readonly DispatcherTimer _settle;
        private readonly DispatcherTimer _travel;
        private readonly Dictionary<Control, (double Width, IDisposable? Subscription)> _itemWidthOverrides = new();
        private readonly HashSet<Control> _observedItems = new();
        private readonly Dictionary<Control, ContentPresenter?> _contentPresenters = new();
        private IBrush? _wipeAccent;
        private IBrush? _wipeLabel;
        private bool _wipeLabelIsDark;
        private readonly ScaleTransform _indicatorStretch = new();
        private long _lastTick;
        private TopLevel? _releaseRoot;
        private EventHandler<PointerReleasedEventArgs>? _releaseHandler;

        private bool _pressed;
        private bool _pressOnPill;
        private bool _dragging;
        private bool _twoItemBar;
        private Point _pressPoint;
        private Point _pressRootPoint;
        private Point _dragRootPoint;
        private double _grabOffset;
        private double _lastX;
        private double _velocity;
        private IPointer? _capturedPointer;

        private readonly bool _segmented;
        private bool _disposed;

        internal (Control Indicator, ItemsPresenter Presenter) TemplateParts =>
            (_indicator, _presenter);

        public State(TemplatedControl owner, Control indicator, ItemsPresenter presenter)
        {
            _owner = owner;
            _segmented = GetIsSegmented(owner);
            _indicator = indicator;
            _presenter = presenter;

            _settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _settle.Tick += OnSettleTick;
            _travel = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _travel.Tick += OnTravelTick;

            owner.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
            owner.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
            owner.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
            owner.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost,
                             RoutingStrategies.Direct, handledEventsToo: true);
            owner.DetachedFromVisualTree += OnDetached;

            if (owner is SelectingItemsControl sic)
            {
                _lastIndex = Math.Max(0, sic.SelectedIndex);
                sic.SelectionChanged += OnSelectionChanged;
            }

            if (!_segmented)
            {
                owner.PropertyChanged += OnOwnerPropertyChanged;
                if (owner is ItemsControl itemsControl)
                    itemsControl.ContainerPrepared += OnContainerPrepared;
                UpdateBottomGeometry();
            }

            // Sample the backdrop after layout.
            if (!_segmented)
                ScheduleBackdropSample();
        }

        private void UpdateBottomGeometry()
        {
            var containers = GetItems(visibleOnly: false);
            UpdateItemObservers(containers);
            var items = new List<Control>(containers.Count);
            foreach (var item in containers)
                if (item.IsVisible)
                    items.Add(item);
            var compact = _twoItemBar = items.Count == 2;
            _owner.Classes.Set(TwoItemClass, compact);

            List<Control>? staleItems = null;
            foreach (var item in _itemWidthOverrides.Keys)
            {
                if (!items.Contains(item))
                {
                    staleItems ??= new List<Control>();
                    staleItems.Add(item);
                }
            }
            if (staleItems is not null)
                foreach (var item in staleItems)
                {
                    _itemWidthOverrides[item].Subscription?.Dispose();
                    _itemWidthOverrides.Remove(item);
                }

            var width = compact ? TwoItemWidth : BottomItemWidth;
            if (items.Count > 0 && _owner.Bounds.Width > 0)
            {
                var insets = compact ? TwoItemBarInsets : BottomBarInsets;
                var available = _owner.Bounds.Width - BottomBarMargins - insets;
                if (Tabs.GetAccessory(_owner) is not null)
                    available -= BottomAccessoryWidth;
                width = Math.Min(width, Math.Max(0, available / items.Count));
            }

            foreach (var item in containers)
                item.Classes.Set(TwoItemClass, compact && item.IsVisible);

            foreach (var item in items)
            {
                if (_itemWidthOverrides.TryGetValue(item, out var current) && current.Width == width)
                    continue;
                if (_itemWidthOverrides.Remove(item, out current))
                    current.Subscription?.Dispose();
                _itemWidthOverrides[item] = (width, item.SetValue(
                    Layoutable.MaxWidthProperty, width, BindingPriority.Style));
            }
        }

        private void UpdateItemObservers(List<Control> items)
        {
            _observedItems.RemoveWhere(item =>
            {
                if (items.Contains(item))
                    return false;
                item.PropertyChanged -= OnItemPropertyChanged;
                return true;
            });

            foreach (var item in items)
            {
                if (_observedItems.Add(item))
                    item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.IsVisibleProperty)
                UpdateBottomGeometry();
        }

        private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ItemsControl.ItemCountProperty
                || e.Property == Visual.BoundsProperty
                || e.Property == Tabs.AccessoryProperty)
                UpdateBottomGeometry();
        }

        private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            _contentPresenters.Remove(e.Container);
            UpdateBottomGeometry();
        }

        private List<Control> GetItems(bool visibleOnly)
        {
            var list = new List<Control>();
            if (_owner is ItemsControl ic)
            {
                for (var i = 0; i < ic.ItemCount; i++)
                    if (ic.ContainerFromIndex(i) is Control c && (!visibleOnly || c.IsVisible))
                        list.Add(c);
            }
            return list;
        }

        private List<Control> Items => GetItems(visibleOnly: true);

        private bool IsTwoItemBar => _twoItemBar;

        private Rect BoundsOf(Control item)
        {
            var host = _indicator.Parent as Visual ?? _presenter;
            var origin = item.TranslatePoint(default, host);
            if (origin is { } p && item.Bounds.Width > 0)
                return new Rect(p.X, p.Y, item.Bounds.Width, item.Bounds.Height);

            // Fall back to equal cells before layout stabilizes.
            var items = Items;
            var index = -1;
            for (var i = 0; i < items.Count; i++)
                if (ReferenceEquals(items[i], item)) { index = i; break; }
            if (index < 0 || items.Count == 0 || _presenter.Bounds.Width <= 0)
                return item.Bounds;

            var w = _presenter.Bounds.Width / items.Count;
            return new Rect(index * w, 0, w, _presenter.Bounds.Height);
        }

        private Rect IndicatorBoundsOf(Control item)
        {
            var cell = BoundsOf(item);
            var overflow = IsTwoItemBar ? TwoItemLensOverflow : BottomLensOverflow;
            return _segmented || cell.Width <= 0
                ? cell
                : new Rect(cell.X - overflow, cell.Y,
                           cell.Width + overflow * 2, cell.Height);
        }

        private int SelectedIndex =>
            _owner is SelectingItemsControl s ? Math.Max(0, s.SelectedIndex) : 0;

        // Wipe against header content bounds.
        private Rect ContentBoundsOf(Control item, Rect cell)
        {
            var host = _indicator.Parent as Visual ?? _presenter;
            if (!_contentPresenters.TryGetValue(item, out var presenter) || presenter?.IsAttachedToVisualTree() != true)
            {
                presenter = item.GetVisualDescendants().OfType<ContentPresenter>()
                    .FirstOrDefault(c => c.Name == "PART_ContentPresenter");
                _contentPresenters[item] = presenter;
            }
            if (presenter?.Child is { } content && content.Bounds.Width > 0
                && content.TranslatePoint(default, host) is { } p)
                return new Rect(p.X, p.Y, content.Bounds.Width, content.Bounds.Height);
            return cell;
        }

        private Rect TargetFor(int index)
        {
            if (_owner is not ItemsControl ic || ic.ItemCount == 0)
                return default;
            index = Math.Clamp(index, 0, ic.ItemCount - 1);
            return ic.ContainerFromIndex(index) is Control { IsVisible: true } c
                ? IndicatorBoundsOf(c)
                : default;
        }

        private void MoveTo(Rect target)
        {
            if (target.Width <= 0)
                return;
            _indicator.Width = target.Width;
            _indicator.Height = target.Height > 0 ? target.Height : _indicator.Bounds.Height;
            Canvas.SetLeft(_indicator, target.X);
            Canvas.SetTop(_indicator, target.Y);
            _lastX = target.X;
        }

        private void SetItemPillsVisible(bool visible)
        {
            foreach (var item in Items)
                item.Classes.Set("cupertino-no-pill", !visible);
        }

        private double _settleFrom, _settleTo, _settleT, _settleStretch = 1;

        private void StartSettle(Rect target)
        {
            if (target.Width <= 0)
            {
                EndDragVisual();
                return;
            }
            if (CupertinoAccessibility.ReduceMotion)
            {
                MoveTo(target);
                EndDragVisual();
                return;
            }
            _indicator.Width = target.Width;
            _settleFrom = Canvas.GetLeft(_indicator);
            if (double.IsNaN(_settleFrom))
                _settleFrom = target.X;
            _settleTo = target.X;
            _settleT = 0;
            _settleStretch = TabBarMotionModel.DragWidthScale(_velocity, _segmented);
            _lastTick = MotionClock.Now;
            _settle.Start();
        }

        private double ElapsedMilliseconds()
        {
            var now = MotionClock.Now;
            var elapsed = Math.Clamp(now - _lastTick, 1, 50);
            _lastTick = now;
            return elapsed;
        }

        private void OnSettleTick(object? sender, EventArgs e)
        {
            var elapsed = ElapsedMilliseconds();
            _settleT += elapsed / SnapMilliseconds;
            if (_settleT >= 1)
            {
                _settleT = 1;
                _settle.Stop();
                EndDragVisual();
                return;
            }

            var w = 8.4 * _settleT;
            var norm = 1 - (1 + 8.4) * Math.Exp(-8.4);
            var p = (1 - (1 + w) * Math.Exp(-w)) / norm;
            Canvas.SetLeft(_indicator, _settleFrom + (_settleTo - _settleFrom) * p);
            RampPhase(elapsed);
            SetStretch(1 + (_settleStretch - 1) * (1 - p),
                       1 + (HeightScale - 1) * (1 - p));   // settle follows a drag
            var settleX = _settleFrom + (_settleTo - _settleFrom) * p;
            var settleW = _indicator.Width;
            if (!double.IsNaN(settleW) && settleW > 0)
            {
                var visible = settleW * (1 + (_settleStretch - 1) * (1 - p));
                var centre = settleX + settleW / 2;
                ApplyWipe(centre - visible / 2, centre + visible / 2);
            }
        }

        private double _travelT;
        private double _travelHops = 1;
        private Rect _travelFrom, _travelTo;
        private int _hideGen;
        private GlassSurface? _segLens;
        private Border? _segFill;
        private double _fillPhase = 1;

        private void RampPhase(double dtMs)
        {
            if (!_segmented)
                return;
            var target = _dragging ? 0.0 : 1.0;
            if (Math.Abs(_fillPhase - target) < 0.001)
                return;
            var step = dtMs / 90.0;
            var fill = target > _fillPhase
                ? Math.Min(target, _fillPhase + step)
                : Math.Max(target, _fillPhase - step);
            SetLensPhase(fill, 1 - fill);
        }

        // The template namescope does not expose the lens; use the indicator tree.
        private void SetLensPhase(double fill, double lens)
        {
            if (!_segmented)
                return;
            _fillPhase = fill;
            _segFill ??= (_indicator as Border)?.Child as Border;
            if (_segLens is null)
            {
                Visual? scope = _indicator;
                while (scope is not null && _segLens is null)
                {
                    scope = scope.GetVisualParent();
                    if (scope is null)
                        break;
                    _segLens = scope.GetVisualDescendants().OfType<GlassSurface>()
                        .FirstOrDefault(g => g.Name == "PART_IndicatorLens");
                }
            }
            if (_segFill is not null)
                _segFill.Opacity = fill;
            if (_segLens is not null)
            {
                _segLens.Opacity = lens;
                _segLens.IsLive = lens > 0;
                _segLens.IsVisible = lens > 0;
                if (lens > 0)
                    SyncLens();
            }
        }

        private double _lensStretchX = 1, _lensStretchY = 1;
        private double _endMin = double.NegativeInfinity, _endMax = double.PositiveInfinity;

        private void CaptureEnds()
        {
            var items = Items;
            if (items.Count == 0)
                return;
            _endMin = IndicatorBoundsOf(items[0]).X - 3;
            _endMax = IndicatorBoundsOf(items[^1]).Right + 3;
        }

        private double CapStretch(double stretch, double centre, double width)
        {
            if (width <= 0)
                return stretch;
            var cap = Math.Min((centre - _endMin) * 2 / width, (_endMax - centre) * 2 / width);
            return Math.Clamp(Math.Min(stretch, cap), 1, TabBarMotionModel.MaxStretch);
        }

        private void SyncLens()
        {
            if (_segLens is null)
                return;
            var left = Canvas.GetLeft(_indicator);
            var top = Canvas.GetTop(_indicator);
            if (double.IsNaN(left))
                left = 0;
            if (double.IsNaN(top))
                top = 0;
            var w = _indicator.Width - 4;
            var h = (_indicator.Height > 0 ? _indicator.Height : _indicator.Bounds.Height) - 4;
            if (w <= 0 || h <= 0)
                return;
            var lw = w * _lensStretchX;
            var lh = h * _lensStretchY;
            _segLens.Width = lw;
            _segLens.Height = lh;
            _segLens.CornerRadius = new CornerRadius(lh / 2);
            var optics = TabBarMotionModel.SegmentedLensOptics(_lensStretchY);
            _segLens.GlassThickness = optics.Thickness;
            _segLens.RefractionStrength = optics.Refraction;
            _segLens.ChromaticAberration = optics.Chroma;
            _segLens.Magnification = optics.Magnification;
            Canvas.SetLeft(_segLens, left + 2 + (w - lw) / 2);
            Canvas.SetTop(_segLens, top + 2 + (h - lh) / 2);
        }
        private int _lastIndex;

        private void StartTravel(Rect from, Rect to)
        {
            if (from.Width <= 0 || to.Width <= 0)
                return;

            if (CupertinoAccessibility.ReduceMotion)
            {
                ClearHotItems();
                return;
            }

            _settle.Stop();
            _travelFrom = from;
            _travelTo = to;
            _travelT = 0;

            var hops = Math.Abs(to.Center.X - from.Center.X) / Math.Max(1, to.Width);
            _travelHops = hops;
            _travelDuration = _segmented
                ? TabBarMotionModel.SegmentedTravelMilliseconds
                : TabBarMotionModel.TravelMilliseconds * Math.Clamp(hops, 1, 2.2);

            BeginDragVisual();
            _indicator.Width = from.Width;
            Canvas.SetLeft(_indicator, from.X);
            Canvas.SetTop(_indicator, from.Y);
            _lastTick = MotionClock.Now;
            _travel.Start();
        }

        private double _travelDuration = TabBarMotionModel.TravelMilliseconds;

        private void OnTravelTick(object? sender, EventArgs e)
        {
            _travelT += ElapsedMilliseconds() / _travelDuration;
            if (_travelT >= 1)
            {
                _travel.Stop();
                MoveTo(_travelTo);
                if (_segmented)
                    SetLensPhase(1, 0);
                EndDragVisual();
                return;
            }

            if (_segmented)
            {
                var t = _travelT * _travelDuration;
                var (p, w, fill, lens) = TabBarMotionModel.SegmentedTravel(t, _travelHops);
                var width = _travelTo.Width * w;
                var centre = _travelFrom.Center.X + (_travelTo.Center.X - _travelFrom.Center.X) * p;
                width = Math.Min(width, Math.Max(_travelTo.Width * 0.9,
                    Math.Min((centre - _endMin) * 2, (_endMax - centre) * 2)));
                Canvas.SetLeft(_indicator, centre - width / 2);
                _indicator.Width = width;
                SetStretch(1, 1 + 0.35 * lens);
                SetLensPhase(fill, lens);
                return;
            }

            var (left, right) = TabBarMotionModel.TravelEdges(_travelT, _travelFrom, _travelTo);
            var goingRight = _travelTo.X >= _travelFrom.X;

            var span = Math.Clamp(right - left, _travelTo.Width,
                                  _travelTo.Width * TabBarMotionModel.MaxStretch);
            Canvas.SetLeft(_indicator, goingRight ? left : right - span);
            _indicator.Width = span;

            var activity = (span / _travelTo.Width - 1) / (TabBarMotionModel.MaxStretch - 1);
            SetStretch(1, 1 + (HeightScale - 1) * Math.Clamp(activity, 0, 1));

            // Use the drawn rect; layout bounds lag by one frame.
            var drawnLeft = goingRight ? left : right - span;
            ApplyWipe(drawnLeft, drawnLeft + span);

        }

        private void BeginDragVisual()
        {
            var left = Canvas.GetLeft(_indicator);
            if (_indicator.IsVisible && !double.IsNaN(left)
                && _pressPoint.X >= left && _pressPoint.X <= left + _indicator.Bounds.Width)
            {
                SetItemPillsVisible(false);
                SetStretch(1, _dragging ? HeightScale : 1.0);
                return;
            }

            var target = TargetFor(SelectedIndex);
            if (target.Width <= 0)
            {
                foreach (var item in Items)
                {
                    var b = BoundsOf(item);
                    if (_pressPoint.X >= b.X && _pressPoint.X <= b.Right)
                    {
                        target = IndicatorBoundsOf(item);
                        break;
                    }
                }
            }
            if (target.Width <= 0)
            {
                _dragging = false;
                return;
            }

            _grabOffset = Math.Clamp(_pressPoint.X - target.X, 0, target.Width);

            _indicator.Width = target.Width;
            _indicator.Height = target.Height;
            Canvas.SetLeft(_indicator, target.X);
            Canvas.SetTop(_indicator, target.Y);
            _lastX = target.X;
            _hideGen++;
            CaptureEnds();
            _indicator.IsVisible = true;
            _indicator.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            if (_indicator is GlassSurface glass)
                glass.IsLive = true;
            SetItemPillsVisible(false);
            SetStretch(1, _dragging ? HeightScale : 1.0);
        }

        private void EndDragVisual()
        {
            _wipeAccent = null;
            _wipeLabel = null;
            if (_segmented && _indicator.IsVisible && !CupertinoAccessibility.ReduceMotion)
            {
                SetLensPhase(1, 0);
                var gen = ++_hideGen;
                DispatcherTimer.RunOnce(() =>
                {
                    if (gen == _hideGen && !_dragging && !_travel.IsEnabled)
                        _indicator.IsVisible = false;
                }, TimeSpan.FromMilliseconds(180));
            }
            else
            {
                if (_segmented)
                    SetLensPhase(1, 0);
                _indicator.IsVisible = false;
            }
            if (_indicator is GlassSurface glass)
                glass.IsLive = false;
            ClearHotItems();
            SetItemPillsVisible(true);
            SetStretch(1);

        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var index = SelectedIndex;
            if (index == _lastIndex)
                return;

            var from = TargetFor(_lastIndex);
            var to = TargetFor(index);
            _lastIndex = index;

            // Sample after the page swap, not from outgoing content.
            ScheduleBackdropSample();

            if (!_dragging && !_travel.IsEnabled && from.Width > 0 && to.Width > 0)
                StartTravel(from, to);
        }

        private void ApplyWipe(double blobLeft, double blobRight)
        {
            if (_segmented)
                return;
            if (_wipeAccent is null || _wipeLabel is null || _wipeLabelIsDark != _backdropIsDark)
            {
                _wipeAccent = Brush("CupertinoAccentBrush", Colors.DodgerBlue);
                _wipeLabel = LabelBrushForBackdrop();
                _wipeLabelIsDark = _backdropIsDark;
            }
            var accent = _wipeAccent;
            var label = _wipeLabel;

            // Preserve header bindings by setting the item foreground.
            foreach (var item in Items)
            {
                var cell = BoundsOf(item);
                if (cell.Width <= 0)
                    continue;

                var b = ContentBoundsOf(item, cell);
                var brush = TabLabelWipe.Apply(
                    item, b.X, b.Width, blobLeft, blobRight, accent, label);
                item.SetCurrentValue(TemplatedControl.ForegroundProperty, brush);
            }
        }

        private IBrush LabelBrushForBackdrop() =>
            _backdropIsDark
                ? Brush("CupertinoLabelOnGlassBrush", Colors.White)
                : Brush("CupertinoLabelBrush", Colors.Black);

        private bool _backdropIsDark;

        private void ScheduleBackdropSample() =>
            DispatcherTimer.RunOnce(() =>
            {
                if (!_disposed)
                    SampleBackdrop();
            }, TimeSpan.FromMilliseconds(120));

        private void SampleBackdrop()
        {
            if (_segmented
                || !BackdropLuminanceSampler.TryIsDark(_owner, _presenter, out var isDark))
                return;

            var wasDark = _backdropIsDark;
            _backdropIsDark = isDark;
            if (wasDark && !isDark)
                foreach (var item in Items)
                    item.ClearValue(TemplatedControl.ForegroundProperty);
            else if (!wasDark && isDark)
                ApplyRestingColours();
        }

        private IBrush Brush(string key, Color fallback) =>
            _owner.TryFindResource(key, out var v) && v is IBrush b ? b : new SolidColorBrush(fallback);

        private void ApplyRestingColours()
        {
            if (!_backdropIsDark)
                return;

            var accent = Brush("CupertinoAccentBrush", Colors.DodgerBlue);
            var light = Brush("CupertinoLabelOnGlassBrush", Colors.White);
            var selected = SelectedIndex;
            var items = Items;

            for (var i = 0; i < items.Count; i++)
            {
                var realIndex = _owner is ItemsControl ic ? ic.IndexFromContainer(items[i]) : i;
                items[i].SetCurrentValue(TemplatedControl.ForegroundProperty,
                                         realIndex == selected ? accent : light);
            }
        }

        private void ClearHotItems()
        {
            foreach (var item in Items)
            {
                // Return colour ownership to styles and bindings.
                item.ClearValue(TemplatedControl.ForegroundProperty);
            }

            ApplyRestingColours();
        }

        private const double DragHeightScale = 1.242;

        private const double SegmentedDragHeightScale = 1.5;

        private double HeightScale => _segmented ? SegmentedDragHeightScale : DragHeightScale;

        private void SetStretch(double scaleX, double scaleY = 1.0)
        {
            if (_segmented)
            {
                _lensStretchX = scaleX;
                _lensStretchY = scaleY;
                SyncLens();
                return;
            }
            // Runs per drag move and settle tick; mutate the cached transform
            // instead of parsing a transform string per call.
            if (!ReferenceEquals(_indicator.RenderTransform, _indicatorStretch))
                _indicator.RenderTransform = _indicatorStretch;
            _indicatorStretch.ScaleX = scaleX;
            _indicatorStretch.ScaleY = scaleY;
        }

        private Rect SelectedItemRect()
        {
            if (_owner is SelectingItemsControl { SelectedIndex: >= 0 } sic
                && _owner is ItemsControl ic
                && ic.ContainerFromIndex(sic.SelectedIndex) is Control c)
                return IndicatorBoundsOf(c);
            return default;
        }

        private void OnPressed(object? sender, PointerPressedEventArgs e)
        {
            var local = e.GetPosition(_presenter);
            if (!new Rect(_presenter.Bounds.Size).Contains(local))
            {
                _pressed = false;
                return;
            }

            _pressed = true;
            ArmTopLevelRelease();
            _dragging = false;
            _velocity = 0;
            _pressPoint = e.GetPosition(_presenter);
            _pressRootPoint = RootPosition(e);
            // Decide drag eligibility before selection changes.
            var left0 = Canvas.GetLeft(_indicator);
            var onIndicator = _indicator.IsVisible && !double.IsNaN(left0)
                && _pressPoint.X >= left0 && _pressPoint.X <= left0 + _indicator.Bounds.Width;
            var sel = SelectedItemRect();
            _pressOnPill = onIndicator
                || (sel.Width > 0 && _pressPoint.X >= sel.X && _pressPoint.X <= sel.Right);
            _travel.Stop();
            var left = Canvas.GetLeft(_indicator);
            _grabOffset = _pressPoint.X - (double.IsNaN(left) ? 0 : left);
            _settle.Stop();
        }

        private void OnMoved(object? sender, PointerEventArgs e)
        {
            if (!_pressed)
                return;

            var p = e.GetPosition(_presenter);
            var rootPoint = RootPosition(e);
            if (!_dragging)
            {
                var dx = rootPoint.X - _pressRootPoint.X;
                var dy = rootPoint.Y - _pressRootPoint.Y;
                if (TabBarMotionModel.ShouldYieldToScroll(dx, dy, dragging: false))
                {
                    CancelPointerGesture(e.Pointer);
                    return;
                }
                if (Math.Abs(dx) < DragThreshold || Math.Abs(dx) < Math.Abs(dy))
                    return;
            }
            else if (TabBarMotionModel.ShouldYieldToScroll(
                         0, rootPoint.Y - _dragRootPoint.Y, dragging: true))
            {
                CancelPointerGesture(e.Pointer);
                return;
            }

            if (!_dragging && _segmented && !_pressOnPill)
                return;

            if (!_dragging)
            {
                _dragging = true;
                _dragRootPoint = rootPoint;
                BeginDragVisual();
                if (!_dragging)
                    return;
            }
            e.Pointer.Capture(_owner);
            _capturedPointer = e.Pointer;

            var items = Items;
            if (items.Count == 0)
                return;

            var first = IndicatorBoundsOf(items[0]);
            var last = IndicatorBoundsOf(items[^1]);

            var x = Math.Clamp(p.X - _grabOffset, first.X, last.X);

            _velocity = _velocity * 0.7 + (x - _lastX) * 0.3;
            _lastX = x;
            Canvas.SetLeft(_indicator, x);
            RampPhase(8);

            var dragStretch = TabBarMotionModel.DragWidthScale(_velocity, _segmented);
            var w0 = _indicator.Bounds.Width > 0 ? _indicator.Bounds.Width : _indicator.Width;
            if (!double.IsNaN(w0) && w0 > 0)
                dragStretch = CapStretch(dragStretch, x + w0 / 2, w0);
            SetStretch(dragStretch, HeightScale);

            var w = _indicator.Bounds.Width > 0 ? _indicator.Bounds.Width : _indicator.Width;
            if (!double.IsNaN(w) && w > 0)
            {
                var centre = x + w / 2;
                var visible = w * dragStretch;
                ApplyWipe(centre - visible / 2, centre + visible / 2);
            }
        }

        private void OnReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_pressed)
                return;
            _pressed = false;
            DisarmTopLevelRelease();

            if (!_dragging)
            {
                if (!_travel.IsEnabled)
                    EndDragVisual();
                return;
            }
            _dragging = false;
            _capturedPointer = null;
            e.Pointer.Capture(null);

            var items = Items;
            if (items.Count == 0)
                return;

            var centre = Canvas.GetLeft(_indicator) + _indicator.Bounds.Width / 2 + _velocity * 4;
            var nearest = 0;
            var best = double.MaxValue;
            for (var i = 0; i < items.Count; i++)
            {
                var d = Math.Abs(BoundsOf(items[i]).Center.X - centre);
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            var targetIndex = -1;
            if (_owner is SelectingItemsControl sic and ItemsControl ic)
            {
                var real = ic.IndexFromContainer(items[nearest]);
                if (real >= 0)
                {
                    targetIndex = real;
                    var changed = real != _lastIndex;
                    _lastIndex = real;
                    sic.SelectedIndex = real;
                    if (changed)
                        ScheduleBackdropSample();
                }
            }

            var target = targetIndex >= 0 ? TargetFor(targetIndex) : BoundsOf(items[nearest]);
            if (target.Width > 0)
                StartSettle(target);
            else
                EndDragVisual();
        }

        // Track releases outside the host.
        private void ArmTopLevelRelease()
        {
            DisarmTopLevelRelease();
            if (TopLevel.GetTopLevel(_owner) is not { } root)
                return;

            EventHandler<PointerReleasedEventArgs>? handler = null;
            handler = (_, e) => OnReleased(_owner, e);
            _releaseRoot = root;
            _releaseHandler = handler;
            root.AddHandler(InputElement.PointerReleasedEvent, handler,
                            RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        private void DisarmTopLevelRelease()
        {
            if (_releaseRoot is not null && _releaseHandler is not null)
                _releaseRoot.RemoveHandler(InputElement.PointerReleasedEvent, _releaseHandler);
            _releaseRoot = null;
            _releaseHandler = null;
        }

        private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _capturedPointer = null;
            if (!_pressed && !_dragging)
                return;

            ResetTransientVisual();
        }

        private Point RootPosition(PointerEventArgs e) =>
            TopLevel.GetTopLevel(_owner) is { } root
                ? e.GetPosition(root)
                : e.GetPosition(_presenter);

        private void CancelPointerGesture(IPointer pointer)
        {
            // Clear state before capture-loss callbacks.
            _capturedPointer = null;
            ResetTransientVisual();
            pointer.Capture(null);
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) =>
            ResetTransientVisual();

        private void ResetTransientVisual()
        {
            _pressed = false;
            _dragging = false;
            DisarmTopLevelRelease();
            _settle.Stop();
            _travel.Stop();
            _hideGen++;
            _indicator.IsVisible = false;
            if (_indicator is GlassSurface glass)
                glass.IsLive = false;
            if (_segmented)
                SetLensPhase(1, 0);
            ClearHotItems();
            SetItemPillsVisible(true);
            SetStretch(1);
        }

        public void Dispose()
        {
            _disposed = true;
            var capturedPointer = _capturedPointer;
            _capturedPointer = null;
            ResetTransientVisual();
            capturedPointer?.Capture(null);
            _settle.Stop();
            _settle.Tick -= OnSettleTick;
            _travel.Stop();
            _travel.Tick -= OnTravelTick;
            _owner.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
            _owner.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
            _owner.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
            _owner.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
            _owner.DetachedFromVisualTree -= OnDetached;
            if (!_segmented)
                _owner.PropertyChanged -= OnOwnerPropertyChanged;
            if (_owner is SelectingItemsControl sic)
                sic.SelectionChanged -= OnSelectionChanged;
            if (!_segmented && _owner is ItemsControl itemsControl)
                itemsControl.ContainerPrepared -= OnContainerPrepared;
            foreach (var item in _observedItems)
                item.PropertyChanged -= OnItemPropertyChanged;
            _observedItems.Clear();
            foreach (var widthOverride in _itemWidthOverrides.Values)
                widthOverride.Subscription?.Dispose();
            _itemWidthOverrides.Clear();
            _contentPresenters.Clear();
        }
    }
}
