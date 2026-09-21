# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0-preview] - 2026-09-21

### Added

- Published the documentation and live gallery to GitHub Pages.
- Added third-party license notices to the package.

### Changed

- The package now requires Avalonia 12.x and declares trimming and AOT compatibility.
- Default interface text can be overridden through theme resources.
- Shared glass styles reduce duplication across buttons, bars, menus, flyouts and pickers.
- Search results now update in place, preserving list state while filtering.
- Expanded keyboard focus and screen-reader support across controls.
- The browser gallery no longer ships the source-view editor stack, cutting its download payload; desktop and mobile keep the View-source action.

### Fixed

- Improved Liquid Glass accuracy, repainting, resource reuse and snapshot cleanup.
- Cut idle glass repainting in the browser gallery: no repaint on pointer move, static heroes are not live.
- Reduced per-frame allocations across controls and animations.
- Gallery View-source action now appears at any window width on desktop and mobile.
- Fixed calendar and picker culture defaults, popup positioning and time rounding.
- Picker and calendar buttons announce their value or purpose instead of a child element type name.
- Gallery capture switches take effect while capturing, so `--scroll`, `--alert`, `--sheet`, `--hover` and `--noactions` shape the saved image.
- Corrected animation timing, gesture handling and haptics.
- Improved Apple system-font shaping, consistency and caching.
- Fixed notification layout and keyboard access and several documentation examples.

### Removed

- Removed unused slider brushes and `CupertinoConverters.DayColumnAbbreviation`.

## [0.1.0-preview] - 2026-09-10

Initial public preview of the iOS 26 design system, including Cupertino control themes, typography, color, motion, custom controls, icons, and the Liquid Glass material.

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
