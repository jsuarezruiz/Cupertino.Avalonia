using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Cupertino.Controls;
using SkiaSharp;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class RtlTextRenderTests
{
    [AvaloniaFact]
    public void Wheel_digits_are_upright_in_both_directions()
    {
        var wheel = new CupertinoWheel
        {
            Items = new[] { "12", "35", "47" },
            SelectedIndex = 1,
            Width = 120,
            Height = 216,
        };
        var window = new Window { Width = 120, Height = 216, Background = Brushes.White, Content = wheel };
        try
        {
            window.Show();
            using var ltr = Capture(window);
            window.FlowDirection = FlowDirection.RightToLeft;
            using var rtl = Capture(window);
            AssertSameRegion(ltr, rtl, 0, 0, 0, 0, 120, 216);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Calendar_mirrors_day_columns_without_reflecting_the_digits()
    {
        var oldMotion = CupertinoAccessibility.ReduceMotion;
        CupertinoAccessibility.ReduceMotion = true;
        var grid = new CupertinoMonthGrid
        {
            Width = 280,
            DisplayMonth = new DateTime(2026, 7, 1),
            FirstDayOfWeek = DayOfWeek.Sunday,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var window = new Window { Width = 280, Height = 240, Background = Brushes.White, Content = grid };
        try
        {
            window.Show();
            using var ltr = Capture(window);
            window.FlowDirection = FlowDirection.RightToLeft;
            using var rtl = Capture(window);
            // July 12 is in Sunday column 0 (LTR) and 6 (RTL), on row 2.
            // Compare the complete cell with the glyph in its original orientation.
            AssertSameRegion(ltr, rtl, 0, 93, 240, 93, 40, 39);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = oldMotion;
        }
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

    private static void AssertSameRegion(SKBitmap left, SKBitmap right,
        int lx, int ly, int rx, int ry, int width, int height)
    {
        var different = 0;
        var ink = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var l = left.GetPixel(lx + x, ly + y);
                var r = right.GetPixel(rx + x, ry + y);
                if (l.Red < 200) ink++;
                if (Math.Abs(l.Red - r.Red) > 3 || Math.Abs(l.Green - r.Green) > 3 ||
                    Math.Abs(l.Blue - r.Blue) > 3) different++;
            }
        }
        Assert.True(ink > 20, "The reference must contain visible text.");
        if (different > 0)
        {
            var directory = Probe.FailureDirectory;
            Directory.CreateDirectory(directory);
            using var leftData = left.Encode(SKEncodedImageFormat.Png, 100);
            using var rightData = right.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(directory, $"rtl-text-{width}-ltr.png"), leftData.ToArray());
            File.WriteAllBytes(Path.Combine(directory, $"rtl-text-{width}-rtl.png"), rightData.ToArray());
        }
        Assert.True(different == 0, $"{different} pixels differ; RTL must not reflect the glyphs.");
    }
}
