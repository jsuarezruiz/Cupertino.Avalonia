# Cupertino.Avalonia

![Cupertino.Avalonia banner](https://raw.githubusercontent.com/jsuarezruiz/Cupertino.Avalonia/main/images/cupertino-avalonia-banner.png)

An iOS 26 design system for Avalonia with control themes, custom controls, motion, typography, colour and Liquid Glass.

## Install

```bash
dotnet add package Cupertino.Avalonia
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

Your app supplies system accessibility preferences through `CupertinoAccessibility`. See the [accessibility guide](https://jsuarezruiz.github.io/Cupertino.Avalonia/docs/fundamentals/accessibility-and-platforms.html) for setup.

On Apple platforms, enable native CoreText advances when configuring the app so SF typography keeps its platform spacing:

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseCupertinoSystemFont();
```

See the [gallery and screenshots](https://github.com/jsuarezruiz/Cupertino.Avalonia#the-gallery) for the complete control catalogue.

## Links

- [Source](https://github.com/jsuarezruiz/Cupertino.Avalonia)
- [FAQ](https://github.com/jsuarezruiz/Cupertino.Avalonia/blob/main/FAQ.md)
- [Release notes](https://github.com/jsuarezruiz/Cupertino.Avalonia/releases)
