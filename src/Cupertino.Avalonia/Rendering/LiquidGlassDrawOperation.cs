using Avalonia;
using Avalonia.Media;
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

// Backdrop snapshot shared across draw operations; disposed once retired and unheld.
internal sealed class FrozenBackdrop
{
    private readonly object _gate = new();
    private SKImage? _image;
    private bool _retired;
    private int _holders;

    public void AddHolder()
    {
        lock (_gate)
            _holders++;
    }

    public SKImage Capture(SKSurface surface)
    {
        lock (_gate)
            return _image ??= surface.Snapshot();
    }

    public void Retire()
    {
        lock (_gate)
        {
            _retired = true;
            ReleaseLocked();
        }
    }

    public void ReleaseIfRetired()
    {
        lock (_gate)
        {
            _holders--;
            ReleaseLocked();
        }
    }

    private void ReleaseLocked()
    {
        if (!_retired || _holders > 0)
            return;
        _image?.Dispose();
        _image = null;
    }
}

internal sealed class LiquidGlassDrawOperation : ICustomDrawOperation
{
    // Matches the observed native stack, where the contact and ambient shadows overlap.
    private const float ShadowContactOverdraw = 1.3f;

    // Per-thread scratch that avoids per-frame allocations.
    [ThreadStatic] private static float[]? _origin;
    [ThreadStatic] private static float[]? _size;
    [ThreadStatic] private static float[]? _radii;
    [ThreadStatic] private static float[]? _tint;
    [ThreadStatic] private static float[]? _lightDir;
    [ThreadStatic] private static float[]? _saturation;
    [ThreadStatic] private static SKRoundRect? _roundRect;
    [ThreadStatic] private static SKPoint[]? _corners;
    [ThreadStatic] private static float[]? _lumaSize;
    // Small quantized caches: pages mix several glass materials (button blur 12,
    // hero 24, menu 36), so a single cached sigma thrashed and recreated the
    // blur every glass every frame. Keys are quantized; entries are bounded.
    [ThreadStatic] private static Dictionary<int, SKImageFilter>? _blurCache;
    [ThreadStatic] private static Dictionary<int, SKColorFilter>? _saturationCache;
    [ThreadStatic] private static Dictionary<int, SKMaskFilter>? _shadowMaskCache;

    private static float[] Pair(ref float[]? storage, float a, float b)
    {
        var array = storage ??= new float[2];
        array[0] = a;
        array[1] = b;
        return array;
    }

    private static float[] Quad(ref float[]? storage, float a, float b, float c, float d)
    {
        var array = storage ??= new float[4];
        array[0] = a;
        array[1] = b;
        array[2] = c;
        array[3] = d;
        return array;
    }

    // Cached by quantized scalar input; callers pass continuously-varying values
    // (animation ticks, DPR scaling), so exact-float keys would never hit.
    private static SKImageFilter? BlurFilter(float sigma)
    {
        if (sigma <= 0.01f)
            return null;
        var key = (int)MathF.Round(sigma * 4f);
        var cache = _blurCache ??= new Dictionary<int, SKImageFilter>(16);
        if (cache.TryGetValue(key, out var filter))
            return filter;
        if (cache.Count >= 24)
        {
            foreach (var entry in cache.Values)
                entry.Dispose();
            cache.Clear();
        }
        filter = SKImageFilter.CreateBlur(key / 4f, key / 4f, SKShaderTileMode.Clamp);
        cache[key] = filter;
        return filter;
    }

    private static SKColorFilter? SaturationFilter(float saturation)
    {
        if (Math.Abs(saturation - 1f) <= 0.001f)
            return null;
        var key = (int)MathF.Round(saturation * 100f);
        var cache = _saturationCache ??= new Dictionary<int, SKColorFilter>(8);
        if (cache.TryGetValue(key, out var filter))
            return filter;
        if (cache.Count >= 16)
        {
            foreach (var entry in cache.Values)
                entry.Dispose();
            cache.Clear();
        }
        filter = SKColorFilter.CreateColorMatrix(CreateSaturationMatrix(key / 100f));
        cache[key] = filter;
        return filter;
    }

