using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Tracks slider interaction state for the Cupertino thumb treatment.
/// </summary>
public static class SliderInteraction
{
    /// <summary>
    /// The CSS class applied while the slider thumb is being manipulated.
    /// </summary>
    public const string ActiveClass = "cupertino-active";
    /// <summary>
    /// The CSS class applied when the slider value equals its minimum.
    /// </summary>
    public const string AtMinimumClass = "cupertino-at-minimum";
    /// <summary>
    /// The CSS class applied when the slider value equals its maximum.
    /// </summary>
    public const string AtMaximumClass = "cupertino-at-maximum";

    private const string HeldReadyClass = "cupertino-held-ready";

    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Slider, bool>("IsEnabled", typeof(SliderInteraction));

    /// <summary>
    /// Identifies the <see cref="GetRailClip"/> attached setting, the rail clip shown while the thumb is held.
    /// </summary>
    public static readonly AttachedProperty<Geometry?> RailClipProperty =
        AvaloniaProperty.RegisterAttached<Thumb, Geometry?>("RailClip", typeof(SliderInteraction));

    /// <summary>
    /// Identifies the <see cref="GetRimTopBrush"/> attached setting, the upper rim brush of the held lens.
    /// </summary>
    public static readonly AttachedProperty<IBrush?> RimTopBrushProperty =
        AvaloniaProperty.RegisterAttached<Thumb, IBrush?>("RimTopBrush", typeof(SliderInteraction));

    /// <summary>
    /// Identifies the <see cref="GetRimBottomBrush"/> attached setting, the lower rim brush of the held lens.
    /// </summary>
    public static readonly AttachedProperty<IBrush?> RimBottomBrushProperty =
        AvaloniaProperty.RegisterAttached<Thumb, IBrush?>("RimBottomBrush", typeof(SliderInteraction));

    private static readonly AttachedProperty<Thumb?> ThumbProperty =
        AvaloniaProperty.RegisterAttached<Slider, Thumb?>("Thumb", typeof(SliderInteraction));

    private static readonly AttachedProperty<TopLevel?> ReleaseRootProperty =
        AvaloniaProperty.RegisterAttached<Slider, TopLevel?>("ReleaseRoot", typeof(SliderInteraction));

    private static readonly AttachedProperty<EventHandler<PointerReleasedEventArgs>?> ReleaseHandlerProperty =
        AvaloniaProperty.RegisterAttached<Slider, EventHandler<PointerReleasedEventArgs>?>("ReleaseHandler", typeof(SliderInteraction));

    static SliderInteraction()
    {
        IsEnabledProperty.Changed.AddClassHandler<Slider>((slider, e) =>
        {
            slider.TemplateApplied -= OnTemplateApplied;
            slider.RemoveHandler(InputElement.PointerPressedEvent, (EventHandler<PointerPressedEventArgs>)OnPressed);
            slider.RemoveHandler(InputElement.PointerReleasedEvent, (EventHandler<PointerReleasedEventArgs>)OnReleased);
            slider.PropertyChanged -= OnSliderPropertyChanged;
            slider.SizeChanged -= OnSliderSizeChanged;
            DisarmTopLevelRelease(slider);

            if (slider.GetValue(ThumbProperty) is { } oldThumb)
            {
                oldThumb.Classes.Remove(ActiveClass);
                oldThumb.Classes.Remove(AtMinimumClass);
                oldThumb.Classes.Remove(AtMaximumClass);
                oldThumb.ClearValue(RailClipProperty);
                oldThumb.ClearValue(RimTopBrushProperty);
                oldThumb.ClearValue(RimBottomBrushProperty);
            }
            slider.ClearValue(ThumbProperty);

            if (e.GetNewValue<bool>())
            {
                slider.TemplateApplied += OnTemplateApplied;
                slider.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
                slider.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
                slider.PropertyChanged += OnSliderPropertyChanged;
                slider.SizeChanged += OnSliderSizeChanged;
            }
        });
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(Slider element, bool value) => element.SetValue(IsEnabledProperty, value);
    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(Slider element) => element.GetValue(IsEnabledProperty);
    /// <inheritdoc cref="RailClipProperty"/>
    public static void SetRailClip(Thumb element, Geometry? value) => element.SetValue(RailClipProperty, value);
    /// <inheritdoc cref="RailClipProperty"/>
    public static Geometry? GetRailClip(Thumb element) => element.GetValue(RailClipProperty);
    /// <inheritdoc cref="RimTopBrushProperty"/>
    public static void SetRimTopBrush(Thumb element, IBrush? value) => element.SetValue(RimTopBrushProperty, value);
    /// <inheritdoc cref="RimTopBrushProperty"/>
    public static IBrush? GetRimTopBrush(Thumb element) => element.GetValue(RimTopBrushProperty);
    /// <inheritdoc cref="RimBottomBrushProperty"/>
    public static void SetRimBottomBrush(Thumb element, IBrush? value) => element.SetValue(RimBottomBrushProperty, value);
    /// <inheritdoc cref="RimBottomBrushProperty"/>
    public static IBrush? GetRimBottomBrush(Thumb element) => element.GetValue(RimBottomBrushProperty);

    private static void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        var slider = (Slider)sender!;
        slider.SetValue(ThumbProperty, e.NameScope.Find<Thumb>("thumb"));
        UpdateLensBrushes(slider);
        UpdateRailClip(slider);
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Slider slider)
            return;

