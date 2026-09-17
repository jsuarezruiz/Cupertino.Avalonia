using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// Presents pull-to-refresh state and mouse interaction.
/// </summary>
public static class RefreshInteraction
{
    private const double BandHeight = 60;
    private const double SettleRate = 14;

    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>("IsEnabled", typeof(RefreshInteraction));

    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, State?>("State", typeof(RefreshInteraction));

    static RefreshInteraction()
    {
        IsEnabledProperty.Changed.AddClassHandler<TemplatedControl>((c, e) =>
        {
            c.TemplateApplied -= OnTemplateApplied;
            c.GetValue(StateProperty)?.Dispose();
            c.ClearValue(StateProperty);
            if (e.GetNewValue<bool>())
                c.TemplateApplied += OnTemplateApplied;
        });
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(TemplatedControl element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(TemplatedControl element) =>
        element.GetValue(IsEnabledProperty);

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        var owner = (TemplatedControl)sender!;
        owner.GetValue(StateProperty)?.Dispose();
        owner.ClearValue(StateProperty);

        var presenter = e.NameScope.Find<Panel>("PART_RefreshVisualizerPresenter");
        var content = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        if (presenter is null || content is null)
            return;

        owner.SetValue(StateProperty, new State(owner, presenter, content));
    }

    private sealed class State : IDisposable
    {
        private readonly TemplatedControl _owner;
        private readonly Panel _presenter;
        private readonly ContentPresenter _content;
        private readonly TranslateTransform _band = new() { Y = -BandHeight };
        private readonly TranslateTransform _hold = new();
        private readonly DispatcherTimer _timer;
        private readonly ITransform? _originalPresenterTransform;
        private readonly Thickness _originalPresenterMargin;
        private readonly double _originalPresenterHeight;
        private readonly VerticalAlignment _originalPresenterAlignment;
        private readonly ITransform? _originalContentTransform;
        private IDisposable? _stateWatch;
        private RefreshVisualizer? _visualizer;
        private CupertinoActivityIndicator? _spinner;
        private long _lastTick;
        private ITransform? _originalVisualizerTransform;
        private Thickness _originalVisualizerMargin;
        private double _originalVisualizerHeight;
        private VerticalAlignment _originalVisualizerAlignment;
        private double _bandTarget = -BandHeight;
        private double _holdTarget;

        private bool _mousePulling;
        private double _pressY;

        public State(TemplatedControl owner, Panel presenter, ContentPresenter content)
        {
            _owner = owner;
            _presenter = presenter;
            _content = content;
            _originalPresenterTransform = presenter.RenderTransform;
            _originalPresenterMargin = presenter.Margin;
            _originalPresenterHeight = presenter.Height;
            _originalPresenterAlignment = presenter.VerticalAlignment;
            _originalContentTransform = content.RenderTransform;

            PlacePresenter();
            content.RenderTransform = _hold;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTick;

            // Restore placement after adapter updates.
            presenter.PropertyChanged += OnPlacementChanged;
            presenter.Children.CollectionChanged += OnPresenterChildren;
            HookVisualizer();

            // Track mouse pulls independently.
            owner.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnPulled, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            owner.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent, OnPullMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            owner.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, OnPullReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            owner.PointerExited += OnPullLost;
            owner.PointerCaptureLost += OnPullLost;
        }

        public void Dispose()
        {
            _owner.RemoveHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnPulled);
            _owner.RemoveHandler(Avalonia.Input.InputElement.PointerMovedEvent, OnPullMoved);
            _owner.RemoveHandler(Avalonia.Input.InputElement.PointerReleasedEvent, OnPullReleased);
            _owner.PointerExited -= OnPullLost;
            _owner.PointerCaptureLost -= OnPullLost;
            _presenter.PropertyChanged -= OnPlacementChanged;
            _presenter.Children.CollectionChanged -= OnPresenterChildren;
            if (_visualizer is not null)
            {
                _visualizer.PropertyChanged -= OnPlacementChanged;
                RestoreVisualizerPlacement(_visualizer);
            }
            _stateWatch?.Dispose();
            _timer.Tick -= OnTick;
            _timer.Stop();
            if (ReferenceEquals(_content.RenderTransform, _hold))
                _content.RenderTransform = _originalContentTransform;
            if (ReferenceEquals(_presenter.RenderTransform, _band))
                _presenter.RenderTransform = _originalPresenterTransform;
            _presenter.Margin = _originalPresenterMargin;
            _presenter.Height = _originalPresenterHeight;
            _presenter.VerticalAlignment = _originalPresenterAlignment;
        }

        private void OnPulled(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.Pointer.Type != Avalonia.Input.PointerType.Mouse)
                return;
            _pressY = e.GetPosition(_owner).Y;
            var scroller = FindScroller();
            _mousePulling = scroller is null || scroller.Offset.Y <= 0.5;
        }

        private void OnPullMoved(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (!_mousePulling || e.Pointer.Type != Avalonia.Input.PointerType.Mouse)
                return;
            // Do not capture; the adapter owns pointer tracking.
            if (!e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed)
            {
                FinishPull();
                return;
            }
            var dy = e.GetPosition(_owner).Y - _pressY;
            if (dy <= 0)
                return;

            _timer.Stop();
            var effective = dy * 0.5;
            if (effective > BandHeight)
                effective = BandHeight + (effective - BandHeight) * 0.55;
            effective = Math.Min(effective, BandHeight * 1.6);
            _band.Y = -BandHeight + Math.Min(effective, BandHeight);
            _hold.Y = effective;
            UpdateSweep();
        }

