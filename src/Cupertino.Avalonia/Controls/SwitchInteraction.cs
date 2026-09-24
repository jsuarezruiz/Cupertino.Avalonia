using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Animation;

namespace Cupertino.Controls;

/// <summary>
/// Drives switch colour and lighting from the knob position.
/// </summary>
public static class SwitchInteraction
{
    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, bool>("IsEnabled", typeof(SwitchInteraction));

    /// <summary>
    /// Identifies the <see cref="GetOnTint"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<Color> OnTintProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, Color>("OnTint", typeof(SwitchInteraction));

    /// <summary>
    /// Identifies the <see cref="GetOffTint"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<Color> OffTintProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, Color>("OffTint", typeof(SwitchInteraction));

    private static readonly AttachedProperty<TrackingState?> StateProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, TrackingState?>("State", typeof(SwitchInteraction));

    // Both state fills are flat. The shared knob supplies the held glass.
    private const double HoverMix = 0.06;
    private const string ReleasingClass = "cupertino-switch-releasing";
    private const string StateContentClass = "cupertino-switch-state-content";
    private static readonly TimeSpan PressTravelDuration = TimeSpan.FromMilliseconds(334);
    private static readonly TimeSpan ReleaseTravelDuration = TimeSpan.FromMilliseconds(334);
    private static readonly TimeSpan PressScaleDuration = TimeSpan.FromMilliseconds(110);
    private static readonly TimeSpan ReleaseScaleDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ReleaseMaterialHold = TimeSpan.FromMilliseconds(110);

