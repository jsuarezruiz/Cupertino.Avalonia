using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Cupertino.Rendering;

/// <summary>
/// Immutable glass-rendering parameters.
/// </summary>
internal readonly record struct GlassParams(
    float RadiusTopLeft,
    float RadiusTopRight,
    float RadiusBottomRight,
    float RadiusBottomLeft,
    float BlurRadius,
    float Saturation,
    float Thickness,
    float Refraction,
    float Chroma,
    float Depth,
    float TintR,
    float TintG,
    float TintB,
    float TintA,
    float LightAngleDegrees,
    float LightIntensity,
    float Fresnel,
    float Adaptive,
    float Magnify,
    float ShadowOpacity,
    float ShadowBlur,
    float ShadowOffset,
    float ShadowContactWeight)
{
    public static GlassParams From(Controls.GlassSurface s)
    {
        var tint = s.Tint;
        var cr = s.CornerRadius;
        return new GlassParams(
            (float)cr.TopLeft, (float)cr.TopRight, (float)cr.BottomRight, (float)cr.BottomLeft,
            (float)s.BlurRadius,
            (float)s.Saturation,
            (float)s.GlassThickness,
            (float)s.RefractionStrength,
            (float)s.ChromaticAberration,
            (float)s.DepthEffect,
            tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f,
            (float)s.LightAngle,
            (float)s.LightIntensity,
            (float)s.FresnelStrength,
            s.IsAdaptive ? 1f : 0f,
            (float)s.Magnification,
            (float)s.ShadowOpacity,
            (float)s.ShadowBlur,
            (float)s.ShadowOffset,
            (float)s.ShadowContactWeight);
    }
}

internal sealed class LiquidGlassDrawOperation : ICustomDrawOperation
{
    private readonly GlassParams _params;

    private readonly Rect _surface;
    private readonly bool _freezeBackdrop;
    private SKImage? _frozenBackdrop;

