---
title: Theme and design tokens
description: Work with Cupertino control themes, classes, colors, typography, and motion resources.
ms.date: 2026-08-27
---

# Theme and design tokens

`CupertinoTheme` is layered on top of a base Avalonia theme. Standard Avalonia types keep their normal APIs while receiving iOS-oriented templates, sizing, typography, states, and motion.

## Control classes

Classes select semantic variants instead of hard-coded colors or dimensions.

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
<Border Background="{DynamicResource CupertinoSecondarySystemBackgroundBrush}">
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
| `CupertinoSystemBackgroundBrush` | Primary grouped background |
| `CupertinoSecondarySystemBackgroundBrush` | Elevated/grouped content |

## Typography and motion

The theme supplies semantic typography classes and iOS-like spring curves. Prefer the theme defaults and semantic roles over setting a font size on every element. Respect `CupertinoAccessibility.ReduceMotion` when adding custom animations beside the built-in controls.

## Right-to-left layouts

Controls inherit `FlowDirection`. Navigation, tab placement, swipe directions, and directional icons mirror with the surrounding view. Avoid manually reversing child collections for RTL.
