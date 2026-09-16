using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Cupertino.Controls;

/// <summary>
/// Paints a search-field shadow outside the field's layout bounds.
/// </summary>
public sealed class CupertinoSearchFieldShadow : Control
{
    private readonly List<Visual> _visibilitySources = new();

    /// <summary>
    /// Identifies the <see cref="GetIsAdornerEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsAdornerEnabledProperty =
        AvaloniaProperty.RegisterAttached<CupertinoSearchFieldShadow, Control, bool>("IsAdornerEnabled");

    private static readonly AttachedProperty<CupertinoSearchFieldShadow?> AdornerProperty =
        AvaloniaProperty.RegisterAttached<CupertinoSearchFieldShadow, Control, CupertinoSearchFieldShadow?>("Adorner");

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> property.
    /// </summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, CornerRadius>(
            nameof(CornerRadius), new CornerRadius(22.5));

    /// <summary>
    /// Identifies the <see cref="ShadowOpacity"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowOpacityProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, double>(
            nameof(ShadowOpacity), 0.073);

    /// <summary>
    /// Identifies the <see cref="ShadowSigma"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowSigmaProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, double>(
            nameof(ShadowSigma), 15.0);

    /// <summary>
    /// Identifies the <see cref="ShadowOffset"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowOffsetProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, double>(
            nameof(ShadowOffset), 5.0);

    static CupertinoSearchFieldShadow()
    {
        AffectsRender<CupertinoSearchFieldShadow>(
            CornerRadiusProperty,
            ShadowOpacityProperty,
            ShadowSigmaProperty,
            ShadowOffsetProperty);

        IsAdornerEnabledProperty.Changed.AddClassHandler<Control>((field, change) =>
        {
            var adorner = field.GetValue(AdornerProperty);
            if (change.GetNewValue<bool>())
            {
                if (adorner is not null)
                    return;

                adorner = new CupertinoSearchFieldShadow
                {
                    IsHitTestVisible = false,
                    IsVisible = field.IsEffectivelyVisible,
                };
                AdornerLayer.SetIsClipEnabled(adorner, false);
                field.SetValue(AdornerProperty, adorner);
                AdornerLayer.SetAdorner(field, adorner);
            }
            else if (adorner is not null)
            {
                AdornerLayer.SetAdorner(field, null);
                field.SetValue(AdornerProperty, null);
            }
        });
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The overlay is outside the field's visual ancestry, so it does not
        // inherit visibility when a page or an adaptive layout is hidden.
        if (AdornerLayer.GetAdornedElement(this) is { } field)
        {
            foreach (var source in field.GetSelfAndVisualAncestors())
            {
                source.PropertyChanged += OnSourcePropertyChanged;
                _visibilitySources.Add(source);
            }
            UpdateSourceVisibility();
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var source in _visibilitySources)
            source.PropertyChanged -= OnSourcePropertyChanged;
        _visibilitySources.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSourcePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
            UpdateSourceVisibility();
    }

    private void UpdateSourceVisibility()
    {
        // Read each source directly: its effective visibility may still be
        // propagating to descendants during the property-change notification.
        IsVisible = _visibilitySources.All(source => source.IsVisible);
    }

    /// <inheritdoc cref="IsAdornerEnabledProperty"/>
    public static bool GetIsAdornerEnabled(Control field) => field.GetValue(IsAdornerEnabledProperty);

    /// <inheritdoc cref="IsAdornerEnabledProperty"/>
    public static void SetIsAdornerEnabled(Control field, bool value) =>
        field.SetValue(IsAdornerEnabledProperty, value);

    /// <summary>
    /// The corner radii, in logical pixels.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
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
    /// The shadow Gaussian sigma in logical pixels.
    /// </summary>
    public double ShadowSigma
    {
        get => GetValue(ShadowSigmaProperty);
        set => SetValue(ShadowSigmaProperty, value);
    }

    /// <summary>
    /// Vertical shadow displacement, in logical pixels.
    /// </summary>
    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        const double materialInset = 2.0 / 3.0;
        var surface = new Rect(
            0,
            materialInset,
            Bounds.Width,
            Bounds.Height - materialInset * 2);
        if (surface.Width < 1 || surface.Height < 1 || ShadowOpacity <= 0)
            return;

        // Include the Gaussian tail, offset and antialiasing fringe.
        var extent = ShadowSigma * 3 + Math.Abs(ShadowOffset) + 2;
        context.Custom(new SearchFieldShadowDrawOperation(
            surface,
            surface.Inflate(extent),
            CornerRadius.TopLeft,
            ShadowOpacity,
            ShadowSigma,
            ShadowOffset));
    }

    private sealed class SearchFieldShadowDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _surface;
        private readonly double _cornerRadius;
        private readonly double _opacity;
        private readonly double _sigma;
        private readonly double _offset;

        public SearchFieldShadowDrawOperation(
            Rect surface,
            Rect bounds,
            double cornerRadius,
            double opacity,
            double sigma,
            double offset)
        {
            _surface = surface;
            Bounds = bounds;
            _cornerRadius = cornerRadius;
            _opacity = opacity;
            _sigma = sigma;
            _offset = offset;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) =>
            other is SearchFieldShadowDrawOperation operation &&
            operation._surface == _surface &&
            operation.Bounds == Bounds &&
            operation._cornerRadius.Equals(_cornerRadius) &&
            operation._opacity.Equals(_opacity) &&
            operation._sigma.Equals(_sigma) &&
            operation._offset.Equals(_offset);

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                return;

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var matrix = canvas.TotalMatrix;
            var deviceRect = matrix.MapRect(new SKRect(
                (float)_surface.X,
                (float)_surface.Y,
                (float)_surface.Right,
                (float)_surface.Bottom));
            var scale = matrix.ScaleX > 0.001f ? matrix.ScaleX : 1f;

            // Rasterize offscreen: a blur mask drawn on the macOS canvas can keep the
            // pre-full-screen render target size.
            var padding = MathF.Ceiling((float)_sigma * scale * 3f)
                + MathF.Ceiling(Math.Abs((float)_offset) * scale) + 2f;
            var width = MathF.Ceiling(deviceRect.Width) + padding * 2 + 1;
            var height = MathF.Ceiling(deviceRect.Height) + padding * 2 + 1;
            var offscreen = float.IsFinite(width) && float.IsFinite(height)
                && width >= 1 && height >= 1 && width <= 8192 && height <= 8192
                && matrix.SkewX == 0 && matrix.SkewY == 0
                && matrix.Persp0 == 0 && matrix.Persp1 == 0 && matrix.Persp2 == 1
                ? CreateSurface(lease.GrContext, (int)width, (int)height)
                : null;

            using (offscreen)
            {
                var pad = (int)padding;
                // Whole device pixels avoid resampling.
                var originX = MathF.Floor(deviceRect.Left) - pad;
                var originY = MathF.Floor(deviceRect.Top) - pad;
                var target = offscreen?.Canvas ?? canvas;

                var surfaceRect = deviceRect;
                if (offscreen is not null)
                    surfaceRect.Offset(-originX, -originY);
                var shadowRect = surfaceRect;
                shadowRect.Offset(0, (float)_offset * scale);

                using var paint = new SKPaint
                {
                    Color = new SKColor(
                        0,
                        0,
                        0,
                        (byte)Math.Clamp(Math.Round(_opacity * 255), 0, 255)),
                    IsAntialias = true,
                    MaskFilter = SKMaskFilter.CreateBlur(
                        SKBlurStyle.Normal,
                        Math.Max(0.5f, (float)_sigma * scale)),
                };

                if (offscreen is null)
                {
                    target.Save();
                    target.SetMatrix(SKMatrix.Identity);
                }
                else
                {
                    target.Clear(SKColors.Transparent);
                    target.Save();
                }

                // Exclude the translucent interior so the shadow cannot darken it.
                var separation = 0.75f * scale;
                var keepOut = SKRect.Inflate(surfaceRect, separation, separation);
                using (var clip = new SKRoundRect(
                           keepOut,
                           Math.Max(0, (float)_cornerRadius * scale + separation)))
                {
                    target.ClipRoundRect(clip, SKClipOperation.Difference, true);
                }
                target.DrawRoundRect(
                    shadowRect,
                    (float)_cornerRadius * scale,
                    (float)_cornerRadius * scale,
                    paint);
                target.Restore();

                if (offscreen is null)
                    return;

                using var image = offscreen.Snapshot();
                canvas.Save();
                canvas.SetMatrix(SKMatrix.Identity);
                canvas.DrawImage(image, originX, originY);
                canvas.Restore();
            }
        }

        private static SKSurface? CreateSurface(GRContext? grContext, int width, int height)
        {
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            return grContext is not null
                ? SKSurface.Create(grContext, true, info) ?? SKSurface.Create(grContext, false, info)
                : SKSurface.Create(info);
        }
    }
}