    public LiquidGlassDrawOperation(
        Rect surface, Rect bounds, GlassParams parameters, bool freezeBackdrop)
    {
        _surface = surface;
        Bounds = bounds;
        _params = parameters;
        _freezeBackdrop = freezeBackdrop;
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => _surface.Contains(p);

    // Glass depends on its current backdrop.
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
        _frozenBackdrop?.Dispose();
        _frozenBackdrop = null;
    }

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null)
        {
            var tint = Color.FromArgb((byte)Math.Clamp(_params.TintA * 255 + 60, 0, 255),
                (byte)(_params.TintR * 255), (byte)(_params.TintG * 255), (byte)(_params.TintB * 255));
            context.DrawRectangle(new Avalonia.Media.Immutable.ImmutableSolidColorBrush(tint), null,
                _surface, _params.RadiusTopLeft, _params.RadiusTopLeft);
            return;
        }

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;
        var surface = lease.SkSurface;

        if (surface is null)
        {
            RenderFallback(canvas, null, default, 1f);
            return;
        }

        var ctm = canvas.TotalMatrix;
        // The shader works in device space, so independent axis scales are safe:
        // deviceRect already contains the transformed size.  Use the vertical
        // scale for radii and optical distances so a source-bound flyout morph
        // never switches to the flat fallback while approaching scale(1).
        if (!float.IsFinite(ctm.ScaleX) || ctm.ScaleX <= 0.001f ||
            !float.IsFinite(ctm.ScaleY) || ctm.ScaleY <= 0.001f ||
            ctm.SkewX != 0 || ctm.SkewY != 0 ||
            ctm.Persp0 != 0 || ctm.Persp1 != 0 || ctm.Persp2 != 1)
        {
            RenderFallback(canvas, null, default, 1f);
            return;
        }
        var localBounds = new SKRect(0, 0, (float)_surface.Width, (float)_surface.Height);
        var deviceRect = ctm.MapRect(localBounds);

        var scale = ctm.ScaleY;

        var sigma = _params.BlurRadius * scale * 0.5f;
        var padding = MathF.Ceiling(sigma * 3f) + MathF.Ceiling((_params.Refraction * 1.35f + 2f) * scale);
        var paddedWidth = MathF.Ceiling(deviceRect.Width) + padding * 2;
        var paddedHeight = MathF.Ceiling(deviceRect.Height) + padding * 2;
        // Include sampling padding in the limit; never convert an unbounded float to int.
        // At four bytes per pixel, one offscreen allocation stays at or below 64 MiB.
        if (!float.IsFinite(paddedWidth) || !float.IsFinite(paddedHeight) ||
            paddedWidth < 1 || paddedHeight < 1 || paddedWidth > 8192 || paddedHeight > 8192 ||
            (double)paddedWidth * paddedHeight > 16 * 1024 * 1024)
        {
            RenderFallback(canvas, null, default, 1f);
            return;
        }
        var pad = (int)padding;
        var info = new SKImageInfo((int)paddedWidth, (int)paddedHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var blurSurface = lease.GrContext is { } grContext
            ? SKSurface.Create(grContext, false, info)
            : SKSurface.Create(info);
        if (blurSurface is null)
        {
            RenderFallback(canvas, null, default, 1f);
            return;
        }

        using var transientSnapshot = _freezeBackdrop ? null : surface.Snapshot();
        var snapshot = transientSnapshot ?? (_frozenBackdrop ??= surface.Snapshot());

        // Draw the shadow after capture so glass cannot sample it.
        if (_params.ShadowOpacity > 0.002f)
        {
            var rTL = _params.RadiusTopLeft * scale;
            var rTR = _params.RadiusTopRight * scale;
            var rBR = _params.RadiusBottomRight * scale;
            var rBL = _params.RadiusBottomLeft * scale;
            canvas.Save();
            canvas.SetMatrix(SKMatrix.Identity);
            var keepOut = SKRect.Inflate(deviceRect, -0.75f, -0.75f);
            using (var clip = CreateRoundRect(
                       keepOut,
                       Math.Max(0, rTL - 0.75f), Math.Max(0, rTR - 0.75f),
                       Math.Max(0, rBR - 0.75f), Math.Max(0, rBL - 0.75f)))
                canvas.ClipRoundRect(clip, SKClipOperation.Difference, true);

            // Split opacity between contact and ambient layers.
            var wc = Math.Clamp(_params.ShadowContactWeight, 0f, 1f);
            DrawShadowLayer(canvas, deviceRect, rTL, rTR, rBR, rBL,
                _params.ShadowOpacity * wc, _params.ShadowBlur * 0.55f, _params.ShadowOffset, scale);
            DrawShadowLayer(canvas, deviceRect, rTL, rTR, rBR, rBL,
                _params.ShadowOpacity * (1.3f - wc), _params.ShadowBlur * 1.8f, _params.ShadowOffset * 0.6f, scale);
            canvas.Restore();
        }

        // Pass 1: crop, blur and saturate into a padded offscreen surface.
        var bc = blurSurface.Canvas;
        bc.Clear(SKColors.Transparent);
        using var blurFilter = sigma > 0.01f
            ? SKImageFilter.CreateBlur(sigma, sigma, SKShaderTileMode.Clamp) : null;
        using var saturationFilter = Math.Abs(_params.Saturation - 1f) > 0.001f
            ? SKColorFilter.CreateColorMatrix(CreateSaturationMatrix(_params.Saturation)) : null;
        using (var blurPaint = new SKPaint { ImageFilter = blurFilter, ColorFilter = saturationFilter })
            bc.DrawImage(snapshot, pad - deviceRect.Left, pad - deviceRect.Top, blurPaint);

        using var blurred = blurSurface.Snapshot();
        using var blurredShader = blurred.ToShader(
            SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Linear),
            SKMatrix.CreateTranslation(-pad, -pad));

        // Pass 2: render the glass effect in device space.
        var effect = LiquidGlassShader.Effect;
        if (effect is null)
        {
            RenderFallback(canvas, blurred, deviceRect, scale, pad);
            return;
        }

        var lightRad = _params.LightAngleDegrees * MathF.PI / 180f;

        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["uOrigin"] = new[] { deviceRect.Left, deviceRect.Top },
            ["uSize"] = new[] { deviceRect.Width, deviceRect.Height },
            ["uCornerRadii"] = new[]
            {
                _params.RadiusTopLeft * scale,
                _params.RadiusTopRight * scale,
                _params.RadiusBottomRight * scale,
                _params.RadiusBottomLeft * scale,
            },
            // Avoid division by zero in the rim mask.
            ["uThickness"] = MathF.Max(0.01f, _params.Thickness * scale),
            ["uRefraction"] = _params.Refraction * scale,
            ["uChroma"] = _params.Chroma,
            ["uDepth"] = _params.Depth,
            ["uMagnify"] = _params.Magnify,
            ["uTint"] = new[] { _params.TintR, _params.TintG, _params.TintB, _params.TintA },
            ["uLightDir"] = new[] { MathF.Cos(lightRad), -MathF.Sin(lightRad) },
            ["uLightIntensity"] = _params.LightIntensity,
            ["uFresnel"] = _params.Fresnel,
            ["uAdaptive"] = _params.Adaptive,
            ["uPad"] = (float)pad,
        };

        using var children = new SKRuntimeEffectChildren(effect)
        {
            ["uBlurred"] = blurredShader,
        };

        using var glassShader = effect.ToShader(uniforms, children);
        using var paint = new SKPaint { Shader = glassShader };

        canvas.Save();
        canvas.SetMatrix(SKMatrix.Identity);
        using (var clip = CreateRoundRect(
                   deviceRect,
                   _params.RadiusTopLeft * scale,
                   _params.RadiusTopRight * scale,
                   _params.RadiusBottomRight * scale,
                   _params.RadiusBottomLeft * scale))
            canvas.ClipRoundRect(clip, antialias: true);
        canvas.DrawRect(SKRect.Inflate(deviceRect, 1, 1), paint);
        canvas.Restore();
    }

    private static void DrawShadowLayer(
        SKCanvas canvas, SKRect deviceRect,
        float radiusTopLeft, float radiusTopRight, float radiusBottomRight, float radiusBottomLeft,
        float opacity, float blur, float offset, float scale)
    {
        var rect = deviceRect;
        rect.Offset(0, offset * scale);
        using var maskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.5f, blur * scale * 0.5f));
        using var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)Math.Clamp(opacity * 255f, 0, 255)),
            MaskFilter = maskFilter,
            IsAntialias = true,
        };
        using var roundRect = CreateRoundRect(
            rect, radiusTopLeft, radiusTopRight, radiusBottomRight, radiusBottomLeft);
        canvas.DrawRoundRect(roundRect, paint);
    }

    // Fallback when runtime effects or a readable render surface are unavailable.
    private void RenderFallback(
        SKCanvas canvas, SKImage? blurred, SKRect deviceRect, float scale, int pad = 0)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(
                (byte)(_params.TintR * 255), (byte)(_params.TintG * 255), (byte)(_params.TintB * 255),
                (byte)Math.Clamp(_params.TintA * 255 + 60, 0, 255)),
            IsAntialias = true,
        };

        if (blurred is not null && deviceRect.Width > 0)
        {
            canvas.Save();
            canvas.SetMatrix(SKMatrix.Identity);
            using (var clip = CreateRoundRect(
                       deviceRect,
                       _params.RadiusTopLeft * scale,
                       _params.RadiusTopRight * scale,
                       _params.RadiusBottomRight * scale,
                       _params.RadiusBottomLeft * scale))
                canvas.ClipRoundRect(clip, antialias: true);
            var source = new SKRect(
                pad, pad,
                Math.Max(pad, blurred.Width - pad),
                Math.Max(pad, blurred.Height - pad));
            canvas.DrawImage(
                blurred, source, deviceRect,
                new SKSamplingOptions(SKFilterMode.Linear), null);
            canvas.DrawRect(deviceRect, paint);
            canvas.Restore();
            return;
        }

        var rect = new SKRect(0, 0, (float)_surface.Width, (float)_surface.Height);
        using var roundRect = CreateRoundRect(
            rect,
            _params.RadiusTopLeft,
            _params.RadiusTopRight,
            _params.RadiusBottomRight,
            _params.RadiusBottomLeft);
        canvas.DrawRoundRect(roundRect, paint);
    }

    private static SKRoundRect CreateRoundRect(
        SKRect rect,
        float radiusTopLeft, float radiusTopRight, float radiusBottomRight, float radiusBottomLeft)
    {
        var roundRect = new SKRoundRect();
        roundRect.SetRectRadii(rect,
        [
            new SKPoint(radiusTopLeft, radiusTopLeft),
            new SKPoint(radiusTopRight, radiusTopRight),
            new SKPoint(radiusBottomRight, radiusBottomRight),
            new SKPoint(radiusBottomLeft, radiusBottomLeft),
        ]);
        return roundRect;
    }

    private static float[] CreateSaturationMatrix(float s)
    {
        const float lr = 0.2126f;
        const float lg = 0.7152f;
        const float lb = 0.0722f;
        var ir = (1f - s) * lr;
        var ig = (1f - s) * lg;
        var ib = (1f - s) * lb;
        return
        [
            ir + s, ig, ib, 0, 0,
            ir, ig + s, ib, 0, 0,
            ir, ig, ib + s, 0, 0,
            0, 0, 0, 1, 0,
        ];
    }
}
