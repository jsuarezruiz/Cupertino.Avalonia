using Avalonia;
using Avalonia.Media;

namespace Cupertino.Gallery;

/// <summary>
/// Adds the symbol families the gallery needs beyond Inter.
/// </summary>
public static class SymbolFontFallbacks
{
    /// <summary>
    /// A symbol family and the asset that carries it.
    /// </summary>
    public readonly record struct BundledFont(string Asset, string FamilyName)
    {
        /// <summary>
        /// The embedded font file.
        /// </summary>
        public Uri Uri => new(Asset);

        /// <summary>
        /// The family the gallery registers for fallback.
        /// </summary>
        public FontFamily Family => new($"avares://Cupertino.Gallery/Assets/Fonts#{FamilyName}");
    }

    /// <summary>
    /// The bundled families, in the order Avalonia should try them.
    /// </summary>
    public static readonly IReadOnlyList<BundledFont> Fonts =
    [
        new("avares://Cupertino.Gallery/Assets/Fonts/NotoSansSymbols2-Regular.ttf", "Noto Sans Symbols 2"),
        new("avares://Cupertino.Gallery/Assets/Fonts/NotoSansSymbols-Regular.ttf", "Noto Sans Symbols"),
        new("avares://Cupertino.Gallery/Assets/Fonts/NotoSansMath-Regular.ttf", "Noto Sans Math"),
    ];

    /// <summary>
    /// Registers the bundled symbol families for characters Inter does not carry,
    /// such as the marks the catalog draws on its tiles.
    /// </summary>
    public static AppBuilder UseCupertinoSymbolFallbacks(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.With(new FontManagerOptions
        {
            FontFallbacks = [.. Fonts.Select(font => new FontFallback { FontFamily = font.Family })],
        });
    }
}
