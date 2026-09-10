using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;

namespace Cupertino;

// Avalonia 12.1.1 does not preserve the optical metrics of Apple's variable
// system font. Keep Avalonia's glyph and cluster selection, but use CoreText
// positioning when both shapers produce the same result.
internal sealed class AppleSystemTextShaper(ITextShaperImpl fallback) : ITextShaperImpl
{
    public ITextShaperTypeface CreateTypeface(GlyphTypeface glyphTypeface) =>
        fallback.CreateTypeface(glyphTypeface);

    public ShapedBuffer ShapeText(ReadOnlyMemory<char> text, TextShaperOptions options)
    {
        var shaped = fallback.ShapeText(text, options);
        var platform = options.GlyphTypeface.PlatformTypeface;
        var tabularNumbers = false;
        if (options.FontFeatures is { Count: > 0 } features)
        {
            tabularNumbers = true;
            for (var i = 0; i < features.Count; i++)
            {
                var feature = features[i];
                if (feature.Tag == "tnum" && feature.Value == 1
                                          && feature.Start == 0 && feature.End == -1)
                    continue;

                tabularNumbers = false;
                break;
            }
        }
        if (text.IsEmpty || platform.FamilyName != ".AppleSystemUIFont"
            || platform.Stretch != FontStretch.Normal
            || (options.FontFeatures is { Count: > 0 } && !tabularNumbers)
            || text.Span.Contains('\t'))
            return shaped;

        var native = AppleCoreText.Shape(
            text.ToString(), options.FontRenderingEmSize, platform.Weight,
            platform.Style == FontStyle.Italic, tabularNumbers);
        if (native is null || native.Count != shaped.Length)
            return shaped;

        for (var i = 0; i < native.Count; i++)
            if (native[i].Glyph != shaped[i].GlyphIndex
                || native[i].Cluster != shaped[i].GlyphCluster)
                return shaped;

        var pen = 0d;
        var origin = native[0].X;
        for (var i = 0; i < native.Count; i++)
        {
            var item = native[i];
            shaped[i] = new GlyphInfo(
                item.Glyph,
                item.Cluster,
                item.Advance + options.LetterSpacing,
                new Vector(item.X - origin - pen, -item.Y));
            pen += item.Advance;
        }
        return shaped;
    }
}
