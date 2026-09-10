using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using CoreText;
using Foundation;
using UIKit;

namespace Cupertino.Gallery;

// Opt in on a simulator or device with GALLERY_VERIFY_FONTS=1. These checks use
// real UIKit metrics and the application's installed shaper, not reference fonts.
internal static class TypographyVerification
{
    public static void Run()
    {
        var metrics = 0;
        var maxWidthDelta = 0d;
        var failures = new List<string>();
        foreach (var weight in new[] { FontWeight.Normal, FontWeight.Medium, FontWeight.SemiBold, FontWeight.Bold })
        foreach (var size in new[] { 10d, 13, 17, 20, 34 })
        foreach (var word in new[] { "Button", "One", "Two", "Home", "Settings", "Library", "Search", "Sep 9, 2026", "AVATAR", "café Ångström", "9:41\u202FAM" })
        {
            var glyph = GetFont(".AppleSystemUIFont", weight);
            using var native = UIFont.SystemFontOfSize((nfloat)size, AppleSystemFont.ToNativeWeight(weight));
            using var attributed = new NSAttributedString(word, new UIStringAttributes { Font = native });
            using var line = new CTLine(attributed);
            using var shaped = TextShaper.Current.ShapeText(word.AsMemory(), new TextShaperOptions(glyph, size));
            var nativeGlyphs = line.GetGlyphRuns().SelectMany(run => run.GetGlyphs()).ToArray();
            var delta = Math.Abs(shaped.Sum(g => g.GlyphAdvance) - line.GetTypographicBounds());
            maxWidthDelta = Math.Max(maxWidthDelta, delta);
            // CoreText supplies these positions directly; do not permit a
            // visible difference just because the font family and size match.
            if (delta > 0.000001 || !nativeGlyphs.SequenceEqual(shaped.Select(g => g.GlyphIndex)))
                failures.Add($"{word}/{size}/{weight}: width delta {delta}, glyph match {nativeGlyphs.SequenceEqual(shaped.Select(g => g.GlyphIndex))}");
            metrics++;
            if (size == 17 && weight == FontWeight.Normal)
                Console.WriteLine($"[font-metric] {word}: UIKit={line.GetTypographicBounds():F6} Avalonia={shaped.Sum(g => g.GlyphAdvance):F6}");
        }

        foreach (var weight in new[] { FontWeight.Normal, FontWeight.Medium, FontWeight.SemiBold, FontWeight.Bold })
        foreach (var size in new[] { 10d, 13, 17, 20, 34 })
        {
            const string value = "9:41\u202FAM";
            var glyph = GetFont(".AppleSystemUIFont", weight);
            using var native = UIFont.MonospacedDigitSystemFontOfSize((nfloat)size, AppleSystemFont.ToNativeWeight(weight));
            using var attributed = new NSAttributedString(value, new UIStringAttributes { Font = native });
            using var line = new CTLine(attributed);
            using var shaped = TextShaper.Current.ShapeText(value.AsMemory(), new TextShaperOptions(glyph, size,
                fontFeatures: new[] { new FontFeature { Tag = "tnum" } }));
            var delta = Math.Abs(shaped.Sum(g => g.GlyphAdvance) - line.GetTypographicBounds());
            maxWidthDelta = Math.Max(maxWidthDelta, delta);
            if (delta > 0.000001) failures.Add($"Tabular time/{size}/{weight}: width delta {delta}");
            if (size == 17 && weight == FontWeight.Normal)
                Console.WriteLine($"[font-metric] Tabular time: UIKit={line.GetTypographicBounds():F6} Avalonia={shaped.Sum(g => g.GlyphAdvance):F6}");
            metrics++;
        }

        var regular = GetFont(".AppleSystemUIFont", FontWeight.Normal);
        var options = new TextShaperOptions(regular, 17);
        using (var sliced = TextShaper.Current.ShapeText("before Button after".AsMemory(7, 6), options))
        using (var whole = TextShaper.Current.ShapeText("Button".AsMemory(), options))
        {
            Check(sliced.Select(g => g.GlyphCluster).SequenceEqual(whole.Select(g => g.GlyphCluster)), "slice clusters", failures);
            Check(sliced.Select(g => g.GlyphAdvance).SequenceEqual(whole.Select(g => g.GlyphAdvance)), "slice advances", failures);
        }
        using (var breaks = TextShaper.Current.ShapeText("a\r\n".AsMemory(), options))
            Check(breaks[^1].GlyphCluster == breaks[^2].GlyphCluster, "CRLF cluster", failures);
        using (var tabs = TextShaper.Current.ShapeText("a\tb".AsMemory(), new TextShaperOptions(regular, 17, incrementalTabWidth: 40)))
            Check(tabs.Single(g => g.GlyphCluster == 1).GlyphAdvance == 40, "tab advance", failures);
        using (var spaced = TextShaper.Current.ShapeText("Button".AsMemory(), new TextShaperOptions(regular, 17, letterSpacing: 1)))
        using (var normal = TextShaper.Current.ShapeText("Button".AsMemory(), options))
            Check(Math.Abs(spaced.Sum(g => g.GlyphAdvance) - normal.Sum(g => g.GlyphAdvance) - 6) < 0.001, "explicit letter spacing", failures);

        var custom = GetFont("Helvetica Neue", FontWeight.Normal);
        using (var original = new Avalonia.Harfbuzz.HarfBuzzTextShaper().ShapeText("AV fi café".AsMemory(), new TextShaperOptions(custom, 17)))
        using (var wrapped = TextShaper.Current.ShapeText("AV fi café".AsMemory(), new TextShaperOptions(custom, 17)))
            Check(original.SequenceEqual(wrapped), "custom font fallback", failures);

        foreach (var value in new[] { "Hello مرحبا", "שלום hello", "café a\u0301", "日本語 👩‍💻" })
        {
            using var layout = new TextLayout(value, new Typeface(".AppleSystemUIFont"), 17, Brushes.Black);
            Check(layout.Width > 0 && double.IsFinite(layout.Width), "multilingual layout", failures);
        }

        Console.WriteLine($"[font-check] {metrics} metric cases; maximum width delta={maxWidthDelta:F6} pt; structural checks; failures={failures.Count}");
        foreach (var failure in failures) Console.WriteLine($"[font-check] FAIL {failure}");
        if (failures.Count != 0) throw new InvalidOperationException("Typography verification failed; see [font-check] output.");
        Console.WriteLine("[font-check] PASS");
    }

    private static GlyphTypeface GetFont(string family, FontWeight weight)
    {
        if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family, weight: weight), out var glyph))
            throw new InvalidOperationException($"Cannot resolve {family} {weight}.");
        return glyph;
    }

    private static void Check(bool passed, string name, List<string> failures)
    {
        if (!passed) failures.Add(name);
    }
}
