using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Cupertino.Gallery;
using SkiaSharp;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class GallerySymbolFontTests
{
    // Read from the font files rather than the platform font manager, so a system
    // font on the build machine cannot make this pass.
    private static IEnumerable<Uri> CoverageFonts() =>
    [
        .. AssetLoader.GetAssets(new Uri("avares://Avalonia.Fonts.Inter/Assets"), null)
                       .Where(uri => uri.AbsolutePath.Contains("Inter-Regular", StringComparison.Ordinal)),
        .. SymbolFontFallbacks.Fonts.Select(font => font.Uri),
    ];

    private static SKTypeface Load(Uri uri)
    {
        using var stream = AssetLoader.Open(uri);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return SKTypeface.FromData(SKData.CreateCopy(buffer.ToArray()))
               ?? throw new InvalidOperationException($"{uri} is not a readable font");
    }

    [AvaloniaFact]
    public void Every_catalog_mark_is_carried_by_a_font_the_gallery_ships()
    {
        var typefaces = new List<SKTypeface>();
        try
        {
            foreach (var uri in CoverageFonts())
            {
                Assert.True(AssetLoader.Exists(uri), $"{uri} should be embedded");
                typefaces.Add(Load(uri));
            }

            var uncovered = new List<string>();
            Assert.NotEmpty(ShellView.Entries);
            foreach (var entry in ShellView.Entries)
            {
                foreach (var mark in entry.Glyph.EnumerateRunes())
                {
                    if (mark.Value < 0x80)
                        continue;
                    if (typefaces.Any(typeface => typeface.GetGlyph(mark.Value) != 0))
                        continue;

                    uncovered.Add($"U+{mark.Value:X4} used by {entry.Title}");
                }
            }

            Assert.Empty(uncovered);
        }
        finally
        {
            foreach (var typeface in typefaces)
                typeface.Dispose();
        }
    }
}