    private static SKMaskFilter ShadowMaskFilter(float radius)
    {
        var key = (int)MathF.Round(Math.Max(0.5f, radius) * 4f);
        var cache = _shadowMaskCache ??= new Dictionary<int, SKMaskFilter>(16);
        if (cache.TryGetValue(key, out var filter))
            return filter;
        if (cache.Count >= 24)
        {
            foreach (var entry in cache.Values)
                entry.Dispose();
            cache.Clear();
        }
        filter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, key / 4f);
        cache[key] = filter;
        return filter;
    }

    private readonly GlassParams _params;

    private readonly Rect _surface;
    private readonly FrozenBackdrop? _frozenBackdrop;

    public LiquidGlassDrawOperation(
        Rect surface, Rect bounds, GlassParams parameters, FrozenBackdrop? frozenBackdrop)
    {
        _surface = surface;
        Bounds = bounds;
        _params = parameters;
        _frozenBackdrop = frozenBackdrop;
        _frozenBackdrop?.AddHolder();
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => _surface.Contains(p);

    // Glass depends on its current backdrop.
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
        _frozenBackdrop?.ReleaseIfRetired();
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
        var glassPadding = MathF.Ceiling(sigma * 3f) + MathF.Ceiling((_params.Refraction * 1.35f + 2f) * scale);
        // Room for the shadow's ambient blur, which shares the offscreen target.
        var shadowPadding = _params.ShadowOpacity > 0.002f
            ? MathF.Ceiling(_params.ShadowBlur * scale * 2.8f)
              + MathF.Ceiling(Math.Abs(_params.ShadowOffset) * scale) + 2f
            : 0f;
        var padding = Math.Max(glassPadding, shadowPadding);
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
        // Budgeted so Skia can recycle it.
        using var blurSurface = lease.GrContext is { } grContext
            ? SKSurface.Create(grContext, true, info) ?? SKSurface.Create(grContext, false, info)
            : SKSurface.Create(info);
        if (blurSurface is null)
        {
            RenderFallback(canvas, null, default, 1f);
            return;
        }

        // Crop to the padded rect: a full copy-on-write snapshot leaves macOS on a stale
        // render target after native full screen.
        var cropLeft = (int)MathF.Floor(deviceRect.Left) - pad;
        var cropTop = (int)MathF.Floor(deviceRect.Top) - pad;
        using var croppedBackdrop = _frozenBackdrop is null
            ? surface.Snapshot(
                SKRectI.Create(cropLeft, cropTop, (int)paddedWidth + 2, (int)paddedHeight + 2))
            : null;
        if (croppedBackdrop is null && _frozenBackdrop is null)
        {
            RenderFallback(canvas, null, default, 1f);
            return;
        }

        var snapshot = croppedBackdrop ?? _frozenBackdrop!.Capture(surface);
        // Skia clamps the crop to the surface.
        var snapshotLeft = croppedBackdrop is null ? 0 : Math.Max(0, cropLeft);
        var snapshotTop = croppedBackdrop is null ? 0 : Math.Max(0, cropTop);

        var bc = blurSurface.Canvas;

        // Rasterize the shadow offscreen too (same macOS issue), before the blur pass
        // reuses the target.
        if (_params.ShadowOpacity > 0.002f)
        {
            var rTL = _params.RadiusTopLeft * scale;
            var rTR = _params.RadiusTopRight * scale;
            var rBR = _params.RadiusBottomRight * scale;
            var rBL = _params.RadiusBottomLeft * scale;
            var localRect = SKRect.Create(pad, pad, deviceRect.Width, deviceRect.Height);

            bc.Clear(SKColors.Transparent);
            bc.Save();
            var keepOut = SKRect.Inflate(localRect, -0.75f, -0.75f);
            bc.ClipRoundRect(
                CreateRoundRect(
                    keepOut,
                    Math.Max(0, rTL - 0.75f), Math.Max(0, rTR - 0.75f),
                    Math.Max(0, rBR - 0.75f), Math.Max(0, rBL - 0.75f)),
                SKClipOperation.Difference, true);

            // The layers sum to ShadowContactOverdraw x opacity; the weight splits it.
            var wc = Math.Clamp(_params.ShadowContactWeight, 0f, 1f);
            DrawShadowLayer(bc, localRect, rTL, rTR, rBR, rBL,
                _params.ShadowOpacity * wc, _params.ShadowBlur * 0.55f, _params.ShadowOffset, scale);
            DrawShadowLayer(bc, localRect, rTL, rTR, rBR, rBL,
                _params.ShadowOpacity * (ShadowContactOverdraw - wc), _params.ShadowBlur * 1.8f, _params.ShadowOffset * 0.6f, scale);
            bc.Restore();

            using (var shadow = blurSurface.Snapshot())
            {
                canvas.Save();
                canvas.SetMatrix(SKMatrix.Identity);
                canvas.DrawImage(shadow, deviceRect.Left - pad, deviceRect.Top - pad);
                canvas.Restore();
            }
        }

        // Pass 1: crop, blur and saturate into the padded offscreen surface.
        bc.Clear(SKColors.Transparent);
        var blurFilter = BlurFilter(sigma);
        var saturationFilter = SaturationFilter(_params.Saturation);
        using (var blurPaint = new SKPaint { ImageFilter = blurFilter, ColorFilter = saturationFilter })
            bc.DrawImage(
                snapshot,
                snapshotLeft + pad - deviceRect.Left,
                snapshotTop + pad - deviceRect.Top,
                blurPaint);

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

        // Average luma once per frame, not per fragment.
        using var lumaTexture = _params.Adaptive > 0.5f
            ? CreateLumaTexture(lease.GrContext, blurredShader, deviceRect, pad)
            : null;

        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["uLumaMode"] = lumaTexture is null ? 0f : 1f,
            ["uOrigin"] = Pair(ref _origin, deviceRect.Left, deviceRect.Top),
            ["uSize"] = Pair(ref _size, deviceRect.Width, deviceRect.Height),
            ["uCornerRadii"] = Quad(
                ref _radii,
                _params.RadiusTopLeft * scale,
                _params.RadiusTopRight * scale,
                _params.RadiusBottomRight * scale,
                _params.RadiusBottomLeft * scale),
            // Avoid division by zero in the rim mask.
            ["uThickness"] = MathF.Max(0.01f, _params.Thickness * scale),
            ["uRefraction"] = _params.Refraction * scale,
            ["uChroma"] = _params.Chroma,
            ["uDepth"] = _params.Depth,
            ["uMagnify"] = _params.Magnify,
            ["uTint"] = Quad(
                ref _tint, _params.TintR, _params.TintG, _params.TintB, _params.TintA),
            ["uLightDir"] = Pair(ref _lightDir, MathF.Cos(lightRad), -MathF.Sin(lightRad)),
            ["uLightIntensity"] = _params.LightIntensity,
            ["uFresnel"] = _params.Fresnel,
            ["uAdaptive"] = _params.Adaptive,
            ["uPad"] = (float)pad,
        };

        using var children = new SKRuntimeEffectChildren(effect)
        {
            ["uBlurred"] = blurredShader,
            ["uLuma"] = lumaTexture ?? blurredShader,
        };

        using var glassShader = effect.ToShader(uniforms, children);
        using var paint = new SKPaint { Shader = glassShader };

        canvas.Save();
        canvas.SetMatrix(SKMatrix.Identity);
        canvas.ClipRoundRect(
            CreateRoundRect(
                deviceRect,
                _params.RadiusTopLeft * scale,
                _params.RadiusTopRight * scale,
                _params.RadiusBottomRight * scale,
                _params.RadiusBottomLeft * scale),
            antialias: true);
        canvas.DrawRect(SKRect.Inflate(deviceRect, 1, 1), paint);
        canvas.Restore();
    }

    private static SKShader? CreateLumaTexture(
        GRContext? grContext, SKShader blurredShader, SKRect deviceRect, int pad)
    {
        var lumaEffect = LiquidGlassShader.LumaEffect;
        if (lumaEffect is null)
            return null;

        var info = new SKImageInfo(1, 1, SKColorType.RgbaF16, SKAlphaType.Premul);
        using var surface = grContext is not null
            ? SKSurface.Create(grContext, true, info) ?? SKSurface.Create(grContext, false, info)
            : SKSurface.Create(info);
        if (surface is null)
            return null;

        using var uniforms = new SKRuntimeEffectUniforms(lumaEffect)
        {
            ["uSize"] = Pair(ref _lumaSize, deviceRect.Width, deviceRect.Height),
            ["uPad"] = (float)pad,
        };
        using var children = new SKRuntimeEffectChildren(lumaEffect)
        {
            ["uBlurred"] = blurredShader,
        };
        using var shader = lumaEffect.ToShader(uniforms, children);
        using var paint = new SKPaint { Shader = shader };
        surface.Canvas.DrawRect(new SKRect(0, 0, 1, 1), paint);
        using var image = surface.Snapshot();
        return image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Nearest));
    }

    private static void DrawShadowLayer(
        SKCanvas canvas, SKRect deviceRect,
        float radiusTopLeft, float radiusTopRight, float radiusBottomRight, float radiusBottomLeft,
        float opacity, float blur, float offset, float scale)
    {
        var rect = deviceRect;
        rect.Offset(0, offset * scale);
        // Cached (owned by the cache; do not dispose): previously allocated per
        // shadow layer per glass per frame.
        var maskFilter = ShadowMaskFilter(blur * scale * 0.5f);
        using var paint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, (byte)Math.Clamp(opacity * 255f, 0, 255)),
            MaskFilter = maskFilter,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(
            CreateRoundRect(rect, radiusTopLeft, radiusTopRight, radiusBottomRight, radiusBottomLeft),
            paint);
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
            canvas.ClipRoundRect(
                CreateRoundRect(
                    deviceRect,
                    _params.RadiusTopLeft * scale,
                    _params.RadiusTopRight * scale,
                    _params.RadiusBottomRight * scale,
                    _params.RadiusBottomLeft * scale),
                antialias: true);
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
        canvas.DrawRoundRect(
            CreateRoundRect(
                rect,
                _params.RadiusTopLeft,
                _params.RadiusTopRight,
                _params.RadiusBottomRight,
                _params.RadiusBottomLeft),
            paint);
    }

    private static SKRoundRect CreateRoundRect(
        SKRect rect,
        float radiusTopLeft, float radiusTopRight, float radiusBottomRight, float radiusBottomLeft)
    {
        var roundRect = _roundRect ??= new SKRoundRect();
        var corners = _corners ??= new SKPoint[4];
        corners[0] = new SKPoint(radiusTopLeft, radiusTopLeft);
        corners[1] = new SKPoint(radiusTopRight, radiusTopRight);
        corners[2] = new SKPoint(radiusBottomRight, radiusBottomRight);
        corners[3] = new SKPoint(radiusBottomLeft, radiusBottomLeft);
        roundRect.SetRectRadii(rect, corners);
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
        var m = _saturation ??= new float[20];
        m[0] = ir + s; m[1] = ig;      m[2] = ib;      m[3] = 0; m[4] = 0;
        m[5] = ir;     m[6] = ig + s;  m[7] = ib;      m[8] = 0; m[9] = 0;
        m[10] = ir;    m[11] = ig;     m[12] = ib + s; m[13] = 0; m[14] = 0;
        m[15] = 0;     m[16] = 0;      m[17] = 0;      m[18] = 1; m[19] = 0;
        return m;
    }
}
