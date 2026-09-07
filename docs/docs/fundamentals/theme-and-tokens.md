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

Use the theme's typography classes to keep text sizes consistent. The theme also provides spring animation curves. Check `CupertinoAccessibility.ReduceMotion` before running your own animations.

## Right-to-left layouts

Controls inherit `FlowDirection`. Navigation, tab placement, swipe directions, and directional icons mirror with the surrounding view. Avoid manually reversing child collections for RTL.
