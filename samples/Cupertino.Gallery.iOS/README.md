# Compare with UIKit

The **Side by Side** gallery page places UIKit controls beside their Cupertino.Avalonia equivalents. Run `Cupertino.Gallery.iOS` on an iPhone simulator and use the gallery settings to compare light and dark appearance.

To capture the simulator:

```sh
xcrun simctl io booted screenshot /tmp/cupertino-comparison.png
```

UIKit uses SF Symbols and its own compositor, so matching geometry does not guarantee identical rendering or motion. Check scrolling and pressed states on a real device as well. Desktop reference testing is documented in the [render test guide](../../tests/Cupertino.Avalonia.RenderTests/README.md).

## System typography

The iOS sample enables the package's `UseCupertinoSystemFont()` integration because Avalonia 12.1.1 does not preserve the optical metrics of Apple's variable system font. The adapter keeps Avalonia's glyph selection but uses CoreText positioning when both shapers produce the same glyphs and clusters. Unsupported cases use Avalonia's original shaper.

The integration ships in the Cupertino NuGet package and currently targets Avalonia 12.1.1's shaping API. Revalidate it when upgrading Avalonia. The AVA3001 build warning is expected.

To run the optional CoreText comparison on an installed simulator build:

```sh
SIMCTL_CHILD_GALLERY_VERIFY_FONTS=1 \
  xcrun simctl launch --terminate-running-process --console \
  booted net.avaloniaui.cupertino.gallery
```

Look for `[font-check] PASS` in the Terminal output.
