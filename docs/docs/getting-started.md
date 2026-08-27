---
title: Getting started
description: Install Cupertino.Avalonia, enable its renderer configuration, and register the theme.
ms.date: 2026-08-27
---

# Getting started

## Requirements

- .NET 8 or later
- Avalonia 12.1.1
- A GPU-backed surface for live Liquid Glass. The material falls back gracefully when live sampling is unavailable or Reduce Transparency is enabled.

## Install the package

```bash
dotnet add package Cupertino.Avalonia
```

## Configure rendering

Call `UseCupertino()` on the application builder. It configures full-frame composition so live glass can sample the complete backdrop.

```csharp
using Avalonia;
using Cupertino;

public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UseCupertino()
        .WithInterFont();
```

> [!NOTE]
> The theme still works without `UseCupertino()`, but live refractive surfaces can retain stale regions when the renderer uses dirty-rectangle clipping.

## Register the theme

Keep your base Avalonia theme first and add `CupertinoTheme` after it.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:cupertino="https://cupertino.avaloniaui.net"
             x:Class="MyApp.App">
  <Application.Styles>
    <FluentTheme />
    <cupertino:CupertinoTheme />
  </Application.Styles>
</Application>
```

The XML namespace covers controls, themes, icons, and motion types:

```xml
xmlns:cupertino="https://cupertino.avaloniaui.net"
```

## Add your first view

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:cupertino="https://cupertino.avaloniaui.net"
            Spacing="12">
  <Button Content="Continue" Classes="prominent" />
  <ToggleSwitch IsChecked="True" />
  <Slider Minimum="0" Maximum="100" Value="40" />
  <TextBox Classes="search" PlaceholderText="Search" />
  <cupertino:CupertinoDatePicker SelectedDate="{Binding Date}" />
</StackPanel>
```

Use normal Avalonia controls whenever one exists. Cupertino-specific controls fill platform gaps and use the same binding, styling, and templating model.

## Next steps

- Read the [control overview](controls/overview.md).
- Learn how [theme resources](fundamentals/theme-and-tokens.md) support light and dark appearances.
- Run the [gallery](gallery.md) to inspect every state interactively.
