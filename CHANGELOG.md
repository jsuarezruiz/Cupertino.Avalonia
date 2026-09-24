# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The browser gallery shows Avalonia's frame-rate and render-time overlay with `?fps`, and can force its software-rendering fallback with `?software` for diagnostics.

### Changed

- Reduced browser download size and startup work, and improved control creation performance.
- Text boxes, buttons and sliders build fewer template parts.
- Pickers, wheels and calendars draw text faster and reuse their item lists, so pickers open faster and hold less memory.
- Glass redraws less work while wheels, calendars and activity indicators animate inside it, and stops repainting when a transparent parent hides it.

### Fixed

- Improved browser glass performance and removed flyout shadow flashes. Glass now follows its opacity, and uses the flat material when the browser renders without a GPU.
- The AutoCompleteBox suggestion list matches the width and position of its field.
- Fixed navigation transitions and several calendar, picker, activity-indicator and tab-bar interactions.
- Interrupted gestures now reset the navigation back swipe, tab bar, sheet, slider and switch instead of leaving them mid-drag.
- Calendars and date pickers show Gregorian month names and years in cultures with a non-Gregorian default calendar, and no longer crash near the end of those calendars' ranges.
- Glass renders in right-to-left layouts instead of falling back to a flat fill, and the search field shadow keeps its size there.
- Popovers, flyouts and menus opened with Reduce Motion still close after it is turned off.
- A sheet being dismissed ignores new touches, so grabbing it can no longer freeze it on screen.
- A context menu whose opening is cancelled no longer stays invisible the next time it opens.
- A calendar created with its month picker open no longer crashes.
- Glass no longer shows a lighter rectangle behind a hovered or pressed item, such as a tab in the bottom tab bar.

## [0.2.0-preview] - 2026-09-21

### Added

- Published the documentation and live gallery, and added third-party license notices.

### Changed

- The package now requires Avalonia 12.x and declares trimming and AOT compatibility.
- Improved resource customization, accessibility, search state preservation and browser download size.

### Fixed

- Improved Liquid Glass rendering and reduced idle repainting and per-frame allocations.
- Fixed calendar, picker, animation, gesture, font, notification and gallery issues.

### Removed

- Removed unused slider brushes and `CupertinoConverters.DayColumnAbbreviation`.

## [0.1.0-preview] - 2026-09-10

Initial public preview of the iOS 26 design system, including Cupertino control themes, typography, color, motion, custom controls, icons, and the Liquid Glass material.
