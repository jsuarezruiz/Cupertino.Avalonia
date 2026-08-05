# FAQ

### Is this a wrapper around UIKit or SwiftUI?

No. These are Avalonia controls styled and behaving like their iOS ounterparts. Nothing calls into Apple frameworks, and the library runs anywhere Avalonia runs.

### Does it work on platforms other than iOS?

Yes. The themes render on desktop, Android and Browser too. The glass material needs a GPU surface and falls back to a flat material without one, or when Reduce Transparency is on.

### Do I have to use the custom controls?

No. Adding `CupertinoTheme` restyles the Avalonia controls you already use. The extra controls are there when you want an experience Avalonia has no counterpart for, such as a sheet with detents or swipe actions on a row.

### Why not SF Symbols for the icons?

Apple does not permit redistributing them. `CupertinoIcon` renders original vector glyphs authored to Apple's published metrics, each in its own design box so alignment and paint mode are handled for you.

### How faithful is it really?

Every metric that is written as measured was taken from official Figma documents, running native apps on the iOS Simulator and verified against our own render.

### Does it support dark mode?

Yes, through Avalonia's theme variants, with a full set of dark resources. Be aware that most measurements so far were captured in light mode, so dark values are derived rather than measured, and a dark measurement pass is on the roadmap.

### Does it support accessibility settings?

Reduce Motion and Reduce Transparency are honoured by the animated and glass paths.

### Can I change the accent colour?

Yes. `CupertinoTheme.Accent` retints the whole accent family, meaning buttons, focus rings and selection, in both variants and at runtime. You can also override `CupertinoAccentBrush` in any control's resources to retint just that subtree.

### Is it production ready?

It is a preview and the API may still move.
