using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Cupertino.Controls;
using SkiaSharp;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class GlassRepaintTests
{
    [AvaloniaFact]
    public async Task Small_backdrop_update_matches_a_full_repaint_after_pulse()
    {
        var backdrop = new PatternBackdrop();
        var patch = new Border
        {
            Width = 24,
            Height = 24,
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var glass = new GlassSurface
        {
            Width = 180,
            Height = 90,
            ShadowOpacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var scene = new Grid { Children = { backdrop, patch, glass } };
        var window = new Window { Width = 400, Height = 240, Content = scene };
        try
        {
            window.Show();
            using var before = Capture(window);
            await Task.Delay(450);
            patch.Background = Brushes.Lime;
            glass.Pulse();
            using var updated = Capture(window);
            Assert.NotEqual(before.GetPixel(200, 120), updated.GetPixel(200, 120));
            backdrop.InvalidateVisual();
            using var fresh = Capture(window);
            AssertMatches(fresh, updated);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Live_glass_repeated_and_moved_frames_match_a_fresh_backdrop(double scaling)
    {
        var backdrop = new PatternBackdrop();
        var glass = new GlassSurface
        {
            Width = 180,
            Height = 90,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsLive = true,
            ShadowOpacity = 0,
        };
        var scene = new Grid { Children = { backdrop, glass } };
        var window = new Window { Width = 400, Height = 240, Content = scene };
        try
        {
            window.Show();
            window.SetRenderScaling(scaling);
            using var initial = Capture(window);
            var cx = initial.Width / 2;
            var cy = initial.Height / 2;
            // Blur must smooth the checker boundary; a tinted fallback would
            // preserve its sharp contrast and falsely pass a stability check.
            Assert.InRange(ChannelDelta(initial.GetPixel(cx - 1, cy), initial.GetPixel(cx, cy)), 0, 20);
            for (var i = 0; i < 5; i++)
            {
                using var next = Capture(window);
                AssertMatches(initial, next);
            }

            // Invalidating a custom operation covering the entire window rebuilds
            // the backdrop, providing a reference independent of retained pixels.
            backdrop.InvalidateVisual();
            using var fresh = Capture(window);
            AssertMatches(initial, fresh);

            glass.RenderTransform = new TranslateTransform(32, 15);
            using var moved = Capture(window);
            backdrop.InvalidateVisual();
            using var movedFresh = Capture(window);
            AssertMatches(movedFresh, moved);
        }
        finally { window.Close(); }
    }

    private static SKBitmap Capture(Window window)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var stream = new MemoryStream();
        frame.Save(stream, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    private static void AssertMatches(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        var different = 0;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var e = expected.GetPixel(x, y);
                var a = actual.GetPixel(x, y);
                if (ChannelDelta(e, a) > 3)
                    different++;
            }
        }
        Assert.True(different == 0, $"{different} pixels differ from the fresh backdrop reference.");
    }

    private static int ChannelDelta(SKColor a, SKColor b) =>
        Math.Max(Math.Max(Math.Abs(a.Red - b.Red), Math.Abs(a.Green - b.Green)),
            Math.Max(Math.Abs(a.Blue - b.Blue), Math.Abs(a.Alpha - b.Alpha)));

    private sealed class PatternBackdrop : Control
    {
        public override void Render(DrawingContext context) =>
            context.Custom(new PatternOperation(new Rect(Bounds.Size)));
    }

    private sealed class PatternOperation(Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            using var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()!.Lease();
            using var paint = new SKPaint();
            for (var y = 0; y < bounds.Height; y += 20)
            {
                for (var x = 0; x < bounds.Width; x += 20)
                {
                    paint.Color = (x / 20 + y / 20) % 2 == 0 ? SKColors.DarkBlue : SKColors.Orange;
                    lease.SkCanvas.DrawRect(x, y, 20, 20, paint);
                }
            }
        }
    }
}
