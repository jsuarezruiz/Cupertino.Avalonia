# Cupertino.Avalonia

An iOS 26 design system for [Avalonia](https://avaloniaui.net): control themes, typography, colour, motion and icons, plus the controls iOS has and Avalonia does not.

![Cupertino.Avalonia banner](images/cupertino-avalonia-banner.png)

<p align="center"><strong>Controls in action</strong><br>Fluid transitions, responsive effects and Liquid Glass in motion.</p>

<table width="100%">
  <tr>
    <td align="center" width="25%"><a href="images/cupertino-avalonia-tabs.gif"><img src="images/cupertino-avalonia-tabs.gif" alt="Tabs" width="100%"></a><br><sub>Tabs</sub></td>
    <td align="center" width="25%"><a href="images/cupertino-avalonia-slider.gif"><img src="images/cupertino-avalonia-slider.gif" alt="Slider" width="100%"></a><br><sub>Slider</sub></td>
    <td align="center" width="25%"><a href="images/cupertino-avalonia-timepicker.gif"><img src="images/cupertino-avalonia-timepicker.gif" alt="Time picker" width="100%"></a><br><sub>Time picker</sub></td>
    <td align="center" width="25%"><a href="images/cupertino-avalonia-toggleswitch.gif"><img src="images/cupertino-avalonia-toggleswitch.gif" alt="Toggle switch" width="100%"></a><br><sub>Toggle switch</sub></td>
  </tr>
</table>

## Install

```bash
dotnet add package Cupertino.Avalonia
```

## Use

Add `CupertinoTheme` after your base theme.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:cupertino="https://cupertino.avaloniaui.net">
  <Application.Styles>
    <FluentTheme />
    <cupertino:CupertinoTheme />
  </Application.Styles>
</Application>
```

```xml
<StackPanel xmlns:cupertino="https://cupertino.avaloniaui.net" Spacing="12">
  <Button Content="Continue" Classes="prominent" />
  <ToggleSwitch IsChecked="True" />
  <Slider Value="40" />
  <TextBox Classes="search" PlaceholderText="Search" />
</StackPanel>
```

## What is included

**Themed Avalonia controls.** Buttons in every role and size, switches, sliders, text fields including search and masked variants, checkboxes and radios, pickers for date and time, lists, trees, tabs, menus, flyouts, dialogs, progress, refresh and expanders.

**Controls iOS has that Avalonia does not.** `CupertinoSheet` with detents, `CupertinoSwipeView` for row actions, `CupertinoBadge`, `CupertinoToolbar`, `CupertinoNavigationPage` with the interactive back gesture and restorable routes, reusable `CupertinoListCell` and `CupertinoFormRow`, `CupertinoSearchView`, `CupertinoPageControl`, inline and compact date, time, duration and paired date-time pickers, `CupertinoCalendarView`, `CupertinoIcon` and the Liquid Glass surface itself.

**The design language.** Dynamic Type scaling, inherited RTL layout, the colour system in both light and dark variants, motion curves, and `GlassSurface`, a shader based material that samples and refracts what passes beneath it.

## The gallery

`samples/Cupertino.Gallery` is a browsable catalogue of every control with live samples and the source for each page. Run it on your desktop:

```bash
dotnet run --project samples/Cupertino.Gallery.Desktop
```

Mobile heads are available at `samples/Cupertino.Gallery.iOS` and
`samples/Cupertino.Gallery.Android`.

### Screenshots

#### Design language

<table width="100%">
  <tr>
    <td align="center" width="25%" colspan="3"><a href="images/liquid-glass.png"><img src="images/liquid-glass.png" alt="Liquid Glass" width="100%"></a><br><sub>Liquid Glass</sub></td>
    <td align="center" width="25%" colspan="3"><a href="images/colors.png"><img src="images/colors.png" alt="Colours" width="100%"></a><br><sub>Colours</sub></td>
    <td align="center" width="25%" colspan="3"><a href="images/typography.png"><img src="images/typography.png" alt="Typography" width="100%"></a><br><sub>Typography</sub></td>
    <td align="center" width="25%" colspan="3"><a href="images/icons.png"><img src="images/icons.png" alt="Icons" width="100%"></a><br><sub>Icons</sub></td>
  </tr>
  <tr>
    <td align="center" width="33%" colspan="4"><a href="images/photos.png"><img src="images/photos.png" alt="Photos" width="100%"></a><br><sub>Photos</sub></td>
    <td align="center" width="33%" colspan="4"><a href="images/lock-screen.png"><img src="images/lock-screen.png" alt="Lock screen" width="100%"></a><br><sub>Lock screen</sub></td>
    <td align="center" width="33%" colspan="4"><a href="images/side-by-side.png"><img src="images/side-by-side.png" alt="Light and dark themes side by side" width="100%"></a><br><sub>Light and dark themes</sub></td>
  </tr>
</table>

#### Navigation and layout

<table width="100%">
  <tr>
    <td align="center" width="25%"><a href="images/navigation-bar.png"><img src="images/navigation-bar.png" alt="Navigation bar" width="100%"></a><br><sub>Navigation bar</sub></td>
    <td align="center" width="25%"><a href="images/toolbar.png"><img src="images/toolbar.png" alt="Toolbar" width="100%"></a><br><sub>Toolbar</sub></td>
    <td align="center" width="25%"><a href="images/tab-control.png"><img src="images/tab-control.png" alt="Tab control" width="100%"></a><br><sub>Tab control</sub></td>
    <td align="center" width="25%"><a href="images/tab-strip.png"><img src="images/tab-strip.png" alt="Tab strip" width="100%"></a><br><sub>Tab strip</sub></td>
  </tr>
  <tr>
    <td align="center" width="25%"><a href="images/split-view.png"><img src="images/split-view.png" alt="Split view" width="100%"></a><br><sub>Split view</sub></td>
    <td align="center" width="25%"><a href="images/grid-splitter.png"><img src="images/grid-splitter.png" alt="Grid splitter" width="100%"></a><br><sub>Grid splitter</sub></td>
    <td align="center" width="25%"><a href="images/carousel.png"><img src="images/carousel.png" alt="Carousel" width="100%"></a><br><sub>Carousel</sub></td>
    <td align="center" width="25%"><a href="images/transitioning-content-control.png"><img src="images/transitioning-content-control.png" alt="Transitioning content control" width="100%"></a><br><sub>Transitioning content</sub></td>
  </tr>
</table>

#### Buttons

<table width="100%">
  <tr>
    <td align="center" width="33%"><a href="images/button.png"><img src="images/button.png" alt="Button" width="100%"></a><br><sub>Button</sub></td>
    <td align="center" width="33%"><a href="images/repeat-button.png"><img src="images/repeat-button.png" alt="Repeat button" width="100%"></a><br><sub>Repeat button</sub></td>
    <td align="center" width="33%"><a href="images/hyperlink-button.png"><img src="images/hyperlink-button.png" alt="Hyperlink button" width="100%"></a><br><sub>Hyperlink button</sub></td>
  </tr>
  <tr>
    <td align="center" width="33%"><a href="images/toggle-button.png"><img src="images/toggle-button.png" alt="Toggle button" width="100%"></a><br><sub>Toggle button</sub></td>
    <td align="center" width="33%"><a href="images/split-button.png"><img src="images/split-button.png" alt="Split button" width="100%"></a><br><sub>Split button</sub></td>
    <td align="center" width="33%"><a href="images/drop-down-button.png"><img src="images/drop-down-button.png" alt="Drop-down button" width="100%"></a><br><sub>Drop-down button</sub></td>
  </tr>
</table>

#### Input and selection

<table width="100%">
  <tr>
    <td align="center" width="25%"><a href="images/text-box.png"><img src="images/text-box.png" alt="Text box" width="100%"></a><br><sub>Text box</sub></td>
    <td align="center" width="25%"><a href="images/masked-text-box.png"><img src="images/masked-text-box.png" alt="Masked text box" width="100%"></a><br><sub>Masked text box</sub></td>
    <td align="center" width="25%"><a href="images/numeric-up-down.png"><img src="images/numeric-up-down.png" alt="Numeric up-down" width="100%"></a><br><sub>Numeric up-down</sub></td>
    <td align="center" width="25%"><a href="images/combo-box.png"><img src="images/combo-box.png" alt="Combo box" width="100%"></a><br><sub>Combo box</sub></td>
  </tr>
  <tr>
    <td align="center" width="25%"><a href="images/auto-complete-box.png"><img src="images/auto-complete-box.png" alt="Auto-complete box" width="100%"></a><br><sub>Auto-complete box</sub></td>
    <td align="center" width="25%"><a href="images/check-box.png"><img src="images/check-box.png" alt="Check box" width="100%"></a><br><sub>Check box</sub></td>
    <td align="center" width="25%"><a href="images/radio-button.png"><img src="images/radio-button.png" alt="Radio button" width="100%"></a><br><sub>Radio button</sub></td>
    <td align="center" width="25%"><a href="images/toggle-switch.png"><img src="images/toggle-switch.png" alt="Toggle switch" width="100%"></a><br><sub>Toggle switch</sub></td>
  </tr>
  <tr>
    <td align="center" width="50%" colspan="2"><a href="images/slider.png"><img src="images/slider.png" alt="Slider" width="50%"></a><br><sub>Slider</sub></td>
    <td align="center" width="50%" colspan="2"><a href="images/color-picker.png"><img src="images/color-picker.png" alt="Colour picker" width="50%"></a><br><sub>Colour picker</sub></td>
  </tr>
</table>

#### Date and time

<table width="100%">
  <tr>
    <td align="center" width="25%"><a href="images/calendar.png"><img src="images/calendar.png" alt="Calendar" width="100%"></a><br><sub>Calendar</sub></td>
    <td align="center" width="25%"><a href="images/calendar-date-picker.png"><img src="images/calendar-date-picker.png" alt="Calendar date picker" width="100%"></a><br><sub>Calendar date picker</sub></td>
    <td align="center" width="25%"><a href="images/date-picker.png"><img src="images/date-picker.png" alt="Date picker" width="100%"></a><br><sub>Date picker</sub></td>
    <td align="center" width="25%"><a href="images/time-picker.png"><img src="images/time-picker.png" alt="Time picker" width="100%"></a><br><sub>Time picker</sub></td>
  </tr>
</table>

#### Lists and collections

<table width="100%">
  <tr>
    <td align="center" width="33%" colspan="2"><a href="images/list-and-search.png"><img src="images/list-and-search.png" alt="List and search" width="100%"></a><br><sub>List and search</sub></td>
    <td align="center" width="33%" colspan="2"><a href="images/list-box.png"><img src="images/list-box.png" alt="List box" width="100%"></a><br><sub>List box</sub></td>
    <td align="center" width="33%" colspan="2"><a href="images/tree-view.png"><img src="images/tree-view.png" alt="Tree view" width="100%"></a><br><sub>Tree view</sub></td>
  </tr>
  <tr>
    <td align="center" width="50%" colspan="3"><a href="images/swipe-view.png"><img src="images/swipe-view.png" alt="Swipe view" width="66%"></a><br><sub>Swipe view</sub></td>
    <td align="center" width="50%" colspan="3"><a href="images/expander.png"><img src="images/expander.png" alt="Expander" width="66%"></a><br><sub>Expander</sub></td>
  </tr>
</table>

#### Feedback and presentation

<table width="100%">
  <tr>
    <td align="center" width="25%"><a href="images/badge.png"><img src="images/badge.png" alt="Badge" width="100%"></a><br><sub>Badge</sub></td>
    <td align="center" width="25%"><a href="images/notifications.png"><img src="images/notifications.png" alt="Notifications" width="100%"></a><br><sub>Notifications</sub></td>
    <td align="center" width="25%"><a href="images/progress-bar.png"><img src="images/progress-bar.png" alt="Progress bar" width="100%"></a><br><sub>Progress bar</sub></td>
    <td align="center" width="25%"><a href="images/refresh-container.png"><img src="images/refresh-container.png" alt="Refresh container" width="100%"></a><br><sub>Refresh container</sub></td>
  </tr>
  <tr>
    <td align="center" width="25%"><a href="images/dialog.png"><img src="images/dialog.png" alt="Dialog" width="100%"></a><br><sub>Dialog</sub></td>
    <td align="center" width="25%"><a href="images/sheet.png"><img src="images/sheet.png" alt="Sheet" width="100%"></a><br><sub>Sheet</sub></td>
    <td align="center" width="25%"><a href="images/flyout.png"><img src="images/flyout.png" alt="Flyout" width="100%"></a><br><sub>Flyout</sub></td>
    <td align="center" width="25%"><a href="images/context-menu.png"><img src="images/context-menu.png" alt="Context menu" width="100%"></a><br><sub>Context menu</sub></td>
  </tr>
  <tr>
    <td align="center" width="50%" colspan="2"><a href="images/menu.png"><img src="images/menu.png" alt="Menu" width="50%"></a><br><sub>Menu</sub></td>
    <td align="center" width="50%" colspan="2"><a href="images/tool-tip-and-label.png"><img src="images/tool-tip-and-label.png" alt="Tool tip and label" width="50%"></a><br><sub>Tool tip and label</sub></td>
  </tr>
</table>

## Documentation

* [FAQ](FAQ.md)
* [Changelog](CHANGELOG.md)
* [Release process](RELEASING.md)

To build the NuGet and symbol packages locally, run:

```bash
ALLOW_DIRTY=1 ./build/pack-nuget.sh 0.1.0-preview.1
```

## Requirements

.NET 8 or later and Avalonia 12.1.1. Desktop, iOS, Android and Browser targets all render the themes; the glass material needs a GPU surface and degrades to a flat material where one is not available or where Reduce Transparency is on.

## Licence

MIT. The icon set is original vector work authored to Apple's published metrics, because SF Symbols cannot be redistributed.
