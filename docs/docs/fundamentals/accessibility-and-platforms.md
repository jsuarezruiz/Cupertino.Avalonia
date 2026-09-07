---
title: Accessibility and platforms
description: Design for reduced motion, reduced transparency, input modes, and platform differences.
ms.date: 2026-08-27
---

# Accessibility and platforms

Cupertino.Avalonia supports desktop, iOS, Android and Browser. Your app can use the same controls on each platform and pass in the user's accessibility preferences.

## Connect platform preferences

Your app must read system preferences and pass them to `CupertinoAccessibility`. Implement `ICupertinoAccessibilityProvider` to keep them in sync. The galleries use switches in Settings for manual testing.

Reduced motion and reduced transparency default to `false`; text scale defaults to `1.0`. Set your provider after Avalonia initializes:

```csharp
using Cupertino.Controls;

// platformPreferences is your implementation of ICupertinoAccessibilityProvider.
CupertinoAccessibility.Provider = platformPreferences;
```

A provider returns the current preferences through `Current` and raises `Changed` when they change. Cupertino applies updates on the UI thread. Replacing or removing the provider disconnects the old subscription; removing it keeps the last applied values.

Your app owns the provider and its system event subscriptions. Refresh settings when the app resumes if needed, and release those subscriptions on shutdown. Test both startup values and changes while the app is running.

For settings managed by your app, set the properties directly:

```csharp
CupertinoAccessibility.ReduceMotion = true;
CupertinoAccessibility.ReduceTransparency = true;
CupertinoAccessibility.TextScaleFactor = 1.25;
```

## Reduced motion and transparency

Built-in transitions respect Reduce Motion. Check the same setting in your own animations:

```csharp
if (CupertinoAccessibility.ReduceMotion)
{
    ApplyFinalState();
    return;
}

StartTransition();
```

Activity indicators continue to show progress when reduced motion is enabled.

Reduce Transparency replaces live glass with an opaque fill. Check that text and control boundaries remain easy to see in both modes.

## Input and focus

- Give icon-only buttons accessible names. Tooltips can provide extra context.
- Preserve a minimum 44 pt interaction target where practical.
- Dialogs and sheets move focus into the overlay, cycle Tab navigation, and support Escape dismissal. Test focus restoration and nested overlays on desktop and Browser.
- Test touch drag thresholds for sheets, swipe rows, sliders, and navigation gestures on mobile.
- Do not use color alone to communicate destructive, selected, or invalid states.

## Platform notes

| Platform | Notes |
| --- | --- |
| Desktop | Supports pointer, keyboard, hover, and live glass through Skia |
| iOS | Primary reference for metrics, gestures, and behavior |
| Android | Uses the same Cupertino visual language and touch behavior |
| Browser | Supports the theme; renderer capabilities determine live-glass fidelity |

Use Avalonia's bindings, commands, validation, accessibility properties and focus navigation as usual.
