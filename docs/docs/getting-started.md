---
title: Getting started
description: Install Cupertino.Avalonia and register the theme.
ms.date: 2026-08-27
---

# Getting started

## Requirements

- .NET 8 or later
- Avalonia 12.1.1
- Skia rendering for live Liquid Glass. When backdrop sampling is unavailable or Reduce Transparency is enabled, the material uses a flat fill.

## Install the package

```bash
dotnet add package Cupertino.Avalonia
```

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

Use standard Avalonia controls where possible. The additional Cupertino controls use the same bindings, styles and templates.

## Next steps

- Read the [control overview](controls/overview.md).
- Learn how [theme resources](fundamentals/theme-and-tokens.md) support light and dark appearances.
- Run the [gallery](gallery.md) to try the controls and their sample states.
