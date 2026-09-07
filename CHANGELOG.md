# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.1]

Initial public preview of the iOS 26 design system, including Cupertino control themes, typography, colour, motion, custom controls, icons, and the Liquid Glass material.

### Added

- macOS gallery app bundle with application metadata, icons, and ad-hoc signing during publish.
- Gallery tests across four layout configurations, plus checks for Settings, source views, and text orientation in right-to-left layouts.
- Regression tests for picker bounds, collection changes, keyboard focus, control cleanup, and reduced motion. Glass repaint tests cover 1× and 2× scaling with default and region-based dirty tracking.

### Changed

- Removed `UseCupertino()` and its setup instructions. Registering `CupertinoTheme` is sufficient, and apps retain control of their compositor settings.
- Completed public API documentation, standardized XML summaries to three lines, and removed unnecessary hard wrapping from Markdown prose. Missing public XML documentation now fails compilation.
- Clarified how apps supply accessibility preferences, when glass uses a fallback, and when releases need a new version.
- Pinned DocFX to 2.78.4 and made documentation warnings fail the documentation build.

### Fixed

- Preserved appearance, layout direction, and accessibility switch values when reopening gallery Settings. Added accessible names to its icon buttons and switches.
- Kept calendar and wheel text upright in right-to-left layouts, including direction changes at runtime. Calendar month and first-weekday changes now update measurement.
- Released pages removed from navigation while keeping pages still in the stack available.
- Made full swipes honor button commands, command parameters, disabled actions, and handled click events.
- Added keyboard focus, focus cycling, Escape dismissal, focus restoration, and content cleanup to sheets and popovers.
- Preserved picker dates outside 1900–2100. Combined date-time bounds and minute rounding respect the selected offset and supported date range.
- Kept open pickers in sync with property changes and handled popovers opened before layout or rapidly reopened. Selections set by the app now take priority over active wheel gestures.
- Updated visible dialog actions when the action collection is replaced.
- Updated search results without filtering the whole collection again. Selection survives item and range moves and removal of a duplicate result.
- Applied reduced-motion preferences to template transitions, navigation, dialogs, switches, and wheels. Detached switches release their accessibility subscriptions and refresh preferences on reattachment.
- Fixed toolbar template reuse and cleaned up source-view resources, safe-area subscriptions, and native iOS feedback generators.
- Derived list-cell accessible names from their titles while preserving application overrides.
- Limited glass memory allocations, added rendering fallbacks, and cleaned up native filters. Flat glass avoids unnecessary continuous rendering, and wheel and calendar text use bounded caches.
- Updated rendering reference images to match the current palette, closed windows created by tests, and corrected simulated swipe timing.
- Corrected Release configuration selection in CI for both PowerShell and Bash runners.
