using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.VisualTree;
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

    [AvaloniaFact]
    public void Non_uniformly_scaled_glass_keeps_the_full_shader_material()
    {
        var backdrop = new PatternBackdrop();
        var glass = new GlassSurface
        {
            Width = 180,
            Height = 90,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new ScaleTransform(0.35, 0.65),
            ShadowOpacity = 0,
        };
        var window = new Window
        {
            Width = 400,
            Height = 240,
            Content = new Grid { Children = { backdrop, glass } },
        };
        try
        {
            window.Show();
            using var frame = Capture(window);
            var cx = frame.Width / 2;
            var cy = frame.Height / 2;

            // A fallback tint preserves the checker edge under the translucent
            // fill. The full material blurs that edge even with unequal scales.
            Assert.InRange(ChannelDelta(
                frame.GetPixel(cx - 1, cy), frame.GetPixel(cx, cy)), 0, 20);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Frozen_glass_keeps_its_clean_backdrop_across_partial_updates()
    {
        var backdrop = new Border { Background = Brushes.Blue };
        var glass = new GlassSurface
        {
            Width = 180,
            Height = 90,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsBackdropFrozen = true,
            ShadowOpacity = 0,
        };
        var window = new Window
        {
            Width = 400,
            Height = 240,
            Content = new Grid { Children = { backdrop, glass } },
        };
        try
        {
            window.Show();
            using var initial = Capture(window);
            var center = initial.GetPixel(initial.Width / 2, initial.Height / 2);

            backdrop.Background = Brushes.Red;
            using var updated = Capture(window);

            Assert.Equal(center, updated.GetPixel(updated.Width / 2, updated.Height / 2));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Wheel_steps_inside_glass_redraw_from_one_backdrop_sample()
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
        var wheel = new CupertinoWheel
        {
            Items = Enumerable.Range(1, 30).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            SelectedIndex = 10,
            Width = 80,
            Height = 150,
        };
        var glass = new GlassSurface
        {
            Width = 200,
            Height = 200,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = wheel,
        };
        var window = new Window { Width = 400, Height = 300, Content = new Grid { Children = { backdrop, patch, glass } } };
        try
        {
            window.Show();
            Capture(window).Dispose();
            var start = wheel.TranslatePoint(new Point(wheel.Bounds.Width / 2, wheel.Bounds.Height / 2), window)!.Value;
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            await Task.Delay(450);
            var before = GlassSurface.GetBackdropInvalidationCount(window);

            SKBitmap? stepped = null;
            for (var i = 1; i <= 6; i++)
            {
                window.MouseMove(start + new Point(0, -8 * i));
                stepped?.Dispose();
                stepped = Capture(window);
            }
            using (stepped)
            {
                // Only the first step repaints the backdrop; the rest reuse its sample.
                Assert.Equal(before + 1, GlassSurface.GetBackdropInvalidationCount(window));
                backdrop.InvalidateVisual();
                using var fresh = Capture(window);
                AssertMatches(fresh, stepped!);
            }

            // A change behind the glass still reaches it between steps.
            using var beforePatch = Capture(window);
            patch.Background = Brushes.Lime;
            window.MouseMove(start + new Point(0, -56));
            using var afterPatch = Capture(window);
            var center = new SKPointI(afterPatch.Width / 2, afterPatch.Height / 2 + 60);
            Assert.NotEqual(beforePatch.GetPixel(center.X, center.Y), afterPatch.GetPixel(center.X, center.Y));
        }
        finally
        {
            window.MouseUp(new Point(1, 1), MouseButton.Left);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Glass_over_an_animating_wheel_keeps_the_full_repaint()
    {
        var (window, backdrop, wheel, _) = WheelInGlass(overlay: true);
        try
        {
            var start = await PressWheel(window, wheel);
            var before = GlassSurface.GetBackdropInvalidationCount(window);
            SKBitmap? stepped = null;
            for (var i = 1; i <= 4; i++)
            {
                window.MouseMove(start + new Point(0, -8 * i));
                stepped?.Dispose();
                stepped = Capture(window);
            }
            using (stepped)
            {
                Assert.Equal(before + 4, GlassSurface.GetBackdropInvalidationCount(window));
                backdrop.InvalidateVisual();
                using var fresh = Capture(window);
                AssertMatches(fresh, stepped!);
            }
        }
        finally { Release(window); }
    }

    [AvaloniaFact]
    public async Task Glass_drops_its_backdrop_sample_once_content_stops()
    {
        var (window, _, wheel, scene) = WheelInGlass(overlay: false);
        try
        {
            var start = await PressWheel(window, wheel);
            for (var i = 1; i <= 3; i++)
            {
                window.MouseMove(start + new Point(0, -8 * i));
                Capture(window).Dispose();
            }
            // The release timer runs a second after the last step; slow machines fire it late.
            var glass = (GlassSurface)scene.Children[1];
            var deadline = DateTime.UtcNow.AddSeconds(10);
            do
            {
                await Task.Delay(100);
                Capture(window).Dispose();
            }
            while (glass.RedrawState is global::Cupertino.Rendering.LiquidGlassDrawOperation.ForegroundState { HasCleanSample: true }
                   && DateTime.UtcNow < deadline);

            var before = GlassSurface.GetBackdropInvalidationCount(window);
            window.MouseMove(start + new Point(0, -32));
            Capture(window).Dispose();
            Assert.Equal(before + 1, GlassSurface.GetBackdropInvalidationCount(window));
        }
        finally { Release(window); }
    }

    [AvaloniaFact]
    public async Task Offscreen_render_leaves_the_glass_redraw_state_alone()
    {
        var (window, _, wheel, scene) = WheelInGlass(overlay: false);
        try
        {
            var start = await PressWheel(window, wheel);
            window.MouseMove(start + new Point(0, -8));
            Capture(window).Dispose();
            var glass = (GlassSurface)scene.Children[1];
            var state = glass.RedrawState;
            Assert.NotNull(state);

            using (var offscreen = new global::Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 300)))
                offscreen.Render(scene);

            Assert.Same(state, glass.RedrawState);
        }
        finally { Release(window); }
    }

    [AvaloniaFact]
    public async Task Hovering_a_tab_in_a_glass_bar_matches_a_full_repaint()
    {
        var backdrop = new PatternBackdrop();
        var tabs = new TabControl { Classes = { "bottom" }, SelectedIndex = 0 };
        foreach (var header in new[] { "Home", "New", "Settings" })
            tabs.Items.Add(new TabItem { Header = header, Content = new Border() });
        var window = new Window { Width = 420, Height = 300, Content = new Grid { Children = { backdrop, tabs } } };
        try
        {
            window.Show();
            await Task.Delay(450);
            Capture(window).Dispose();
            var item = tabs.GetVisualDescendants().OfType<TabItem>().ElementAt(1);
            window.MouseMove(item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value);
            SKBitmap? hovered = null;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(20);
                hovered?.Dispose();
                hovered = Capture(window);
            }
            using (hovered)
            {
                backdrop.InvalidateVisual();
                using var fresh = Capture(window);
                AssertMatches(fresh, hovered!);
            }
        }
        finally { window.Close(); }
    }

    private static (Window Window, PatternBackdrop Backdrop, CupertinoWheel Wheel, Grid Scene) WheelInGlass(bool overlay)
    {
        var backdrop = new PatternBackdrop();
        var wheel = new CupertinoWheel
        {
            Items = Enumerable.Range(1, 30).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            SelectedIndex = 10,
            Width = 80,
            Height = 150,
        };
        var content = new Grid { Children = { wheel } };
        if (overlay)
            content.Children.Add(new GlassSurface { Width = 120, Height = 36, CornerRadius = new CornerRadius(18) });
        var glass = new GlassSurface
        {
            Width = 200,
            Height = 200,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = content,
        };
        var scene = new Grid { Children = { backdrop, glass } };
        var window = new Window { Width = 400, Height = 300, Content = scene };
        window.Show();
        return (window, backdrop, wheel, scene);
    }

    private static async Task<Point> PressWheel(Window window, CupertinoWheel wheel)
    {
        Capture(window).Dispose();
        var start = wheel.TranslatePoint(new Point(wheel.Bounds.Width / 2, wheel.Bounds.Height / 2 + 40), window)!.Value;
        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left);
        await Task.Delay(450);
        return start;
    }

    private static void Release(Window window)
    {
        window.MouseUp(new Point(1, 1), MouseButton.Left);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(40, true)]
    [InlineData(double.NaN, false)]
    public async Task Spinner_inside_glass_matches_a_full_repaint(double size, bool reuses)
    {
        var backdrop = new PatternBackdrop();
        var glass = new GlassSurface
        {
            Width = 120,
            Height = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new CupertinoActivityIndicator { IsActive = true, Width = size, Height = size },
        };
        var window = new Window { Width = 400, Height = 240, Content = new Grid { Children = { backdrop, glass } } };
        try
        {
            window.Show();
            await Task.Delay(450);
            var spun = Capture(window);
            var before = GlassSurface.GetBackdropInvalidationCount(window);
            // Wait for spinner steps rather than a fixed time; slow machines tick late.
            var steps = 0;
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (steps < 4 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(40);
                var next = Capture(window);
                if (CenterChanged(spun, next))
                    steps++;
                spun.Dispose();
                spun = next;
            }
            using (spun)
            {
                Assert.True(steps >= 4, "The spinner did not advance.");
                // A spinner reaching the rounded edge repaints the backdrop on every step instead.
                var repaints = GlassSurface.GetBackdropInvalidationCount(window) - before;
                if (reuses)
                    Assert.InRange(repaints, 0, 1);
                else
                    Assert.True(repaints >= steps, $"{repaints} backdrop repaints for {steps} steps.");
                backdrop.InvalidateVisual();
                using var fresh = Capture(window);
                AssertMatches(fresh, spun);
            }
        }
        finally { window.Close(); }
    }

    private static bool CenterChanged(SKBitmap a, SKBitmap b)
    {
        var cx = a.Width / 2;
        var cy = a.Height / 2;
        for (var y = cy - 15; y < cy + 15; y++)
            for (var x = cx - 15; x < cx + 15; x++)
                if (ChannelDelta(a.GetPixel(x, y), b.GetPixel(x, y)) > 3)
                    return true;
        return false;
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
