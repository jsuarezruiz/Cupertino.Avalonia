---
title: Accessibility and platforms
description: Design for reduced motion, reduced transparency, input modes, and platform differences.
ms.date: 2026-08-27
---

# Accessibility and platforms

Cupertino.Avalonia is designed for desktop, iOS, Android, and Browser targets. The same XAML surface is used everywhere, with adaptations for the active renderer and accessibility settings.

## Reduced motion and transparency

Built-in navigation, sheets, popovers, switches, and other animated controls consult `CupertinoAccessibility`. Custom motion should do the same:

```csharp
if (CupertinoAccessibility.ReduceMotion)
{
    ApplyFinalState();
    return;
}

StartTransition();
```

Live glass falls back to a non-refractive material when Reduce Transparency is active. Keep text contrast and control boundaries usable in both modes.

## Input and focus

- Provide tooltips or accessible names for icon-only buttons.
- Preserve a minimum 44 pt interaction target where practical.
- Test keyboard focus and activation on desktop and Browser.
- Test touch drag thresholds for sheets, swipe rows, sliders, and navigation gestures on mobile.
- Do not use color alone to communicate destructive, selected, or invalid states.

## Platform notes

| Platform | Notes |
| --- | --- |
| Desktop | Supports pointer, keyboard, hover, and live GPU glass |
| iOS | Primary reference for metrics, gestures, and behavior |
| Android | Uses the same Cupertino visual language and touch behavior |
| Browser | Supports the theme; renderer capabilities determine live-glass fidelity |

The visual language follows iOS, but the controls remain Avalonia controls. Use Avalonia binding, commands, validation, automation properties, and focus navigation normally.