    static SwitchInteraction()
    {
        IsEnabledProperty.Changed.AddClassHandler<ToggleSwitch>((sw, e) => OnIsEnabledChanged(sw, e.GetNewValue<bool>()));
        OnTintProperty.Changed.AddClassHandler<ToggleSwitch>((sw, _) => Refresh(sw));
        OffTintProperty.Changed.AddClassHandler<ToggleSwitch>((sw, _) => Refresh(sw));
        ToggleSwitch.OnContentProperty.Changed.AddClassHandler<ToggleSwitch>((sw, _) => RefreshStateContent(sw));
        ToggleSwitch.OffContentProperty.Changed.AddClassHandler<ToggleSwitch>((sw, _) => RefreshStateContent(sw));
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(ToggleSwitch element, bool value) => element.SetValue(IsEnabledProperty, value);
    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(ToggleSwitch element) => element.GetValue(IsEnabledProperty);

    /// <inheritdoc cref="OnTintProperty"/>
    public static void SetOnTint(ToggleSwitch element, Color value) => element.SetValue(OnTintProperty, value);
    /// <inheritdoc cref="OnTintProperty"/>
    public static Color GetOnTint(ToggleSwitch element) => element.GetValue(OnTintProperty);

    /// <inheritdoc cref="OffTintProperty"/>
    public static void SetOffTint(ToggleSwitch element, Color value) => element.SetValue(OffTintProperty, value);
    /// <inheritdoc cref="OffTintProperty"/>
    public static Color GetOffTint(ToggleSwitch element) => element.GetValue(OffTintProperty);

    private static void OnCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch sw && sw.GetValue(StateProperty)?.IsUserToggling == true)
            CupertinoHaptics.Play(HapticFeedback.ImpactLight);
    }

    private static void OnIsEnabledChanged(ToggleSwitch sw, bool enabled)
    {
        RefreshStateContent(sw);
        sw.GetValue(StateProperty)?.Dispose();
        sw.SetValue(StateProperty, null);
        sw.TemplateApplied -= OnTemplateApplied;
        sw.IsCheckedChanged -= OnCheckedChanged;
        sw.PointerEntered -= OnPointerEntered;
        sw.PointerExited -= OnPointerExited;
        if (enabled)
        {
            sw.TemplateApplied += OnTemplateApplied;
            sw.IsCheckedChanged += OnCheckedChanged;
            sw.PointerEntered += OnPointerEntered;
            sw.PointerExited += OnPointerExited;
        }
    }

    private static void RefreshStateContent(ToggleSwitch sw) =>
        sw.Classes.Set(StateContentClass,
            sw.IsSet(ToggleSwitch.OnContentProperty) || sw.IsSet(ToggleSwitch.OffContentProperty));

    private static void OnPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        var sw = (ToggleSwitch)sender!;
        if (sw.GetValue(StateProperty) is { } state)
        {
            state.Hovered = true;
            Refresh(sw);
        }
    }

    private static void OnPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        var sw = (ToggleSwitch)sender!;
        if (sw.GetValue(StateProperty) is { } state)
        {
            state.Hovered = false;
            Refresh(sw);
        }
    }

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        var sw = (ToggleSwitch)sender!;
        sw.GetValue(StateProperty)?.Dispose();
        sw.SetValue(StateProperty, null);

        var knobs = e.NameScope.Find<Panel>("PART_MovingKnobs");
        var travelCanvas = e.NameScope.Find<Panel>("PART_SwitchKnob");
        var track = e.NameScope.Find<Border>("Track");
        if (knobs is null || travelCanvas is null || track is null)
            return;

        var state = new TrackingState(sw, knobs, travelCanvas, track);
        state.Subscription = knobs.GetObservable(Canvas.LeftProperty)
            .Subscribe(new AnonymousObserver<double>(left => state.Update(left)));
        sw.SetValue(StateProperty, state);
    }

    private static void Refresh(ToggleSwitch sw)
    {
        if (sw.GetValue(StateProperty) is { } state)
            state.Update(Canvas.GetLeft(state.Knobs));
    }

    private sealed class TrackingState : IDisposable
    {
        private bool _observingAccessibility;
        private readonly IBrush? _originalTrackBackground;
        private readonly Transitions? _originalKnobTransitions;

        public TrackingState(ToggleSwitch sw, Panel knobs, Panel travelCanvas, Border track)
        {
            Switch = sw;
            Knobs = knobs;
            TravelCanvas = travelCanvas;
            Track = track;
            _originalTrackBackground = track.Background;
            _originalKnobTransitions = knobs.Transitions;
            Track.Background = TrackBrush;

            PressTransitions = CreateTransitions(
                PressTravelDuration, PressScaleDuration, new SineEaseOut());
            ReleaseTransitions = CreateTransitions(
                ReleaseTravelDuration, ReleaseScaleDuration, new LinearEasing());

            ReleaseTimer = new DispatcherTimer { Interval = ReleaseMaterialHold };
            ReleaseTimer.Tick += OnReleaseTimerTick;
            Switch.AttachedToVisualTree += OnAttached;
            Switch.DetachedFromVisualTree += OnDetached;
            if (Switch.IsAttachedToVisualTree())
                ObserveAccessibility();
            // Observe releases before ToggleSwitch updates Canvas.Left.
            Switch.AddHandler(InputElement.PointerPressedEvent, OnSwitchPointerPressed,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            Switch.AddHandler(InputElement.PointerReleasedEvent, OnSwitchPointerReleased,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            Switch.AddHandler(InputElement.KeyDownEvent, OnSwitchKeyDown,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            Switch.AddHandler(InputElement.KeyUpEvent, OnSwitchKeyUp,
                RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        public bool IsUserToggling { get; private set; }

        private void BeginUserToggle() => IsUserToggling = true;

        private void EndUserToggle() =>
            Dispatcher.UIThread.Post(() => IsUserToggling = false, DispatcherPriority.Input);

        private void OnSwitchKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                BeginUserToggle();
        }

        private void OnSwitchKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                EndUserToggle();
        }

        public ToggleSwitch Switch { get; }
        public Panel Knobs { get; }
        public Panel TravelCanvas { get; }
        public Border Track { get; }
        public SolidColorBrush TrackBrush { get; } = new();
        public Transitions PressTransitions { get; }
        public Transitions ReleaseTransitions { get; }
        public DispatcherTimer ReleaseTimer { get; }
        public IDisposable? Subscription { get; set; }
        public bool Hovered { get; set; }

        public void Update(double left)
        {
            var travel = TravelCanvas.Width;
            if (double.IsNaN(travel) || travel <= 0)
                travel = TravelCanvas.Bounds.Width;
            if (travel <= 0)
                return;

            double progress;
            if (double.IsNaN(left))
                progress = Switch.IsChecked == true ? 1.0 : 0.0;
            else
                progress = Math.Clamp(left / travel, 0.0, 1.0);

            var color = Lerp(GetOffTint(Switch), GetOnTint(Switch), progress);
            TrackBrush.Color = Hovered ? Lerp(color, Colors.White, HoverMix) : color;
        }

        public void Dispose()
        {
            ReleaseTimer.Stop();
            ReleaseTimer.Tick -= OnReleaseTimerTick;
            StopObservingAccessibility();
            Switch.AttachedToVisualTree -= OnAttached;
            Switch.DetachedFromVisualTree -= OnDetached;
            Switch.RemoveHandler(InputElement.PointerPressedEvent, OnSwitchPointerPressed);
            Switch.RemoveHandler(InputElement.PointerReleasedEvent, OnSwitchPointerReleased);
            Switch.RemoveHandler(InputElement.KeyDownEvent, OnSwitchKeyDown);
            Switch.RemoveHandler(InputElement.KeyUpEvent, OnSwitchKeyUp);
            Switch.Classes.Remove(ReleasingClass);
            IsUserToggling = false;
            Subscription?.Dispose();
            if (ReferenceEquals(Track.Background, TrackBrush))
                Track.Background = _originalTrackBackground;
            Knobs.Transitions = _originalKnobTransitions;
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => ObserveAccessibility();

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ReleaseTimer.Stop();
            Switch.Classes.Remove(ReleasingClass);
            IsUserToggling = false;
            StopObservingAccessibility();
        }

        private void ObserveAccessibility()
        {
            if (_observingAccessibility)
                return;
            _observingAccessibility = true;
            CupertinoAccessibility.Changed += OnAccessibilityChanged;
            OnAccessibilityChanged(null, EventArgs.Empty);
        }

        private void StopObservingAccessibility()
        {
            if (!_observingAccessibility)
                return;
            _observingAccessibility = false;
            CupertinoAccessibility.Changed -= OnAccessibilityChanged;
        }

        private void OnAccessibilityChanged(object? sender, EventArgs e)
        {
            Knobs.Transitions = CupertinoAccessibility.ReduceMotion ? null : ReleaseTransitions;
            if (CupertinoAccessibility.ReduceMotion)
            {
                ReleaseTimer.Stop();
                Switch.Classes.Remove(ReleasingClass);
            }
        }

        private void OnSwitchPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(Switch).Properties.IsLeftButtonPressed)
                return;
            BeginUserToggle();
            PointerCaptureWatch.OnLost(e.Pointer, () => IsUserToggling = false);
            Knobs.Transitions = CupertinoAccessibility.ReduceMotion ? null : PressTransitions;
            ReleaseTimer.Stop();
            Switch.Classes.Remove(ReleasingClass);
        }

        private void OnSwitchPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!IsUserToggling)
                return;
            EndUserToggle();
            // Preserve release material through spring travel.
            Knobs.Transitions = CupertinoAccessibility.ReduceMotion ? null : ReleaseTransitions;
            if (CupertinoAccessibility.ReduceMotion)
            {
                ReleaseTimer.Stop();
                Switch.Classes.Remove(ReleasingClass);
                return;
            }
            Switch.Classes.Add(ReleasingClass);
            ReleaseTimer.Stop();
            ReleaseTimer.Start();
        }

        private void OnReleaseTimerTick(object? sender, EventArgs e)
        {
            ReleaseTimer.Stop();
            Switch.Classes.Remove(ReleasingClass);
        }

        private static Transitions CreateTransitions(
            TimeSpan travelDuration, TimeSpan scaleDuration, Easing scaleEasing) =>
        [
            new DoubleTransition
            {
                Property = Canvas.LeftProperty,
                Duration = travelDuration,
                Easing = new SwitchTravelEasing(),
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = scaleDuration,
                Easing = scaleEasing,
            },
        ];

        private static Color Lerp(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }
    }
}
