using Avalonia.Media;
using UIKit;

namespace Cupertino.Gallery;

internal static class AppleSystemFont
{
    internal static UIFontWeight ToNativeWeight(FontWeight weight) => (int)weight switch
    {
        <= 100 => UIFontWeight.UltraLight,
        <= 200 => UIFontWeight.Thin,
        <= 300 => UIFontWeight.Light,
        <= 400 => UIFontWeight.Regular,
        <= 500 => UIFontWeight.Medium,
        <= 600 => UIFontWeight.Semibold,
        <= 700 => UIFontWeight.Bold,
        <= 800 => UIFontWeight.Heavy,
        _ => UIFontWeight.Black,
    };
}
