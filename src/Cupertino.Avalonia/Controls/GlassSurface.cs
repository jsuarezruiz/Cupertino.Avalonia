using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Rendering;

namespace Cupertino.Controls;

/// <summary>
/// Renders a live, refractive glass material behind its child.
/// </summary>
public class GlassSurface : Decorator
{
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlassSurface, CornerRadius>(nameof(CornerRadius), new CornerRadius(24));

    /// <summary>
    /// Frost blur sigma, in logical pixels.
    /// </summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(BlurRadius), 20.0);

    /// <summary>
    /// Backdrop saturation boost (1 = unchanged).
    /// </summary>
    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(Saturation), 1.5);

    /// <summary>
    /// Depth of the refracting bevel band along the edges, in logical pixels.
    /// </summary>
    public static readonly StyledProperty<double> GlassThicknessProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(GlassThickness), 16.0);

    /// <summary>
    /// How far the edge band displaces the backdrop sample, in logical pixels.
    /// </summary>
    public static readonly StyledProperty<double> RefractionStrengthProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(RefractionStrength), 24.0);

    /// <summary>
    /// Chromatic aberration amount in the refraction band (0..1).
    /// </summary>
    public static readonly StyledProperty<double> ChromaticAberrationProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ChromaticAberration), 0.5);

    /// <summary>
    /// Adds a radial component to the lens so the surface reads as slightly convex (0..1).
    /// </summary>
    public static readonly StyledProperty<double> DepthEffectProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(DepthEffect), 0.15);

    /// <summary>
    /// Glass tint; alpha controls tint strength.
    /// </summary>
    public static readonly StyledProperty<Color> TintProperty =
        AvaloniaProperty.Register<GlassSurface, Color>(nameof(Tint), Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    /// <summary>
    /// Direction the key light comes from, in degrees (135 = upper-left).
    /// </summary>
    public static readonly StyledProperty<double> LightAngleProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(LightAngle), 135.0);

    public static readonly StyledProperty<double> LightIntensityProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(LightIntensity), 1.2);

    /// <summary>
    /// Strength of the bright fresnel rim at the very edge (0..1).
    /// </summary>
    public static readonly StyledProperty<double> FresnelStrengthProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(FresnelStrength), 1.0);

    /// <summary>
    /// Opacity of the drop shadow cast by the surface (0 disables it).
    /// </summary>
    public static readonly StyledProperty<double> ShadowOpacityProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowOpacity), 0.18);

    /// <summary>
    /// Shadow blur radius, in logical pixels.
    /// </summary>
    public static readonly StyledProperty<double> ShadowBlurProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowBlur), 14.0);

    /// <summary>
    /// Vertical shadow offset, in logical pixels.
    /// </summary>
    public static readonly StyledProperty<double> ShadowOffsetProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowOffset), 4.0);

    /// <summary>
    /// Balances contact and ambient shadow layers.
    /// </summary>
    public static readonly StyledProperty<double> ShadowContactWeightProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowContactWeight), 0.85);

    /// <summary>
    /// Convex magnification; 1 is flat.
    /// </summary>
    public static readonly StyledProperty<double> MagnificationProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(Magnification), 1.0);

    public double Magnification
    {
        get => GetValue(MagnificationProperty);
        set => SetValue(MagnificationProperty, value);
    }

    /// <summary>
    /// Adapts the material to backdrop luminance.
    /// </summary>
    public static readonly StyledProperty<bool> IsAdaptiveProperty =
        AvaloniaProperty.Register<GlassSurface, bool>(nameof(IsAdaptive), true);

    static GlassSurface()
    {
        AffectsRender<GlassSurface>(
            CornerRadiusProperty, BlurRadiusProperty, SaturationProperty,
            GlassThicknessProperty, RefractionStrengthProperty, ChromaticAberrationProperty,
            DepthEffectProperty, TintProperty, LightAngleProperty, LightIntensityProperty,
            FresnelStrengthProperty, IsAdaptiveProperty, MagnificationProperty,
            ShadowOpacityProperty, ShadowBlurProperty, ShadowOffsetProperty,
            ShadowContactWeightProperty);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public double GlassThickness
    {
        get => GetValue(GlassThicknessProperty);
        set => SetValue(GlassThicknessProperty, value);
    }

    public double RefractionStrength
    {
        get => GetValue(RefractionStrengthProperty);
        set => SetValue(RefractionStrengthProperty, value);
    }

    public double ChromaticAberration
    {
        get => GetValue(ChromaticAberrationProperty);
        set => SetValue(ChromaticAberrationProperty, value);
    }

    public double DepthEffect
    {
        get => GetValue(DepthEffectProperty);
        set => SetValue(DepthEffectProperty, value);
    }

    public Color Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public double LightAngle
    {
        get => GetValue(LightAngleProperty);
        set => SetValue(LightAngleProperty, value);
    }

    public double LightIntensity
    {
        get => GetValue(LightIntensityProperty);
        set => SetValue(LightIntensityProperty, value);
    }

    public double FresnelStrength
    {
        get => GetValue(FresnelStrengthProperty);
        set => SetValue(FresnelStrengthProperty, value);
    }

    public double ShadowContactWeight
    {
        get => GetValue(ShadowContactWeightProperty);
        set => SetValue(ShadowContactWeightProperty, value);
    }

    public double ShadowOpacity
    {
        get => GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }

    public double ShadowBlur
    {
        get => GetValue(ShadowBlurProperty);
        set => SetValue(ShadowBlurProperty, value);
    }

    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    public bool IsAdaptive
    {
        get => GetValue(IsAdaptiveProperty);
        set => SetValue(IsAdaptiveProperty, value);
    }

    /// <summary>
    /// Repaints continuously for independently animated backdrops.
    /// </summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<GlassSurface, bool>(nameof(IsLive), false);

    public bool IsLive
    {
        get => GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    private static readonly ConditionalWeakTable<TopLevel, TopLevelPulseCoordinator> Coordinators = new();

    private DateTime _pulseUntil;
    private TopLevelPulseCoordinator? _coordinator;

    // Partial repaints can resample already-rendered glass.
    private const int PulseMilliseconds = 350;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        CupertinoAccessibility.Changed += OnAccessibilityChanged;

        if (TopLevel.GetTopLevel(this) is { } top)
        {
            _coordinator = Coordinators.GetValue(top, static value => new TopLevelPulseCoordinator(value));
            _coordinator.Add(this);
        }

        Pulse();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CupertinoAccessibility.Changed -= OnAccessibilityChanged;
        _coordinator?.Remove(this);
        _coordinator = null;
    }

    /// <summary>
    /// Repaints the surface for the next few frames.
    /// </summary>
    public void Pulse()
    {
        _pulseUntil = DateTime.UtcNow.AddMilliseconds(PulseMilliseconds);
        _coordinator?.RequestFrame();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLiveProperty && change.GetNewValue<bool>())
            Pulse();
    }

    private sealed class TopLevelPulseCoordinator
    {
        private readonly TopLevel _top;
        private readonly HashSet<GlassSurface> _surfaces = new();
        private bool _framePending;
        private bool _disposed;

        public TopLevelPulseCoordinator(TopLevel top)
        {
            _top = top;
            _top.PointerMoved += OnActivity;
            _top.PointerPressed += OnActivity;
            _top.PointerReleased += OnActivity;
            _top.PointerWheelChanged += OnActivity;
            _top.LayoutUpdated += OnActivity;
        }

        public void Add(GlassSurface surface) => _surfaces.Add(surface);

        public void Remove(GlassSurface surface)
        {
            _surfaces.Remove(surface);
            if (_surfaces.Count != 0)
                return;

            Dispose();
            Coordinators.Remove(_top);
        }

        private void OnActivity(object? sender, EventArgs e)
        {
            var until = DateTime.UtcNow.AddMilliseconds(PulseMilliseconds);
            foreach (var surface in _surfaces)
                surface._pulseUntil = until;
            RequestFrame();
        }

        public void RequestFrame()
        {
            if (_disposed || _framePending || _surfaces.Count == 0)
                return;

            _framePending = true;
            _top.RequestAnimationFrame(_ =>
            {
                _framePending = false;
                if (_disposed)
                    return;

                _surfaces.RemoveWhere(surface =>
                    !surface.IsAttachedToVisualTree() || TopLevel.GetTopLevel(surface) != _top);
                if (_surfaces.Count == 0)
                {
                    Dispose();
                    Coordinators.Remove(_top);
                    return;
                }

                // Invalidate the top level once for all glass surfaces.
                _top.InvalidateVisual();

                var now = DateTime.UtcNow;
                if (_surfaces.Any(surface => surface.IsEffectivelyVisible
                    && (surface.IsLive || now < surface._pulseUntil)))
                {
                    RequestFrame();
                }
            });
        }

        private void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _top.PointerMoved -= OnActivity;
            _top.PointerPressed -= OnActivity;
            _top.PointerReleased -= OnActivity;
            _top.PointerWheelChanged -= OnActivity;
            _top.LayoutUpdated -= OnActivity;
            _surfaces.Clear();
        }
    }

    private void OnAccessibilityChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 1 || bounds.Height < 1)
            return;

        // Expand bounds to avoid clipping the ambient shadow.
        var extent = ShadowOpacity > 0.002
            ? ShadowBlur * 1.8 + Math.Abs(ShadowOffset) + 2
            : 0;
        var opBounds = extent > 0 ? bounds.Inflate(extent) : bounds;

        if (CupertinoAccessibility.ReduceTransparency)
        {
            RenderPlain(context, bounds);
            return;
        }

        context.Custom(new LiquidGlassDrawOperation(bounds, opBounds, GlassParams.From(this)));
    }

    // Flat fallback for Reduce Transparency.
    private void RenderPlain(DrawingContext context, Rect bounds)
    {
        var tint = Tint;
        var page = this.TryFindResource("CupertinoCardBrush", out var v) && v is ISolidColorBrush s
            ? s.Color
            : Colors.White;

        var a = tint.A / 255.0;
        var fill = Color.FromArgb(
            255,
            (byte)(tint.R * a + page.R * (1 - a)),
            (byte)(tint.G * a + page.G * (1 - a)),
            (byte)(tint.B * a + page.B * (1 - a)));

        if (ShadowOpacity > 0.002)
        {
            var shadow = new BoxShadow
            {
                Blur = ShadowBlur,
                OffsetY = ShadowOffset,
                Color = Color.FromArgb((byte)Math.Clamp(ShadowOpacity * 255, 0, 255), 0, 0, 0),
            };
            context.DrawRectangle(null, null, new RoundedRect(bounds, CornerRadius),
                                  new BoxShadows(shadow));
        }

        context.DrawRectangle(new SolidColorBrush(fill), null,
                              new RoundedRect(bounds, CornerRadius));
    }
}
