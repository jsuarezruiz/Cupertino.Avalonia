# FAQ

### Is this a wrapper around UIKit or SwiftUI?

No. These are Avalonia controls with an iOS appearance and interaction style. The library does not wrap UIKit or SwiftUI.

### Does it work on platforms other than iOS?

Yes. The themes also support desktop, Android and Browser. Liquid Glass uses Skia through GPU or software rendering. It uses a flat fill when backdrop sampling is unavailable or Reduce Transparency is enabled.

### Do I have to use the custom controls?

No. Adding `CupertinoTheme` restyles the Avalonia controls you already use. Use the extra controls for features such as draggable sheets or swipe actions on a row.

### Why not SF Symbols for the icons?

`CupertinoIcon` provides the library's own vector icons, included with the theme. It has no dependency on SF Symbols.

### How faithful is it really?

The controls aim for the appearance and interaction of iOS 26. Exact animation parity with native controls has not been verified.

### Does it support dark mode?

Yes. The theme includes light and dark colours and follows Avalonia's theme selection.

### Does it support accessibility settings?

Yes. Your app supplies reduced motion, reduced transparency and text scale through `CupertinoAccessibility.Provider` or its individual properties. System settings are not read automatically. See the [accessibility guide](docs/docs/fundamentals/accessibility-and-platforms.md) for setup.

### Can I change the accent colour?

Yes. Set `CupertinoTheme.Accent` to change buttons, focus rings and selection in both themes at runtime. Override `CupertinoAccentBrush` in a control's resources to change just that control and its children.

### Is it production ready?

It is a preview, so the API can change.