        UpdateRailClip(slider);
        SetActive(slider, true);
        ArmTopLevelRelease(slider);
        PointerCaptureWatch.OnLost(e.Pointer, () => Deactivate(slider));
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Slider slider)
            Deactivate(slider);
    }

    private static void Deactivate(Slider slider)
    {
        SetActive(slider, false);
        DisarmTopLevelRelease(slider);
    }

    private static void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty)
            OnValueChanged(sender, e);
        else if (e.Property == RangeBase.MinimumProperty ||
                 e.Property == RangeBase.MaximumProperty ||
                 e.Property == Slider.IsDirectionReversedProperty ||
                 e.Property == Slider.OrientationProperty)
            UpdateRailClip(sender as Slider);
        else if (e.Property == TemplatedControl.ForegroundProperty)
            UpdateLensBrushes(sender as Slider);
    }

    private static void OnValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not Slider slider)
            return;

        var value = e.GetNewValue<double>();
        UpdateRailClip(slider);
        if (slider.GetValue(ThumbProperty)?.Classes.Contains(ActiveClass) != true)
            return;
        if (value <= slider.Minimum || value >= slider.Maximum)
            CupertinoHaptics.Play(HapticFeedback.ImpactLight);
        else if (slider.TickFrequency > 0 && slider.IsSnapToTickEnabled)
            CupertinoHaptics.Play(HapticFeedback.Selection);
    }

    private static void OnSliderSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateRailClip(sender as Slider);

    private static void UpdateLensBrushes(Slider? slider)
    {
        if (slider?.GetValue(ThumbProperty) is not { } thumb)
            return;

        if (slider.Foreground is ISolidColorBrush solid)
        {
            thumb.SetValue(RimTopBrushProperty, CreateTopRim(solid.Color));
            thumb.SetValue(RimBottomBrushProperty, CreateBottomRim(solid.Color));
        }
        else
        {
            // Preserve non-solid custom brushes.
            thumb.SetValue(RimTopBrushProperty, slider.Foreground);
            thumb.SetValue(RimBottomBrushProperty, slider.Foreground);
        }
    }

    private static LinearGradientBrush CreateTopRim(Color accent)
    {
        var systemBlue = IsSystemBlue(accent);
        return Gradient(
            ("#FFD9E1E6", 0), ("#FFB8CAD4", 0.20),
            (systemBlue ? Color.Parse("#FF9BC4E0") : Mix(Color.Parse("#FFC8CDD0"), accent, 0.18), 0.32),
            (systemBlue ? Color.Parse("#FF6FA7D3") : Mix(Color.Parse("#FFB1B5B8"), accent, 0.36), 0.35),
            (systemBlue ? Color.Parse("#FF5A96CA") : Mix(Color.Parse("#FFA0A4A7"), accent, 0.50), 0.38),
            (systemBlue ? Color.Parse("#FF5489C5") : Mix(Color.Parse("#FF999DA0"), accent, 0.55), 0.41),
            (systemBlue ? Color.Parse("#FF678FB6") : Mix(Color.Parse("#FFA8ACAF"), accent, 0.40), 0.44),
            ("#FF8399A8", 0.47), ("#FF899DAC", 0.53), ("#FF9DB2BB", 0.59),
            ("#FFD6E1E2", 0.65), ("#FFD4D3D1", 1));
    }

    private static LinearGradientBrush CreateBottomRim(Color accent)
    {
        var systemBlue = IsSystemBlue(accent);
        return Gradient(
            ("#FFE1E6EA", 0), (systemBlue ? "#FFDCE5EF" : "#FFDCE3E7", 0.20),
            (systemBlue ? Color.Parse("#FFB5D4FA") : Mix(Color.Parse("#FFDEE2E8"), accent, 0.12), 0.32),
            (systemBlue ? Color.Parse("#FFBCD3FE") : Mix(Color.Parse("#FFDFE3E9"), accent, 0.16), 0.35),
            (systemBlue ? Color.Parse("#FFE1E6FE") : Mix(Color.Parse("#FFEAEDF0"), accent, 0.08), 0.38),
            (systemBlue ? "#FFF0FBFD" : "#FFF4F6F7", 0.41),
            (systemBlue ? "#FFEFFBFC" : "#FFF5F7F8", 0.60),
            (systemBlue ? "#FFE9EEF5" : "#FFECEFF1", 0.72),
            ("#FFE1E6EA", 1));
    }

    private static bool IsSystemBlue(Color color) =>
        color.R <= 40 && color.G is >= 115 and <= 165 && color.B >= 240;

    private static Color Mix(Color neutral, Color accent, double amount)
    {
        static byte Channel(byte from, byte to, double t) =>
            (byte)Math.Round(from + (to - from) * t);
        return Color.FromArgb(
            Channel(neutral.A, accent.A, amount), Channel(neutral.R, accent.R, amount),
            Channel(neutral.G, accent.G, amount), Channel(neutral.B, accent.B, amount));
    }

    private static LinearGradientBrush Gradient(params (object Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        foreach (var (value, offset) in stops)
        {
            var color = value switch
            {
                Color c => c,
                string text => Color.Parse(text),
                _ => Colors.Transparent,
            };
            brush.GradientStops.Add(new GradientStop(color, offset));
        }
        return brush;
    }

    private static void UpdateRailClip(Slider? slider)
    {
        if (slider?.GetValue(ThumbProperty) is not { } thumb)
            return;

        if (slider.Orientation != Orientation.Horizontal ||
            slider.Bounds.Width <= 0 || thumb.Bounds.Width <= 0 || thumb.Bounds.Height <= 0)
        {
            thumb.ClearValue(RailClipProperty);
            thumb.Classes.Remove(AtMinimumClass);
            thumb.Classes.Remove(AtMaximumClass);
            return;
        }

        var range = slider.Maximum - slider.Minimum;
        var progress = range > 0 ? (slider.Value - slider.Minimum) / range : 0;
        progress = Math.Clamp(progress, 0, 1);
        if (slider.IsDirectionReversed)
            progress = 1 - progress;

        SetClass(thumb, AtMinimumClass, progress <= 0.000001);
        SetClass(thumb, AtMaximumClass, progress >= 0.999999);

        // Clip only endpoint overhang.
        const double activeScale = 1.5;
        var thumbWidth = thumb.Bounds.Width;
        var travel = Math.Max(0, slider.Bounds.Width - thumbWidth);
        var overhang = thumbWidth * (activeScale - 1) / 2;
        var left = Math.Max(0, overhang - progress * travel) / activeScale;
        var right = Math.Max(0, overhang - (1 - progress) * travel) / activeScale;

        // Move the opposite rounded edge outside the lens.
        const double railHeight = 3.8;
        const double railRadius = railHeight / 2;
        var clipLeft = left > 0 ? left : -railRadius;
        var clipRight = right > 0 ? thumbWidth - right : thumbWidth + railRadius;

        // Mid-range dragging recomputes the same rect on every move; skip the
        // geometry churn when nothing changed.
        var clipRect = new Rect(clipLeft, 0, Math.Max(0, clipRight - clipLeft), railHeight);
        if (thumb.GetValue(RailClipProperty) is RectangleGeometry existing
            && existing.Rect == clipRect
            && existing.RadiusX == railRadius && existing.RadiusY == railRadius)
            return;

        thumb.SetValue(RailClipProperty, new RectangleGeometry
        {
            Rect = clipRect,
            RadiusX = railRadius,
            RadiusY = railRadius,
        });
    }

    private static void SetClass(Thumb thumb, string name, bool present)
    {
        if (present && !thumb.Classes.Contains(name))
            thumb.Classes.Add(name);
        else if (!present)
            thumb.Classes.Remove(name);
    }
    private static void ArmTopLevelRelease(Slider slider)
    {
        DisarmTopLevelRelease(slider);

        if (TopLevel.GetTopLevel(slider) is not { } root)
            return;

        EventHandler<PointerReleasedEventArgs>? handler = null;
        handler = (_, _) =>
        {
            SetActive(slider, false);
            DisarmTopLevelRelease(slider);
        };

        slider.SetValue(ReleaseRootProperty, root);
        slider.SetValue(ReleaseHandlerProperty, handler);
        root.AddHandler(InputElement.PointerReleasedEvent, handler, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static void DisarmTopLevelRelease(Slider slider)
    {
        var root = slider.GetValue(ReleaseRootProperty);
        var handler = slider.GetValue(ReleaseHandlerProperty);
        if (root is not null && handler is not null)
            root.RemoveHandler(InputElement.PointerReleasedEvent, handler);

        slider.ClearValue(ReleaseRootProperty);
        slider.ClearValue(ReleaseHandlerProperty);
    }

    private static void SetActive(object? sender, bool active)
    {
        if (sender is Slider slider && slider.GetValue(ThumbProperty) is { } thumb)
        {
            if (active && !thumb.Classes.Contains(ActiveClass))
            {
                thumb.Classes.Add(HeldReadyClass);
                thumb.Classes.Add(ActiveClass);
            }
            else if (!active)
                thumb.Classes.Remove(ActiveClass);
        }
    }
}
