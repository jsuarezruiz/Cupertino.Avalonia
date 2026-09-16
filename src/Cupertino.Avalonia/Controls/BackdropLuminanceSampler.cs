using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// Samples the content beneath a bottom tab bar and classifies the material backdrop.
/// </summary>
internal static class BackdropLuminanceSampler
{
    private const int SampleSize = 32;

    internal static bool TryIsDark(TemplatedControl owner, ItemsPresenter presenter, out bool isDark)
    {
        isDark = false;
        if (presenter.Bounds.Height <= 0)
            return false;

        var page = owner.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .FirstOrDefault(control => control.Name == "PART_SelectedContentHost");
        if (page is null || page.Bounds.Width < 1 || page.Bounds.Height < 1)
            return false;

        var presenterOrigin = presenter.TranslatePoint(default, page);
        if (presenterOrigin is null)
            return false;

        var pageBounds = new Rect(page.Bounds.Size);
        var sampleBounds = new Rect(presenterOrigin.Value, presenter.Bounds.Size).Intersect(pageBounds);
        if (sampleBounds.Width < 1 || sampleBounds.Height < 1)
            return false;

        try
        {
            using var renderTarget = new RenderTargetBitmap(
                new PixelSize(SampleSize, SampleSize), new Vector(96, 96));
            var brush = new VisualBrush(page)
            {
                SourceRect = new RelativeRect(sampleBounds, RelativeUnit.Absolute),
                DestinationRect = new RelativeRect(
                    new Rect(0, 0, SampleSize, SampleSize), RelativeUnit.Absolute),
                Stretch = Stretch.Fill,
            };
            using (var drawingContext = renderTarget.CreateDrawingContext())
                drawingContext.DrawRectangle(brush, null, new Rect(0, 0, SampleSize, SampleSize));

            // CopyPixels uses the target's channel order, which varies by backend.
            var format = renderTarget.Format ?? PixelFormat.Rgba8888;
            if (format != PixelFormat.Rgba8888 && format != PixelFormat.Bgra8888)
                return false;

            const int rowBytes = SampleSize * 4;
            var bytes = new byte[rowBytes * SampleSize];
            var pinned = System.Runtime.InteropServices.GCHandle.Alloc(
                bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                renderTarget.CopyPixels(
                    new PixelRect(0, 0, SampleSize, SampleSize), pinned.AddrOfPinnedObject(),
                    bytes.Length, rowBytes);
            }
            finally
            {
                pinned.Free();
            }

            var backgroundLuma = BackgroundLuma(owner);
            var materialLuma = AverageLuma(bytes, rowBytes, format, backgroundLuma);
            if (owner.TryFindResource("CupertinoTabBarTint", owner.ActualThemeVariant, out var resource)
                && resource is Color tint)
            {
                var alpha = tint.A / 255.0;
                var tintLuma = (0.2126 * tint.R + 0.7152 * tint.G + 0.0722 * tint.B) / 255.0;
                materialLuma = materialLuma * (1 - alpha) + tintLuma * alpha;
            }

            isDark = materialLuma < 0.34;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or InvalidOperationException
                                          or NotSupportedException)
        {
            return false;
        }
    }

    // Rec. 709 luma over the sample, composited onto the page behind any transparency.
    internal static double AverageLuma(
        ReadOnlySpan<byte> bytes, int rowBytes, PixelFormat format, double backgroundLuma)
    {
        var redOffset = format == PixelFormat.Rgba8888 ? 0 : 2;
        var blueOffset = 2 - redOffset;
        double sum = 0;
        for (var y = 0; y < SampleSize; y++)
            for (var x = 0; x < SampleSize; x++)
            {
                var offset = y * rowBytes + x * 4;
                var alpha = bytes[offset + 3] / 255.0;
                sum += (0.2126 * bytes[offset + redOffset]
                        + 0.7152 * bytes[offset + 1]
                        + 0.0722 * bytes[offset + blueOffset]) / 255.0
                       + (1 - alpha) * backgroundLuma;
            }

        return sum / (SampleSize * SampleSize);
    }

    private static double BackgroundLuma(Visual visual)
    {
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            var background = (current as Border)?.Background
                             ?? (current as Panel)?.Background
                             ?? (current as TemplatedControl)?.Background;
            if (background is not ISolidColorBrush { Color.A: > 24 } solid)
                continue;

            var color = solid.Color;
            return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        }

        return 1.0;
    }
}
