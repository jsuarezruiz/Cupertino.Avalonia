using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Cupertino.Controls;

/// <summary>
/// Paints a search-field shadow outside the field's layout bounds.
/// </summary>
public sealed class CupertinoSearchFieldShadow : Control
{
    public static readonly AttachedProperty<bool> IsAdornerEnabledProperty =
        AvaloniaProperty.RegisterAttached<CupertinoSearchFieldShadow, TextBox, bool>("IsAdornerEnabled");

    private static readonly AttachedProperty<CupertinoSearchFieldShadow?> AdornerProperty =
        AvaloniaProperty.RegisterAttached<CupertinoSearchFieldShadow, TextBox, CupertinoSearchFieldShadow?>("Adorner");

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, CornerRadius>(
            nameof(CornerRadius), new CornerRadius(22.5));

    public static readonly StyledProperty<double> ShadowOpacityProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, double>(
            nameof(ShadowOpacity), 0.073);

    public static readonly StyledProperty<double> ShadowSigmaProperty =
        AvaloniaProperty.Register<CupertinoSearchFieldShadow, double>(
            nameof(ShadowSigma), 15.0);

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

        IsAdornerEnabledProperty.Changed.AddClassHandler<TextBox>((field, change) =>
        {
            var adorner = field.GetValue(AdornerProperty);
            if (change.GetNewValue<bool>())
            {
                if (adorner is not null)
                    return;

                adorner = new CupertinoSearchFieldShadow { IsHitTestVisible = false };
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

    public static bool GetIsAdornerEnabled(TextBox field) => field.GetValue(IsAdornerEnabledProperty);

    public static void SetIsAdornerEnabled(TextBox field, bool value) =>
        field.SetValue(IsAdornerEnabledProperty, value);

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double ShadowOpacity
    {
        get => GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }

    public double ShadowSigma
    {
        get => GetValue(ShadowSigmaProperty);
        set => SetValue(ShadowSigmaProperty, value);
    }

    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

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

            var surfaceRect = deviceRect;
            deviceRect.Offset(0, (float)_offset * scale);
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

            canvas.Save();
            canvas.SetMatrix(SKMatrix.Identity);
            // Exclude the translucent interior so the shadow cannot darken it.
            var separation = 0.75f * scale;
            var keepOut = SKRect.Inflate(surfaceRect, separation, separation);
            using (var clip = new SKRoundRect(
                       keepOut,
                       Math.Max(0, (float)_cornerRadius * scale + separation)))
            {
                canvas.ClipRoundRect(clip, SKClipOperation.Difference, true);
            }
            canvas.DrawRoundRect(
                deviceRect,
                (float)_cornerRadius * scale,
                (float)_cornerRadius * scale,
                paint);
            canvas.Restore();
        }
    }
}
