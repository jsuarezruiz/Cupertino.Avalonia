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
using Cupertino.Animation;

namespace Cupertino.Controls;

/// <summary>
/// Drives switch colour and lighting from the knob position.
/// </summary>
public static class SwitchInteraction
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, bool>("IsEnabled", typeof(SwitchInteraction));

    public static readonly AttachedProperty<Color> OnTintProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, Color>("OnTint", typeof(SwitchInteraction));

    public static readonly AttachedProperty<Color> OffTintProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, Color>("OffTint", typeof(SwitchInteraction));

    private static readonly AttachedProperty<TrackingState?> StateProperty =
        AvaloniaProperty.RegisterAttached<ToggleSwitch, TrackingState?>("State", typeof(SwitchInteraction));

    // Both state fills are flat. The shared knob supplies the held glass.
    private const double HoverMix = 0.06;
    private const string ReleasingClass = "cupertino-switch-releasing";
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
    }

    public static void SetIsEnabled(ToggleSwitch element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(ToggleSwitch element) => element.GetValue(IsEnabledProperty);

    public static void SetOnTint(ToggleSwitch element, Color value) => element.SetValue(OnTintProperty, value);
    public static Color GetOnTint(ToggleSwitch element) => element.GetValue(OnTintProperty);

    public static void SetOffTint(ToggleSwitch element, Color value) => element.SetValue(OffTintProperty, value);
    public static Color GetOffTint(ToggleSwitch element) => element.GetValue(OffTintProperty);

    private static void OnCheckedChanged(object? sender, RoutedEventArgs e) =>
        CupertinoHaptics.Play(HapticFeedback.ImpactLight);

    private static void OnIsEnabledChanged(ToggleSwitch sw, bool enabled)
    {
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
        public TrackingState(ToggleSwitch sw, Panel knobs, Panel travelCanvas, Border track)
        {
            Switch = sw;
            Knobs = knobs;
            TravelCanvas = travelCanvas;
            Track = track;
            Track.Background = TrackBrush;

            PressTransitions = CreateTransitions(
                PressTravelDuration, PressScaleDuration, new SineEaseOut());
            ReleaseTransitions = CreateTransitions(
                ReleaseTravelDuration, ReleaseScaleDuration, new LinearEasing());

            ReleaseTimer = new DispatcherTimer { Interval = ReleaseMaterialHold };
            ReleaseTimer.Tick += OnReleaseTimerTick;
            // Observe releases before ToggleSwitch updates Canvas.Left.
            Switch.AddHandler(InputElement.PointerPressedEvent, OnSwitchPointerPressed,
                RoutingStrategies.Tunnel, handledEventsToo: true);
            Switch.AddHandler(InputElement.PointerReleasedEvent, OnSwitchPointerReleased,
                RoutingStrategies.Tunnel, handledEventsToo: true);
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
            Switch.RemoveHandler(InputElement.PointerPressedEvent, OnSwitchPointerPressed);
            Switch.RemoveHandler(InputElement.PointerReleasedEvent, OnSwitchPointerReleased);
            Switch.Classes.Remove(ReleasingClass);
            Subscription?.Dispose();
        }

        private void OnSwitchPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            Knobs.Transitions = PressTransitions;
            ReleaseTimer.Stop();
            Switch.Classes.Remove(ReleasingClass);
        }

        private void OnSwitchPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // Preserve release material through spring travel.
            Knobs.Transitions = ReleaseTransitions;
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
