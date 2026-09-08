using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cupertino.Controls;
using SkiaSharp;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class SearchFieldShadowTests
{
    [AvaloniaTheory]
    [InlineData(false, 1.0)]
    [InlineData(true, 1.0)]
    [InlineData(true, 2.0)]
    public void Hidden_search_field_leaves_no_shadow_in_the_overlay(bool hideAncestor, double scaling)
    {
        var search = new TextBox
        {
            Classes = { "search" },
            Width = 280,
            Height = 47,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var host = new Border { Child = search };
        var window = new Window
        {
            Width = 400,
            Height = 240,
            Background = Brushes.LightGray,
            Content = host,
        };
        try
        {
            window.Show();
            window.SetRenderScaling(scaling);
            using var visible = Capture(window);
            var shadow = Assert.IsType<CupertinoSearchFieldShadow>(AdornerLayer.GetAdorner(search));
            Assert.True(shadow.IsEffectivelyVisible);

            (hideAncestor ? (Control)host : search).IsVisible = false;
            using var hidden = Capture(window);

            // Removing the decoration gives an independent reference for a
            // hidden field. Its old position must not affect any other content.
            CupertinoSearchFieldShadow.SetIsAdornerEnabled(search, false);
            using var withoutShadow = Capture(window);
            AssertPixelsEqual(withoutShadow, hidden);
            Assert.False(shadow.IsVisible);

            // A decoration created while its field is hidden must stay hidden,
            // then return with the field when the layout becomes visible again.
            CupertinoSearchFieldShadow.SetIsAdornerEnabled(search, true);
            var replacement = Assert.IsType<CupertinoSearchFieldShadow>(AdornerLayer.GetAdorner(search));
            Assert.False(replacement.IsVisible);
            (hideAncestor ? (Control)host : search).IsVisible = true;
            using var restored = Capture(window);
            Assert.True(replacement.IsEffectivelyVisible);
            AssertPixelsEqual(visible, restored);
        }
        finally { window.Close(); }
    }

    private static SKBitmap Capture(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var stream = new MemoryStream();
        frame.Save(stream, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return SKBitmap.Decode(stream);
    }

    private static void AssertPixelsEqual(SKBitmap expected, SKBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        var different = 0;
        for (var y = 0; y < expected.Height; y++)
        for (var x = 0; x < expected.Width; x++)
        {
            var a = expected.GetPixel(x, y);
            var b = actual.GetPixel(x, y);
            if (Math.Abs(a.Red - b.Red) > 2 || Math.Abs(a.Green - b.Green) > 2 ||
                Math.Abs(a.Blue - b.Blue) > 2 || Math.Abs(a.Alpha - b.Alpha) > 2)
                different++;
        }
        Assert.True(different == 0, $"{different} pixels differ from the expected search shadow visibility.");
    }
}
