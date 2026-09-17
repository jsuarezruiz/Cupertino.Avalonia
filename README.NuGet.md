# Cupertino.Avalonia

![Cupertino.Avalonia banner](https://raw.githubusercontent.com/jsuarezruiz/Cupertino.Avalonia/main/images/cupertino-avalonia-banner.png)

An iOS 26 design system for Avalonia with control themes, custom controls, motion, typography, color and Liquid Glass.

## Install

```bash
dotnet add package Cupertino.Avalonia --prerelease
```

## Use

Add `CupertinoTheme` after your base theme.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:cupertino="https://cupertino.avaloniaui.net">
  <Application.Styles>
    <FluentTheme />
    <cupertino:CupertinoTheme />
  </Application.Styles>
</Application>
```

## Accessibility

Cupertino.Avalonia does not read operating-system accessibility preferences automatically. After Avalonia initializes, update `CupertinoAccessibility.ReduceMotion`, `ReduceTransparency`, and `TextScaleFactor` with values supplied by your application. See the [accessibility guide](https://jsuarezruiz.github.io/Cupertino.Avalonia/docs/fundamentals/accessibility-and-platforms.html).

## Apple system fonts

On macOS, iOS and Mac Catalyst, enable CoreText positioning for Apple system fonts. Unsupported text runs continue to use Avalonia's original text shaper.

For desktop applications, call `UseCupertinoSystemFont` after selecting the Avalonia platform:

```csharp
using Avalonia;
using Cupertino;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseCupertinoSystemFont();
```

For iOS applications, add it in the app delegate:

```csharp
using Avalonia;
using Cupertino;

protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
{
    return base.CustomizeAppBuilder(builder)
        .UseCupertinoSystemFont();
}
```

Explore the complete control catalog in the [live gallery](https://jsuarezruiz.github.io/Cupertino.Avalonia/gallery/).

## License

MIT. The Liquid Glass shader adapts MIT-licensed work from [LiquidGlassAvaloniaUI](https://github.com/KaranocaVe/LiquidGlassAvaloniaUI) (refraction) and [flutter_liquid_glass](https://github.com/whynotmake-it/flutter_liquid_glass) (lighting); the notices ship in the package as `THIRD-PARTY-NOTICES.md`.

## Links

- [Source](https://github.com/jsuarezruiz/Cupertino.Avalonia)
- [FAQ](https://github.com/jsuarezruiz/Cupertino.Avalonia/blob/main/FAQ.md)
- [Release notes](https://github.com/jsuarezruiz/Cupertino.Avalonia/releases)
