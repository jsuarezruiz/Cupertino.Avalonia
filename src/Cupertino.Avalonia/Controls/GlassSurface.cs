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
/// <remarks>
/// Supports translation and axis-aligned scaling. Rotation, skew and perspective use a flat
/// tinted fill, as do Reduce Transparency, software-rendered browsers and backends that cannot
/// compile the effect.
/// </remarks>
public class GlassSurface : Decorator
{
    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> property.
    /// </summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlassSurface, CornerRadius>(nameof(CornerRadius), new CornerRadius(24),
            coerce: (_, value) => new CornerRadius(
                Normalize(value.TopLeft, 24, 0, 10000), Normalize(value.TopRight, 24, 0, 10000),
                Normalize(value.BottomRight, 24, 0, 10000), Normalize(value.BottomLeft, 24, 0, 10000)));

    /// <summary>
    /// Identifies the <see cref="BlurRadius"/> property.
    /// </summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(BlurRadius), 20.0, coerce: (_, value) => Normalize(value, 20.0, 0, 128));

    /// <summary>
    /// Identifies the <see cref="Saturation"/> property.
    /// </summary>
    public static readonly StyledProperty<double> SaturationProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(Saturation), 1.5, coerce: (_, value) => Normalize(value, 1.5, 0, 8));

    /// <summary>
    /// Identifies the <see cref="GlassThickness"/> property.
    /// </summary>
    public static readonly StyledProperty<double> GlassThicknessProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(GlassThickness), 16.0, coerce: (_, value) => Normalize(value, 16.0, 0, 512));

    /// <summary>
    /// Identifies the <see cref="RefractionStrength"/> property.
    /// </summary>
    public static readonly StyledProperty<double> RefractionStrengthProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(RefractionStrength), 24.0, coerce: (_, value) => Normalize(value, 24.0, 0, 256));

    /// <summary>
    /// Identifies the <see cref="ChromaticAberration"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ChromaticAberrationProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ChromaticAberration), 0.5, coerce: (_, value) => Normalize(value, 0.5, 0, 1));

    /// <summary>
    /// Identifies the <see cref="DepthEffect"/> property.
    /// </summary>
    public static readonly StyledProperty<double> DepthEffectProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(DepthEffect), 0.15, coerce: (_, value) => Normalize(value, 0.15, 0, 1));

    /// <summary>
    /// Identifies the <see cref="Tint"/> property.
    /// </summary>
    public static readonly StyledProperty<Color> TintProperty =
        AvaloniaProperty.Register<GlassSurface, Color>(nameof(Tint), Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

    /// <summary>
    /// Identifies the <see cref="LightAngle"/> property.
    /// </summary>
    public static readonly StyledProperty<double> LightAngleProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(LightAngle), 135.0, coerce: (_, value) => Normalize(value, 135.0, -36000, 36000));

    /// <summary>
    /// Identifies the <see cref="LightIntensity"/> property.
    /// </summary>
    public static readonly StyledProperty<double> LightIntensityProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(LightIntensity), 1.2, coerce: (_, value) => Normalize(value, 1.2, 0, 8));

    /// <summary>
    /// Identifies the <see cref="FresnelStrength"/> property.
    /// </summary>
    public static readonly StyledProperty<double> FresnelStrengthProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(FresnelStrength), 1.0, coerce: (_, value) => Normalize(value, 1.0, 0, 8));

    /// <summary>
    /// Identifies the <see cref="ShadowOpacity"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowOpacityProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowOpacity), 0.18, coerce: (_, value) => Normalize(value, 0.18, 0, 1));

    /// <summary>
    /// Identifies the <see cref="ShadowBlur"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowBlurProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowBlur), 14.0, coerce: (_, value) => Normalize(value, 14.0, 0, 128));

    /// <summary>
    /// Identifies the <see cref="ShadowOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowOffsetProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowOffset), 4.0, coerce: (_, value) => Normalize(value, 4.0, -512, 512));

    /// <summary>
    /// Identifies the <see cref="ShadowContactWeight"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowContactWeightProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(ShadowContactWeight), 0.85, coerce: (_, value) => Normalize(value, 0.85, 0, 1));

    /// <summary>
    /// Identifies the <see cref="Magnification"/> property.
    /// </summary>
    public static readonly StyledProperty<double> MagnificationProperty =
        AvaloniaProperty.Register<GlassSurface, double>(nameof(Magnification), 1.0, coerce: (_, value) => Normalize(value, 1.0, 0.01, 8));

    /// <summary>
    /// Convex magnification of the whole interior, clamped to 0.01–8; 1 is flat.
    /// </summary>
    public double Magnification
    {
        get => GetValue(MagnificationProperty);
        set => SetValue(MagnificationProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsAdaptive"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsAdaptiveProperty =
        AvaloniaProperty.Register<GlassSurface, bool>(nameof(IsAdaptive), true);

    private static double Normalize(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    static GlassSurface()
    {
        AffectsRender<GlassSurface>(
            CornerRadiusProperty, BlurRadiusProperty, SaturationProperty,
            GlassThicknessProperty, RefractionStrengthProperty, ChromaticAberrationProperty,
            DepthEffectProperty, TintProperty, LightAngleProperty, LightIntensityProperty,
            FresnelStrengthProperty, IsAdaptiveProperty, MagnificationProperty,
            ShadowOpacityProperty, ShadowBlurProperty, ShadowOffsetProperty,
            ShadowContactWeightProperty, IsBackdropFrozenProperty);
    }

    /// <summary>
    /// The corner radii, in logical pixels.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Frost blur radius in logical pixels; the Gaussian sigma is half this value. Values are clamped to 0–128.
    /// </summary>
    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    /// <summary>
    /// Backdrop saturation boost, clamped to 0–8; 1 leaves the backdrop unchanged.
    /// </summary>
    public double Saturation
    {
        get => GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>
    /// Depth of the refracting bevel band along the edges, in logical pixels.
    /// </summary>
    public double GlassThickness
    {
        get => GetValue(GlassThicknessProperty);
        set => SetValue(GlassThicknessProperty, value);
    }

    /// <summary>
    /// How far the edge band displaces the backdrop sample, in logical pixels.
    /// </summary>
    public double RefractionStrength
    {
        get => GetValue(RefractionStrengthProperty);
        set => SetValue(RefractionStrengthProperty, value);
    }

    /// <summary>
    /// Chromatic aberration amount in the refraction band, from 0 to 1.
    /// </summary>
    public double ChromaticAberration
    {
        get => GetValue(ChromaticAberrationProperty);
        set => SetValue(ChromaticAberrationProperty, value);
    }

    /// <summary>
    /// Radial lens component that makes the surface read as slightly convex, from 0 to 1.
    /// </summary>
    public double DepthEffect
    {
        get => GetValue(DepthEffectProperty);
        set => SetValue(DepthEffectProperty, value);
    }

    /// <summary>
    /// Glass tint; the alpha channel controls tint strength.
    /// </summary>
    public Color Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>
    /// Direction the key light comes from, in degrees; 135 is upper-left.
    /// </summary>
    public double LightAngle
    {
        get => GetValue(LightAngleProperty);
        set => SetValue(LightAngleProperty, value);
    }

    /// <summary>
    /// The edge-light intensity, clamped to 0–8; nonfinite values use 1.2.
    /// </summary>
    public double LightIntensity
    {
        get => GetValue(LightIntensityProperty);
        set => SetValue(LightIntensityProperty, value);
    }

    /// <summary>
    /// Strength of the bright fresnel rim at the very edge, clamped to 0–8.
    /// </summary>
    public double FresnelStrength
    {
        get => GetValue(FresnelStrengthProperty);
        set => SetValue(FresnelStrengthProperty, value);
    }

    /// <summary>
    /// Balance between the contact and ambient shadow layers, from 0 to 1.
    /// </summary>
    public double ShadowContactWeight
    {
        get => GetValue(ShadowContactWeightProperty);
        set => SetValue(ShadowContactWeightProperty, value);
    }

    /// <summary>
    /// Shadow opacity, where zero removes the shadow and one is fully opaque.
    /// </summary>
    public double ShadowOpacity
    {
        get => GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }

    /// <summary>
    /// Shadow blur radius, in logical pixels.
    /// </summary>
    public double ShadowBlur
    {
        get => GetValue(ShadowBlurProperty);
        set => SetValue(ShadowBlurProperty, value);
    }

    /// <summary>
    /// Vertical shadow displacement, in logical pixels.
    /// </summary>
    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    /// <summary>
    /// Whether the material adapts its veil and lighting to backdrop luminance.
    /// </summary>
    public bool IsAdaptive
    {
        get => GetValue(IsAdaptiveProperty);
        set => SetValue(IsAdaptiveProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsLive"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<GlassSurface, bool>(nameof(IsLive), false);

    /// <summary>
    /// Whether the surface repaints continuously for independently animated backdrops.
    /// </summary>
    public bool IsLive
    {
        get => GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsBackdropFrozen"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsBackdropFrozenProperty =
        AvaloniaProperty.Register<GlassSurface, bool>(nameof(IsBackdropFrozen), false);

    /// <summary>
    /// Keeps the first clean backdrop captured after this is set, or after the surface attaches, until it is cleared or the surface detaches.
    /// Use this for modal materials whose obscured content cannot change while they are open.
    /// </summary>
    public bool IsBackdropFrozen
    {
        get => GetValue(IsBackdropFrozenProperty);
        set => SetValue(IsBackdropFrozenProperty, value);
    }

    private static readonly ConditionalWeakTable<TopLevel, TopLevelPulseCoordinator> Coordinators = new();

    private static bool UsesFlatMaterial =>
        CupertinoAccessibility.ReduceTransparency || LiquidGlassDrawOperation.IsBrowserRaster;

    internal static void InvalidateAllSurfaces()
    {
        foreach (var pair in Coordinators)
            pair.Value.InvalidateSurfaces();
    }

    private long _pulseUntil;
    private long _foregroundAt = long.MinValue;
    private LiquidGlassDrawOperation.ForegroundState? _foreground;
    private DispatcherTimer? _sampleRelease;
    private TopLevelPulseCoordinator? _coordinator;
    private FrozenBackdrop _frozenBackdrop = new();

    // Keep sampling briefly after input or layout activity, sharing one frame
    // callback per top level instead of a timer for every surface.
    private const int PulseMilliseconds = 350;
    private const int ForegroundSampleMilliseconds = 1000;

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        CupertinoAccessibility.Changed += OnAccessibilityChanged;

        if (TopLevel.GetTopLevel(this) is { } top)
        {
            _coordinator = Coordinators.GetValue(top, static value => new TopLevelPulseCoordinator(value));
            _coordinator.Add(this);
        }

        if (!IsBackdropFrozen)
            Pulse();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CupertinoAccessibility.Changed -= OnAccessibilityChanged;
        _coordinator?.Remove(this);
        _coordinator = null;
        _sampleRelease?.Stop();
        ResetFrozenBackdrop();
    }

    private void ResetFrozenBackdrop()
    {
        _frozenBackdrop.Retire();
        _frozenBackdrop = new FrozenBackdrop();
    }

    [ThreadStatic] private static HashSet<GlassSurface>? _pulsedScratch;
    [ThreadStatic] private static List<GlassSurface>? _behindScratch;

    /// <summary>
    /// Repaints the glass behind a control for the next few frames.
    /// </summary>
    /// <remarks>
    /// Call this from controls that run their own animation, so nearby glass does not
    /// sample a stale backdrop.
    /// </remarks>
    public static void PulseBehind(Visual visual) => ForEachBehind(visual, extend: true);

    // Step animations inside glass: redraw from the glass's clean sample, or repaint its backdrop once.
    internal static void ForegroundChanged(Visual visual) => ForEachBehind(visual, extend: false);

    private static void ForEachBehind(Visual visual, bool extend)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (UsesFlatMaterial
            || TopLevel.GetTopLevel(visual) is not { } top
            || !Coordinators.TryGetValue(top, out var coordinator)
            || !coordinator.HasSurfaces)
            return;

        var pulsed = _pulsedScratch ??= new HashSet<GlassSurface>();
        var behind = _behindScratch ??= new List<GlassSurface>();
        pulsed.Clear();
        behind.Clear();
        var branch = visual;
        while (branch.GetVisualParent() is { } parent)
        {
            // Ancestors, and earlier siblings as in popover and picker templates.
            if (parent is GlassSurface ancestor && pulsed.Add(ancestor))
                behind.Add(ancestor);

            if (parent is Panel panel && branch is Control child)
            {
                var branchIndex = panel.Children.IndexOf(child);
                for (var i = 0; i < branchIndex; ++i)
                    if (panel.Children[i] is GlassSurface sibling && pulsed.Add(sibling))
                        behind.Add(sibling);
            }

            branch = parent;
        }

        // Other glass over the control samples it, so it needs the full repaint.
        var reuse = !extend && behind.Count > 0 && !coordinator.HasOtherSurfaceOver(visual, pulsed);
        foreach (var surface in behind)
            surface.PulseNow(visual, extend, reuse);
        behind.Clear();
    }

    /// <summary>
    /// Repaints the surface for the next few frames.
    /// </summary>
    public void Pulse()
    {
        _pulseUntil = MotionClock.Now + PulseMilliseconds;
        _coordinator?.RequestFrame();
    }

    // Only for controls already invalidated this frame; other paths stay deferred
    // because a synchronous repaint during resize layout breaks macOS full screen.
    private void PulseNow(Visual source, bool extend, bool reuse = false)
    {
        if (!extend)
        {
            _foregroundAt = MotionClock.Now;
            ScheduleSampleRelease();
            // Redraw rects grow slightly past the control; stay clear of the rounded edge.
            if (reuse && source.TransformToVisual(this) is { } transform
                && new Rect(source.Bounds.Size).TransformToAABB(transform).Inflate(2) is var area
                && _foreground is { HasCleanSample: true } foreground
                && LiquidGlassDrawOperation.InsideShape(area, new Rect(Bounds.Size).Deflate(1), CornerRadius))
            {
                foreground.Mark(area);
                return;
            }
            _coordinator?.InvalidateNow(this);
            return;
        }
        _pulseUntil = MotionClock.Now + PulseMilliseconds;
        _coordinator?.PulseNow(this);
    }

    internal bool HasActivePulse => _pulseUntil > MotionClock.Now;

    // Once content stops animating, redraw once so the kept sample is released.
    private void ScheduleSampleRelease()
    {
        if (_sampleRelease is null)
        {
            _sampleRelease = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ForegroundSampleMilliseconds) };
            _sampleRelease.Tick += OnSampleRelease;
        }
        _sampleRelease.Stop();
        _sampleRelease.Start();
    }

    private void OnSampleRelease(object? sender, EventArgs e)
    {
        _sampleRelease!.Stop();
        // The timer and the clock can disagree by a few milliseconds; end the window explicitly.
        _foregroundAt = long.MinValue;
        if (_foreground is { HasCleanSample: true })
            InvalidateVisual();
    }

    // A transparent ancestor hides the glass as surely as its own opacity.
    private bool IsShown
    {
        get
        {
            if (!IsEffectivelyVisible)
                return false;
            for (Visual? visual = this; visual is not null; visual = visual.GetVisualParent())
                if (visual.Opacity <= 0)
                    return false;
            return true;
        }
    }

    internal static int GetBackdropInvalidationCount(TopLevel top) =>
        Coordinators.TryGetValue(top, out var coordinator) ? coordinator.BackdropInvalidations : 0;

    internal FrozenBackdrop BackdropCapture => _frozenBackdrop;

    internal object? RedrawState => _foreground;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBackdropFrozenProperty)
        {
            ResetFrozenBackdrop();
            if (change.GetNewValue<bool>())
                _pulseUntil = 0;
            else
                Pulse();
        }
        else if (change.Property == IsLiveProperty && change.GetNewValue<bool>()
                 || change.Property == OpacityProperty && change.GetOldValue<double>() <= 0
                    && change.GetNewValue<double>() > 0)
            Pulse();
    }

    private sealed class TopLevelPulseCoordinator
    {
        private readonly TopLevel _top;
        private readonly HashSet<GlassSurface> _surfaces = new();
        private readonly List<GlassSurface> _due = new();
        // Cached once per coordinator: RequestFrame and the per-frame prune run
        // often enough that the closure and predicate allocations showed up.
        private readonly Action<TimeSpan> _onFrame;
        private readonly Predicate<GlassSurface> _isDetached;
        private bool _framePending;
        private bool _disposed;
        private Size _clientSize;
        private double _scaling;

        public TopLevelPulseCoordinator(TopLevel top)
        {
            _top = top;
            _onFrame = OnFrame;
            _isDetached = IsDetached;
            _clientSize = top.ClientSize;
            _scaling = top.RenderScaling;
            // Note: PointerMoved intentionally does not arm a pulse. Hover movement
            // does not change backdrop pixels (hover tints repaint the glass itself
            // through AffectsRender), and arming every surface on every mousemove
            // kept the whole window repainting continuously on desktop browsers.
            _top.PointerPressed += OnActivity;
            _top.PointerReleased += OnActivity;
            _top.PointerWheelChanged += OnActivity;
            _top.LayoutUpdated += OnActivity;
        }

        public void Add(GlassSurface surface) => _surfaces.Add(surface);

        public bool HasOtherSurfaceOver(Visual source, HashSet<GlassSurface> behind)
        {
            if (source.TransformToVisual(_top) is not { } transform)
                return true;
            var area = new Rect(source.Bounds.Size).TransformToAABB(transform);
            foreach (var surface in _surfaces)
            {
                if (behind.Contains(surface) || !surface.IsShown)
                    continue;
                if (surface.TransformToVisual(_top) is not { } other
                    || new Rect(surface.Bounds.Size).TransformToAABB(other).Intersects(area))
                    return true;
            }
            return false;
        }

        internal bool HasSurfaces => _surfaces.Count != 0;

        public void InvalidateSurfaces()
        {
            foreach (var surface in _surfaces)
                surface.InvalidateVisual();
            RequestFrame();
        }

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
            // Layout already repaints every surface on resize.
            if (TopLevelResized())
                return;

            var until = MotionClock.Now + PulseMilliseconds;
            var armed = false;
            foreach (var surface in _surfaces)
                if (!surface.IsBackdropFrozen)
                {
                    surface._pulseUntil = until;
                    armed |= surface.IsShown;
                }

            // Skip a full-window frame when nothing visible needs it.
            if (armed)
                RequestFrame();
        }

        private bool TopLevelResized()
        {
            var size = _top.ClientSize;
            var scaling = _top.RenderScaling;
            if (size == _clientSize && scaling.Equals(_scaling))
                return false;

            _clientSize = size;
            _scaling = scaling;
            foreach (var surface in _surfaces)
            {
                // A frozen capture is stale after a resize.
                if (surface.IsBackdropFrozen)
                    surface.ResetFrozenBackdrop();
                surface.InvalidateVisual();
            }
            return true;
        }

        internal int BackdropInvalidations { get; private set; }

        // Repaint the backdrop in the caller's frame; deferring it lets the glass sample
        // its own previous output.
        public void PulseNow(GlassSurface surface)
        {
            InvalidateNow(surface);
            RequestFrame();
        }

        public void InvalidateNow(GlassSurface surface)
        {
            if (_disposed || UsesFlatMaterial || !surface.IsShown)
                return;

            _top.InvalidateVisual();
            surface.InvalidateVisual();
            ++BackdropInvalidations;
        }

        public void RequestFrame()
        {
            if (_disposed || _framePending || _surfaces.Count == 0 || UsesFlatMaterial)
                return;

            _framePending = true;
            _top.RequestAnimationFrame(_onFrame);
        }

        private bool IsDetached(GlassSurface surface) =>
            !surface.IsAttachedToVisualTree() || TopLevel.GetTopLevel(surface) != _top;

        private void OnFrame(TimeSpan _)
        {
            _framePending = false;
            if (_disposed || UsesFlatMaterial)
                return;

            _surfaces.RemoveWhere(_isDetached);
            if (_surfaces.Count == 0)
            {
                Dispose();
                Coordinators.Remove(_top);
                return;
            }

            var now = MotionClock.Now;
            var waitingLive = false;
            _due.Clear();
            foreach (var surface in _surfaces)
            {
                if (surface.IsBackdropFrozen || (!surface.IsLive && now >= surface._pulseUntil))
                    continue;
                if (!surface.IsShown)
                {
                    // Live glass behind a fading ancestor must resume without a pulse.
                    waitingLive |= surface.IsLive && surface.IsEffectivelyVisible;
                    continue;
                }

                _due.Add(surface);
            }
            if (_due.Count == 0 && waitingLive)
                RequestFrame();
            else if (_due.Count > 0)
            {
                // Repaint the backdrop before sampling; invalidating only the
                // glass can leave previously rendered glass in retained pixels.
                _top.InvalidateVisual();
                foreach (var surface in _due)
                    surface.InvalidateVisual();
                _due.Clear();
                RequestFrame();
            }
        }

        private void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _top.PointerPressed -= OnActivity;
            _top.PointerReleased -= OnActivity;
            _top.PointerWheelChanged -= OnActivity;
            _top.LayoutUpdated -= OnActivity;
            _surfaces.Clear();
            _due.Clear();
        }
    }

    private void OnAccessibilityChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
        if (!UsesFlatMaterial)
            Pulse();
    }

    /// <inheritdoc/>
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

        if (UsesFlatMaterial)
        {
            _foreground = null;
            if (LiquidGlassDrawOperation.IsBrowserRaster)
                context.Custom(new BrowserBackendProbe(bounds));
            RenderPlain(context, bounds);
            return;
        }

        // Keep a sample only while content inside is animating.
        var operation = new LiquidGlassDrawOperation(
            bounds, opBounds, GlassParams.From(this), IsBackdropFrozen ? _frozenBackdrop : null,
            retainSample: _foregroundAt > MotionClock.Now - ForegroundSampleMilliseconds);
        context.Custom(operation);
        // RenderTargetBitmap and VisualBrush draw it at once and never dispose it.
        if (operation.HasDrawn)
            operation.Dispose();
        else
            _foreground = operation.Foreground;
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
