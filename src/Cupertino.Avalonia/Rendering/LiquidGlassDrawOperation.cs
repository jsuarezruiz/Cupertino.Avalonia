using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
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
    [ThreadStatic] private static Queue<int>? _blurOrder;
    [ThreadStatic] private static Dictionary<int, SKColorFilter>? _saturationCache;
    [ThreadStatic] private static Queue<int>? _saturationOrder;
    [ThreadStatic] private static Dictionary<int, SKMaskFilter>? _shadowMaskCache;
    [ThreadStatic] private static Queue<int>? _shadowMaskOrder;

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

    // Evicts a single oldest entry instead of clearing the cache, so a burst
    // of distinct inputs cannot make every subsequent lookup miss at once.
    // Returned values are pure functions of their keys, so eviction policy
    // never changes rendering output.
    private static void EvictOldest<T>(Dictionary<int, T> cache, ref Queue<int>? order)
        where T : IDisposable
    {
        order ??= new Queue<int>();
        while (order.Count > 0)
            if (cache.Remove(order.Dequeue(), out var evicted))
            {
                evicted.Dispose();
                return;
            }
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
            EvictOldest(cache, ref _blurOrder);
        filter = SKImageFilter.CreateBlur(key / 4f, key / 4f, SKShaderTileMode.Clamp);
        cache[key] = filter;
        (_blurOrder ??= new Queue<int>()).Enqueue(key);
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
            EvictOldest(cache, ref _saturationOrder);
        filter = SKColorFilter.CreateColorMatrix(CreateSaturationMatrix(key / 100f));
        cache[key] = filter;
        (_saturationOrder ??= new Queue<int>()).Enqueue(key);
        return filter;
    }

    private static SKMaskFilter ShadowMaskFilter(float radius)
    {
        var key = (int)MathF.Round(Math.Max(0.5f, radius) * 4f);
        var cache = _shadowMaskCache ??= new Dictionary<int, SKMaskFilter>(16);
        if (cache.TryGetValue(key, out var filter))
            return filter;
        if (cache.Count >= 24)
            EvictOldest(cache, ref _shadowMaskOrder);
        filter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, key / 4f);
        cache[key] = filter;
        (_shadowMaskOrder ??= new Queue<int>()).Enqueue(key);
        return filter;
    }

    private const int BackendUnknown = 0, BackendRaster = 1, BackendGpu = 2;
    private static int _browserBackend;

    // Software canvas in the browser: the flat material replaces glass.
    internal static bool IsBrowserRaster => Volatile.Read(ref _browserBackend) == BackendRaster;

    // Offscreen bitmaps are raster too, so a GPU frame always wins.
    internal static bool IsBrowserRasterFrame(ISkiaSharpApiLease lease)
    {
        if (!OperatingSystem.IsBrowser() || lease.SkSurface is null)
            return false;
        if (lease.GrContext is not null)
        {
            if (Interlocked.Exchange(ref _browserBackend, BackendGpu) == BackendRaster)
                Dispatcher.UIThread.Post(Controls.GlassSurface.InvalidateAllSurfaces);
            return false;
        }
        if (Interlocked.CompareExchange(ref _browserBackend, BackendRaster, BackendUnknown) == BackendUnknown)
            Dispatcher.UIThread.Post(Controls.GlassSurface.InvalidateAllSurfaces);
        return IsBrowserRaster;
    }

    private readonly GlassParams _params;

    private readonly Rect _surface;
    private readonly FrozenBackdrop? _frozenBackdrop;
    private readonly bool _retainSample;
    private readonly object _sampleGate = new();
    private Sample? _sample;
    private bool _rendered;
    private bool _disposed;
    private volatile bool _drawn;
    // GPU images are released on the render thread, where their context is current.
    private static readonly System.Collections.Concurrent.ConcurrentQueue<IDisposable> Retired = new();
    // Larger glass keeps sampling the retained frame rather than holding a copy.
    private const double MaxKeptBackdropPixels = 2_000_000;
    private SKSurface? _backdrop;
    private SKRectI _backdropRect;
    private IntPtr _backdropSurface;
    private IntPtr _backdropContext;

    public LiquidGlassDrawOperation(
        Rect surface, Rect bounds, GlassParams parameters, FrozenBackdrop? frozenBackdrop,
        bool retainSample = false)
    {
        _surface = surface;
        Bounds = bounds;
        _params = parameters;
        _frozenBackdrop = frozenBackdrop;
        _frozenBackdrop?.AddHolder();
        _retainSample = retainSample || frozenBackdrop is not null;
    }

    internal ForegroundState Foreground { get; } = new();

    // True once drawn; immediate renderers draw while recording.
    internal bool HasDrawn => _drawn;

    // Shared with the owning surface on the UI thread.
    internal sealed class ForegroundState
    {
        private readonly object _gate = new();
        private Rect? _pending;
        private volatile bool _hasCleanSample;

        // True while the first sample is kept and no other redraw has touched the backdrop since.
        public bool HasCleanSample
        {
            get => _hasCleanSample;
            internal set => _hasCleanSample = value;
        }

        // Content above the glass changed inside this local rect; its backdrop did not.
        public void Mark(Rect local)
        {
            lock (_gate)
                _pending = _pending is { } pending ? pending.Union(local) : local;
        }

        // Marks last for the operation: UI ticks can run ahead of the frame being rendered.
        internal Rect? Current
        {
            get
            {
                lock (_gate)
                    return _pending;
            }
        }
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => _surface.Contains(p);

    // Glass depends on its current backdrop.
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
        lock (_sampleGate)
        {
            _disposed = true;
            if (_sample is { } sample)
            {
                if (sample.OnGpu)
                    Retired.Enqueue(sample);
                else
                    sample.Dispose();
                _sample = null;
            }
            if (_backdrop is not null)
            {
                if (_backdropContext != IntPtr.Zero)
                    Retired.Enqueue(_backdrop);
                else
                    _backdrop.Dispose();
                _backdrop = null;
            }
            Foreground.HasCleanSample = false;
        }
        _frozenBackdrop?.ReleaseIfRetired();
    }

    public void Render(ImmediateDrawingContext context)
    {
        _drawn = true;
        var foreground = Foreground.Current;
        lock (_sampleGate)
        {
            if (!_disposed)
                Render(context, foreground);
        }
    }

    private void Render(ImmediateDrawingContext context, Rect? foreground)
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
        while (Retired.TryDequeue(out var retired))
            retired.Dispose();
        var canvas = lease.SkCanvas;
        var surface = lease.SkSurface;

        var opacity = (float)Math.Clamp(lease.CurrentOpacity, 0, 1);

        if (surface is null)
        {
            RenderFallback(canvas, null, default, default, opacity);
            return;
        }

        if (IsBrowserRasterFrame(lease))
        {
            RenderFallback(canvas, null, default, default, opacity);
            return;
        }

        var ctm = canvas.TotalMatrix;
        // The shader works in device space, so independent axis scales are safe:
        // deviceRect already contains the transformed size.  Use the vertical
        // scale for radii and optical distances so a source-bound flyout morph
        // never switches to the flat fallback while approaching scale(1).
        if (!float.IsFinite(ctm.ScaleX) || MathF.Abs(ctm.ScaleX) <= 0.001f ||
            !float.IsFinite(ctm.ScaleY) || ctm.ScaleY <= 0.001f ||
            ctm.SkewX != 0 || ctm.SkewY != 0 ||
            ctm.Persp0 != 0 || ctm.Persp1 != 0 || ctm.Persp2 != 1)
        {
            RenderFallback(canvas, null, default, default, opacity);
            return;
        }
        var localBounds = new SKRect(0, 0, (float)_surface.Width, (float)_surface.Height);
        var deviceRect = ctm.MapRect(localBounds);

        var scale = ctm.ScaleY;
        // Right-to-left layouts mirror the surface, swapping its left and right corners on screen.
        var radii = ctm.ScaleX < 0
            ? new Radii(_params.RadiusTopRight * scale, _params.RadiusTopLeft * scale,
                        _params.RadiusBottomLeft * scale, _params.RadiusBottomRight * scale)
            : new Radii(_params.RadiusTopLeft * scale, _params.RadiusTopRight * scale,
                        _params.RadiusBottomRight * scale, _params.RadiusBottomLeft * scale);

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
            RenderFallback(canvas, null, default, default, opacity);
            return;
        }
        var pad = (int)padding;
        // Frozen input never changes; otherwise only a redraw confined to marked foreground may reuse.
        var contextHandle = lease.GrContext?.Handle ?? IntPtr.Zero;
        if (_sample is { } kept && kept.Matches(ctm, surface.Handle, contextHandle)
            && (_frozenBackdrop is not null
                || kept.Clean && foreground is { } local && Covers(ctm.MapRect(ToSKRect(local)), canvas.DeviceClipBounds)
                   && InsideShape(canvas.DeviceClipBounds, deviceRect, radii)))
        {
            DrawShadow(canvas, kept.Shadow, deviceRect, pad, opacity);
            DrawGlass(canvas, kept.Blurred, kept.Luma, deviceRect, radii, scale, pad, opacity);
            return;
        }
        // Only a new operation's first draw repaints its whole backdrop.
        var first = !_rendered;
        _rendered = true;
        ReplaceSample(null);

        var info = new SKImageInfo((int)paddedWidth, (int)paddedHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        // Budgeted so Skia can recycle it.
        using var blurSurface = lease.GrContext is { } grContext
            ? SKSurface.Create(grContext, true, info) ?? SKSurface.Create(grContext, false, info)
            : SKSurface.Create(info);
        if (blurSurface is null)
        {
            RenderFallback(canvas, null, default, default, opacity);
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
            RenderFallback(canvas, null, default, default, opacity);
            return;
        }

        var snapshot = croppedBackdrop ?? _frozenBackdrop!.Capture(surface);
        // Skia clamps the crop to the surface.
        var snapshotLeft = croppedBackdrop is null ? 0 : Math.Max(0, cropLeft);
        var snapshotTop = croppedBackdrop is null ? 0 : Math.Max(0, cropTop);
        using var merged = croppedBackdrop is null
            ? null
            : MergeBackdrop(lease.GrContext, croppedBackdrop, snapshotLeft, snapshotTop,
                ctm.MapRect(ToSKRect(Bounds)), canvas.DeviceClipBounds, surface.Handle, contextHandle);
        if (merged is not null)
            snapshot = merged;

        var bc = blurSurface.Canvas;
        SKImage? shadow = null;
        SKImage? blurred = null;
        SKShader? lumaTexture = null;
        try
        {
            // Rasterize the shadow offscreen too (same macOS issue), before the blur pass
            // reuses the target.
            if (_params.ShadowOpacity > 0.002f)
            {
                var (rTL, rTR, rBR, rBL) = radii;
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

                shadow = blurSurface.Snapshot();
                DrawShadow(canvas, shadow, deviceRect, pad, opacity);
                // A kept snapshot makes the next write copy the target.
                if (!_retainSample)
                {
                    shadow.Dispose();
                    shadow = null;
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

            blurred = blurSurface.Snapshot();

            // Pass 2: render the glass effect in device space.
            if (LiquidGlassShader.Effect is null)
            {
                RenderFallback(canvas, blurred, deviceRect, radii, opacity, pad);
                return;
            }

            // Average luma once per frame, not per fragment.
            if (_params.Adaptive > 0.5f)
            {
                using var blurredShader = BlurredShader(blurred, pad);
                lumaTexture = CreateLumaTexture(lease.GrContext, blurredShader, deviceRect, pad);
            }

            DrawGlass(canvas, blurred, lumaTexture, deviceRect, radii, scale, pad, opacity);

            // A partial redraw without a kept backdrop samples retained glass pixels; do not reuse it.
            if (_retainSample && (first || merged is not null || _frozenBackdrop is not null))
            {
                ReplaceSample(new Sample(ctm, surface.Handle, contextHandle, first || merged is not null,
                    shadow, blurred, lumaTexture));
                shadow = null;
                blurred = null;
                lumaTexture = null;
            }
        }
        finally
        {
            shadow?.Dispose();
            blurred?.Dispose();
            lumaTexture?.Dispose();
        }
    }

    private void ReplaceSample(Sample? sample)
    {
        _sample?.Dispose();
        _sample = sample;
        Foreground.HasCleanSample = sample is { Clean: true };
    }

    // Outside a partial redraw's clip, the surface still holds last frame's output, glass included.
    // Under the glass that is replaced by the backdrop kept from earlier draws.
    private SKImage? MergeBackdrop(
        GRContext? grContext, SKImage live, int left, int top, SKRect opDevice, SKRectI clip,
        IntPtr surfaceHandle, IntPtr contextHandle)
    {
        var liveRect = SKRectI.Create(left, top, live.Width, live.Height);
        if ((double)live.Width * live.Height > MaxKeptBackdropPixels)
        {
            _backdrop?.Dispose();
            _backdrop = null;
            return null;
        }
        if (_backdrop is null || _backdropRect != liveRect
            || _backdropSurface != surfaceHandle || _backdropContext != contextHandle)
        {
            _backdrop?.Dispose();
            var info = new SKImageInfo(live.Width, live.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            _backdrop = grContext is not null
                ? SKSurface.Create(grContext, true, info) ?? SKSurface.Create(grContext, false, info)
                : SKSurface.Create(info);
            (_backdropRect, _backdropSurface, _backdropContext) = (liveRect, surfaceHandle, contextHandle);
            if (_backdrop is null)
                return null;
            using var copy = new SKPaint { BlendMode = SKBlendMode.Src };
            _backdrop.Canvas.DrawImage(live, 0, 0, copy);
            return null;
        }

        var inner = new SKRectI(
            (int)MathF.Ceiling(opDevice.Left) - left, (int)MathF.Ceiling(opDevice.Top) - top,
            (int)MathF.Floor(opDevice.Right) - left, (int)MathF.Floor(opDevice.Bottom) - top);
        var redrawn = new SKRectI(clip.Left - left, clip.Top - top, clip.Right - left, clip.Bottom - top);
        var canvas = _backdrop.Canvas;
        using var paint = new SKPaint { BlendMode = SKBlendMode.Src };
        canvas.Save();
        canvas.ClipRect(inner, SKClipOperation.Difference);
        canvas.DrawImage(live, 0, 0, paint);
        canvas.Restore();
        canvas.Save();
        canvas.ClipRect(redrawn);
        canvas.DrawImage(live, 0, 0, paint);
        canvas.Restore();
        return _backdrop.Snapshot();
    }

    private static SKShader BlurredShader(SKImage blurred, int pad) =>
        blurred.ToShader(
            SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Linear),
            SKMatrix.CreateTranslation(-pad, -pad));

    private static SKRect ToSKRect(Rect rect) =>
        new((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);

    // Dirty rects are inflated and snapped to device pixels.
    private static bool Covers(SKRect area, SKRectI clip) =>
        !clip.IsEmpty
        && clip.Left >= MathF.Floor(area.Left) - 2 && clip.Top >= MathF.Floor(area.Top) - 2
        && clip.Right <= MathF.Ceiling(area.Right) + 2 && clip.Bottom <= MathF.Ceiling(area.Bottom) + 2;

    // A redraw clip crossing the rounded edge changes its antialiasing, so only interiors reuse.
    private static bool InsideShape(SKRectI clip, SKRect deviceRect, Radii radii) =>
        CreateRoundRect(
            SKRect.Inflate(deviceRect, -1, -1),
            Math.Max(0, radii.TopLeft - 1), Math.Max(0, radii.TopRight - 1),
            Math.Max(0, radii.BottomRight - 1), Math.Max(0, radii.BottomLeft - 1))
        .Contains(new SKRect(clip.Left, clip.Top, clip.Right, clip.Bottom));

    // Local-space counterpart used to decide before the frame is rendered.
    internal static bool InsideShape(Rect area, Rect shape, CornerRadius radius)
    {
        var (tl, tr, br, bl) = (radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        var fit = Math.Min(1, Math.Min(
            Math.Min(shape.Width / Math.Max(tl + tr, 1e-9), shape.Width / Math.Max(bl + br, 1e-9)),
            Math.Min(shape.Height / Math.Max(tl + bl, 1e-9), shape.Height / Math.Max(tr + br, 1e-9))));
        (tl, tr, br, bl) = (tl * fit, tr * fit, br * fit, bl * fit);
        return shape.Contains(area.TopLeft) && shape.Contains(area.BottomRight)
            && Inside(area.TopLeft, shape.Left + tl, shape.Top + tl, tl, -1, -1)
            && Inside(area.TopRight, shape.Right - tr, shape.Top + tr, tr, 1, -1)
            && Inside(area.BottomRight, shape.Right - br, shape.Bottom - br, br, 1, 1)
            && Inside(area.BottomLeft, shape.Left + bl, shape.Bottom - bl, bl, -1, 1);

        static bool Inside(Point p, double cx, double cy, double r, int sx, int sy)
        {
            var dx = (p.X - cx) * sx;
            var dy = (p.Y - cy) * sy;
            return dx <= 0 || dy <= 0 || dx * dx + dy * dy <= r * r;
        }
    }

    private static void DrawShadow(SKCanvas canvas, SKImage? shadow, SKRect deviceRect, int pad, float opacity)
    {
        if (shadow is null)
            return;
        canvas.Save();
        canvas.SetMatrix(SKMatrix.Identity);
        using var shadowPaint = opacity < 1 ? new SKPaint { Color = SKColors.White.WithAlpha(ToByte(opacity)) } : null;
        canvas.DrawImage(shadow, deviceRect.Left - pad, deviceRect.Top - pad, shadowPaint);
        canvas.Restore();
    }

    private void DrawGlass(
        SKCanvas canvas, SKImage blurred, SKShader? lumaTexture,
        SKRect deviceRect, Radii radii, float scale, int pad, float opacity)
    {
        var effect = LiquidGlassShader.Effect!;
        var lightRad = _params.LightAngleDegrees * MathF.PI / 180f;
        using var blurredShader = BlurredShader(blurred, pad);

        using var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["uLumaMode"] = lumaTexture is null ? 0f : 1f,
            ["uOrigin"] = Pair(ref _origin, deviceRect.Left, deviceRect.Top),
            ["uSize"] = Pair(ref _size, deviceRect.Width, deviceRect.Height),
            ["uCornerRadii"] = Quad(
                ref _radii, radii.TopLeft, radii.TopRight, radii.BottomRight, radii.BottomLeft),
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
        using var paint = new SKPaint { Shader = glassShader, Color = SKColors.White.WithAlpha(ToByte(opacity)) };

        canvas.Save();
        canvas.SetMatrix(SKMatrix.Identity);
        canvas.ClipRoundRect(
            CreateRoundRect(
                deviceRect, radii.TopLeft, radii.TopRight, radii.BottomRight, radii.BottomLeft),
            antialias: true);
        canvas.DrawRect(SKRect.Inflate(deviceRect, 1, 1), paint);
        canvas.Restore();
    }

    private sealed class Sample(
        SKMatrix matrix, IntPtr surface, IntPtr context, bool clean,
        SKImage? shadow, SKImage blurred, SKShader? luma) : IDisposable
    {
        public bool Clean { get; } = clean;
        public SKImage? Shadow { get; } = shadow;
        public SKImage Blurred { get; } = blurred;
        public SKShader? Luma { get; } = luma;

        public bool OnGpu => context != IntPtr.Zero;

        public bool Matches(SKMatrix m, IntPtr s, IntPtr c) => m == matrix && s == surface && c == context;

        public void Dispose()
        {
            Shadow?.Dispose();
            Blurred.Dispose();
            Luma?.Dispose();
        }
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
        SKCanvas canvas, SKImage? blurred, SKRect deviceRect, Radii radii, float opacity, int pad = 0)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(
                (byte)(_params.TintR * 255), (byte)(_params.TintG * 255), (byte)(_params.TintB * 255),
                ToByte(Math.Clamp(_params.TintA + 60f / 255f, 0, 1) * opacity)),
            IsAntialias = true,
        };

        if (blurred is not null && deviceRect.Width > 0)
        {
            canvas.Save();
            canvas.SetMatrix(SKMatrix.Identity);
            canvas.ClipRoundRect(
                CreateRoundRect(
                    deviceRect, radii.TopLeft, radii.TopRight, radii.BottomRight, radii.BottomLeft),
                antialias: true);
            var source = new SKRect(
                pad, pad,
                Math.Max(pad, blurred.Width - pad),
                Math.Max(pad, blurred.Height - pad));
            using var imagePaint = opacity < 1 ? new SKPaint { Color = SKColors.White.WithAlpha(ToByte(opacity)) } : null;
            canvas.DrawImage(
                blurred, source, deviceRect,
                new SKSamplingOptions(SKFilterMode.Linear), imagePaint);
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

    private readonly record struct Radii(float TopLeft, float TopRight, float BottomRight, float BottomLeft);

    private static byte ToByte(float unit) => (byte)MathF.Round(Math.Clamp(unit, 0, 1) * 255);

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

// Keeps observing the backend while flat glass draws nothing through the effect.
internal sealed class BrowserBackendProbe(Rect bounds) : ICustomDrawOperation
{
    public Rect Bounds => bounds;

    public bool HitTest(Point p) => false;

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
    }

    public void Render(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
            return;
        using var lease = feature.Lease();
        LiquidGlassDrawOperation.IsBrowserRasterFrame(lease);
    }
}
