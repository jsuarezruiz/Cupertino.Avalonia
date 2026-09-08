using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

/// <summary>
/// Renders a control on a known background and reports luma.
/// </summary>
public static class Probe
{
    private const int ChannelTolerance = 3;
    private const double AllowedMismatchRatio = 0.002;
    private static readonly string ReferenceRootDirectory = FindReferenceDirectory();
    // Skia rasterizes text differently on each OS, even with the same bundled font.
    private static readonly string ReferenceDirectory = Path.Combine(ReferenceRootDirectory,
        OperatingSystem.IsMacOS() ? "macos" :
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsLinux() ? "linux" :
        throw new PlatformNotSupportedException("No render references are configured for this OS."));
    internal static readonly string FailureDirectory = Path.Combine(
        Path.GetDirectoryName(ReferenceRootDirectory)!, "TestResults", "RenderDiffs");

    public static byte[,] Render(string referenceName, Control content, int width, int height, Color background)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(background),
            Content = content,
        };
        try
        {
            window.Show();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            using var frame = window.CaptureRenderedFrame()!;
            AssertMatchesReference(referenceName, frame);
            return ToLuma(frame);
        }
        finally { window.Close(); }
    }

    public static void Snapshot(string referenceName, Window window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        AssertMatchesReference(referenceName, frame);
    }

    private static void AssertMatchesReference(string referenceName, WriteableBitmap frame)
    {
        Assert.DoesNotContain(referenceName, Path.GetInvalidFileNameChars());
        var referencePath = Path.Combine(ReferenceDirectory, referenceName + ".png");
        if (Environment.GetEnvironmentVariable("UPDATE_RENDER_REFERENCES") == "1")
        {
            Directory.CreateDirectory(ReferenceDirectory);
            frame.Save(referencePath, PngBitmapEncoderOptions.Default);
            return;
        }

        if (!File.Exists(referencePath))
        {
            SaveActual(referenceName, frame);
            Assert.Fail(
                $"Missing render reference '{referencePath}'. See '{FailureDirectory}' for the actual image. " +
                "Run with UPDATE_RENDER_REFERENCES=1 to create it after review.");
        }

        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        using var actual = SKBitmap.Decode(stream);
        using var expected = SKBitmap.Decode(referencePath);
        Assert.NotNull(actual);
        Assert.NotNull(expected);

        if (actual.Width != expected.Width || actual.Height != expected.Height)
        {
            SaveActual(referenceName, frame);
            Assert.Fail(
                $"{referenceName}: expected {expected.Width}x{expected.Height}, " +
                $"got {actual.Width}x{actual.Height}. See '{FailureDirectory}'.");
        }

        var different = 0;
        var total = actual.Width * actual.Height;
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                var delta = Math.Max(
                    Math.Max(Math.Abs(a.Red - e.Red), Math.Abs(a.Green - e.Green)),
                    Math.Max(Math.Abs(a.Blue - e.Blue), Math.Abs(a.Alpha - e.Alpha)));
                if (delta > ChannelTolerance)
                    different++;
            }
        }

        var ratio = (double)different / total;
        if (ratio > AllowedMismatchRatio)
        {
            SaveActual(referenceName, frame);
            SaveDiff(referenceName, expected, actual);
            Assert.Fail(
                $"{referenceName}: {different} of {total} pixels ({ratio:P3}) differ; " +
                $"allowed {AllowedMismatchRatio:P3} at channel tolerance {ChannelTolerance}. " +
                $"See '{FailureDirectory}'.");
        }

        DeleteFailureArtifacts(referenceName);
    }

    private static void SaveActual(string referenceName, WriteableBitmap frame)
    {
        Directory.CreateDirectory(FailureDirectory);
        frame.Save(
            Path.Combine(FailureDirectory, referenceName + ".actual.png"),
            PngBitmapEncoderOptions.Default);
    }

    private static void SaveDiff(string referenceName, SKBitmap expected, SKBitmap actual)
    {
        Directory.CreateDirectory(FailureDirectory);
        using var diff = new SKBitmap(expected.Width, expected.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                var delta = Math.Max(
                    Math.Max(Math.Abs(a.Red - e.Red), Math.Abs(a.Green - e.Green)),
                    Math.Max(Math.Abs(a.Blue - e.Blue), Math.Abs(a.Alpha - e.Alpha)));
                if (delta > ChannelTolerance)
                {
                    diff.SetPixel(x, y, new SKColor(255, 0, 255));
                }
                else
                {
                    var luma = (byte)((e.Red * 299 + e.Green * 587 + e.Blue * 114) / 1000);
                    var muted = (byte)(luma / 3);
                    diff.SetPixel(x, y, new SKColor(muted, muted, muted));
                }
            }
        }

        using var output = File.Create(Path.Combine(FailureDirectory, referenceName + ".diff.png"));
        diff.Encode(output, SKEncodedImageFormat.Png, 100);
    }

    private static void DeleteFailureArtifacts(string referenceName)
    {
        foreach (var suffix in new[] { ".actual.png", ".diff.png" })
        {
            var path = Path.Combine(FailureDirectory, referenceName + suffix);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string FindReferenceDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Cupertino.Avalonia.RenderTests.csproj")))
                return Path.Combine(directory.FullName, "ReferenceImages");
        }

        return Path.Combine(AppContext.BaseDirectory, "ReferenceImages");
    }

    private static byte[,] ToLuma(WriteableBitmap frame)
    {
        var w = frame.PixelSize.Width;
        var h = frame.PixelSize.Height;
        var luma = new byte[w, h];
        using var buffer = frame.Lock();
        unsafe
        {
            var p = (byte*)buffer.Address;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var o = y * buffer.RowBytes + x * 4;
                    luma[x, y] = (byte)((p[o + 2] * 299 + p[o + 1] * 587 + p[o] * 114) / 1000);
                }
        }
        return luma;
    }
}
