---
title: Toolbars, tabs, and badges
description: Compose bottom actions, destinations, and notification indicators.
ms.date: 2026-08-27
---

# Toolbars, tabs, and badges

<table>
  <tr>
    <td><img src="../../images/toolbar.png" alt="Cupertino bottom toolbar" width="300" /></td>
    <td><img src="../../images/tab-strip.png" alt="Cupertino tab strip" width="300" /></td>
    <td><img src="../../images/badge.png" alt="Cupertino badges on controls and tabs" width="300" /></td>
  </tr>
</table>

## Toolbars

Toolbars contain actions, not destinations. Consecutive items share one glass capsule; `ToolbarSpacer` breaks groups and distributes remaining width.

```xml
<cupertino:CupertinoToolbar>
  <Button ToolTip.Tip="Back">
    <cupertino:CupertinoIcon Glyph="chevron.left" Size="20" />
  </Button>
  <Button ToolTip.Tip="Forward">
    <cupertino:CupertinoIcon Glyph="chevron.right" Size="20" />
  </Button>
  <cupertino:ToolbarSpacer />
  <Button ToolTip.Tip="Search">
    <cupertino:CupertinoIcon Glyph="magnifyingglass" Size="20" />
  </Button>
</cupertino:CupertinoToolbar>
```

## Tabs

Tabs represent peer destinations. Use the themed `TabControl` or `TabStrip`, and set attached tab metadata through `Tabs` when an item needs an icon or badge.

Do not force equal-width tabs into a compact bar. The bar sizes to its content, while the selected lens retains its own native-like width.

## Badges

`CupertinoBadge` accepts an integer `Value`. Positive values render a count capped at `99+`; a negative value renders the dot form. `Tabs.BadgeValue` places the same badge anatomy on a tab icon.

Keep badge text supplementary. The associated control still needs an accessible label that explains the destination or action.