        private void OnPullReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e) =>
            FinishPull();

        private void OnPullLost(object? sender, EventArgs e) =>
            FinishPull();

        private void FinishPull()
        {
            if (!_mousePulling)
                return;
            _mousePulling = false;
            if (CupertinoAccessibility.ReduceMotion)
            {
                _band.Y = _bandTarget;
                _hold.Y = _holdTarget;
            }
            else
            {
                StartTimer();
            }
        }

        private void StartTimer()
        {
            _lastTick = MotionClock.Now;
            _timer.Start();
        }

        private ScrollViewer? FindScroller()
        {
            return Find(_content);

            static ScrollViewer? Find(Visual v)
            {
                foreach (var child in v.GetVisualChildren())
                {
                    if (child is ScrollViewer s)
                        return s;
                    if (Find(child) is { } nested)
                        return nested;
                }
                return null;
            }
        }

        private void PlacePresenter()
        {
            _presenter.Margin = default;
            _presenter.Height = BandHeight;
            _presenter.VerticalAlignment = VerticalAlignment.Top;
            _presenter.RenderTransform = _band;
        }

        private static void PlaceVisualizer(RefreshVisualizer viz)
        {
            viz.Margin = default;
            viz.Height = BandHeight;
            viz.RenderTransform = null;
            viz.VerticalAlignment = VerticalAlignment.Stretch;
        }

        private void OnPlacementChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != Visual.RenderTransformProperty
                && e.Property != Layoutable.MarginProperty
                && e.Property != Layoutable.HeightProperty)
                return;

            if (ReferenceEquals(sender, _presenter))
            {
                if (!ReferenceEquals(_presenter.RenderTransform, _band)
                    || _presenter.Margin != default
                    || Math.Abs(_presenter.Height - BandHeight) > 0.5)
                    PlacePresenter();
            }
            else if (sender is RefreshVisualizer viz
                     && (viz.RenderTransform is not null
                         || viz.Margin != default
                         || Math.Abs(viz.Height - BandHeight) > 0.5))
            {
                PlaceVisualizer(viz);
            }
        }

        private void OnPresenterChildren(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
            HookVisualizer();

        private void HookVisualizer()
        {
            RefreshVisualizer? found = null;
            foreach (var child in _presenter.Children)
                if (child is RefreshVisualizer v) { found = v; break; }
            if (ReferenceEquals(found, _visualizer))
                return;

            _stateWatch?.Dispose();
            if (_visualizer is not null)
            {
                _visualizer.PropertyChanged -= OnPlacementChanged;
                RestoreVisualizerPlacement(_visualizer);
            }
            _visualizer = found;
            _spinner = null;
            if (found is null)
                return;

            // Flatten the default composition transform.
            _originalVisualizerTransform = found.RenderTransform;
            _originalVisualizerMargin = found.Margin;
            _originalVisualizerHeight = found.Height;
            _originalVisualizerAlignment = found.VerticalAlignment;
            PlaceVisualizer(found);
            found.PropertyChanged += OnPlacementChanged;

            _stateWatch = found.GetObservable(RefreshVisualizer.RefreshVisualizerStateProperty)
                .Subscribe(new AnonymousObserver<RefreshVisualizerState>(OnStateChanged));
        }

        private void RestoreVisualizerPlacement(RefreshVisualizer visualizer)
        {
            visualizer.RenderTransform = _originalVisualizerTransform;
            visualizer.Margin = _originalVisualizerMargin;
            visualizer.Height = _originalVisualizerHeight;
            visualizer.VerticalAlignment = _originalVisualizerAlignment;
        }

        private void OnStateChanged(RefreshVisualizerState state)
        {
            (_bandTarget, _holdTarget) = state switch
            {
                RefreshVisualizerState.Peeking => (-BandHeight * 0.5, BandHeight * 0.5),
                RefreshVisualizerState.Interacting => (-BandHeight * 0.5, BandHeight * 0.5),
                RefreshVisualizerState.Pending => (0d, BandHeight),
                RefreshVisualizerState.Refreshing => (0d, BandHeight),
                _ => (-BandHeight, 0d),
            };

            if (_mousePulling && state != RefreshVisualizerState.Refreshing)
                return;

            if (CupertinoAccessibility.ReduceMotion)
            {
                _band.Y = _bandTarget;
                _hold.Y = _holdTarget;
                return;
            }
            StartTimer();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var elapsed = MotionClock.TakeElapsedSeconds(ref _lastTick);
            var k = 1 - Math.Exp(-SettleRate * elapsed);
            _band.Y += (_bandTarget - _band.Y) * k;
            _hold.Y += (_holdTarget - _hold.Y) * k;
            if (Math.Abs(_band.Y - _bandTarget) < 0.5 && Math.Abs(_hold.Y - _holdTarget) < 0.5)
            {
                _band.Y = _bandTarget;
                _hold.Y = _holdTarget;
                _timer.Stop();
            }
            UpdateSweep();
        }

        private void UpdateSweep()
        {
            if (_visualizer is not { } visualizer)
                return;
            if (_spinner is null || !_spinner.IsAttachedToVisualTree())
                _spinner = FindSpinner(visualizer);
            if (_spinner is not { } spinner)
                return;

            spinner.SweepFraction =
                visualizer.GetValue(RefreshVisualizer.RefreshVisualizerStateProperty)
                    is RefreshVisualizerState.Pending or RefreshVisualizerState.Refreshing
                ? 1
                : Math.Clamp((_band.Y + BandHeight) / BandHeight, 0, 1);
        }
    }

    private static CupertinoActivityIndicator? FindSpinner(Visual visualizer)
    {
        foreach (var descendant in visualizer.GetVisualDescendants())
            if (descendant is CupertinoActivityIndicator found)
                return found;
        return null;
    }
}
