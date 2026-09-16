---
title: Theme and design tokens
description: Work with Cupertino control themes, classes, colors, typography, and motion resources.
ms.date: 2026-08-27
---

# Theme and design tokens

Add `CupertinoTheme` after a base Avalonia theme to give its controls iOS styling and motion. Their bindings, commands and events work as usual.

## Control classes

Use classes to choose a control's style and size:

```xml
<StackPanel Spacing="8">
  <Button Content="Default" />
  <Button Content="Continue" Classes="prominent" />
  <Button Content="Learn more" Classes="bordered" />
  <Button Content="Cancel" Classes="plain" />
  <Button Content="Compact" Classes="small" />
  <TextBox PlaceholderText="Name" />
  <TextBox Classes="search" PlaceholderText="Search" />
</StackPanel>
```

Combine compatible classes, such as `small prominent`, when the role and size are independent.

## Dynamic colors

Use Cupertino brushes through `DynamicResource` so the view follows light and dark appearance changes.

```xml
<Border Background="{DynamicResource CupertinoCardBrush}">
  <TextBlock Foreground="{DynamicResource CupertinoLabelBrush}"
             Text="Primary content" />
</Border>
```

Frequently used resources include:

| Resource | Purpose |
| --- | --- |
| `CupertinoLabelBrush` | Primary text and icons |
| `CupertinoSecondaryLabelBrush` | Supporting text |
| `CupertinoAccentBrush` | Interactive accent color |
| `CupertinoDangerBrush` | Destructive actions |
| `CupertinoSeparatorBrush` | Hairline separators |
| `CupertinoGroupedBackgroundBrush` | Page background behind grouped content |
| `CupertinoCardBrush` | Elevated cards and grouped rows |

The default English strings used by controls are also resources: `CupertinoPickerPlaceholderText`, `CupertinoDurationFormat`, `CupertinoSearchPlaceholderText`, `CupertinoSearchCancelText`, `CupertinoSearchEmptyText`, `CupertinoDialogDefaultActionText` and `CupertinoPageControlFormat`. Override them in your application resources to localize the theme.

## Typography and motion

Use the theme's typography classes to keep text sizes consistent. The theme also provides spring animation curves. Check `CupertinoAccessibility.ReduceMotion` before running your own animations.

## Glass materials

Three control themes for `GlassSurface` hold the shared materials: `CupertinoGlassButtonSurface` (buttons), `CupertinoPopoverSurface` (menus, flyouts and picker popovers) and `CupertinoBarCapsule` (navigation and toolbar capsules). Apply them with `Theme="{StaticResource ...}"` when composing your own glass controls so they match the theme.

## Menus and flyouts

Registering `CupertinoTheme` attaches the Cupertino opening and closing motion to every `Flyout`, `MenuFlyout` and `ContextMenu` in the application, and moves flyout popups into the window overlay layer so they can render live glass. This applies to controls outside Cupertino-styled subtrees as well.

## Right-to-left layouts

Controls inherit `FlowDirection`. Navigation, tab placement, swipe directions, and directional icons mirror with the surrounding view. Avoid manually reversing child collections for RTL.
