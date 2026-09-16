using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class EveryControlRenderTests
{
    private static readonly Dictionary<string, Func<Control>> Factories = new()
    {
        ["DefaultButton"] = () => new Button { Content = "Button" },
        ["ProminentButton"] = () => new Button { Content = "Prominent", Classes = { "prominent" } },
        ["BorderedButton"] = () => new Button { Content = "Bordered", Classes = { "bordered" } },
        ["RepeatButton"] = () => new RepeatButton { Content = "Repeat" },
        ["ToggleButton"] = () => new ToggleButton { Content = "Toggle", IsChecked = true },
        ["SplitButton"] = () => new SplitButton { Content = "Split" },
        ["DropDownButton"] = () => new DropDownButton { Content = "Drop" },
        ["HyperlinkButton"] = () => new HyperlinkButton { Content = "Link" },
        ["ToggleSwitch"] = () => new ToggleSwitch { IsChecked = true },
        ["Slider"] = () => new Slider { Value = 40, Width = 200 },
        ["ProgressBar"] = () => new ProgressBar { Value = 60, Width = 200 },
        ["TextBox"] = () => new TextBox { Text = "Field", Width = 200 },
        ["SearchTextBox"] = () => new TextBox { Classes = { "search" }, Text = "Search", Width = 200 },
        ["MaskedTextBox"] = () => new MaskedTextBox { Mask = "(000) 000-0000", Width = 200 },
        ["AutoCompleteBox"] = () => new AutoCompleteBox { Text = "Auto", Width = 200 },
        ["NumericUpDown"] = () => new NumericUpDown { Value = 3 },
        ["CheckBox"] = () => new CheckBox { Content = "Check", IsChecked = true },
        ["RadioButton"] = () => new RadioButton { Content = "Radio", IsChecked = true },
        ["ComboBox"] = () => new ComboBox { ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0, Width = 160 },
        ["InsetListBox"] = () => new ListBox { ItemsSource = new[] { "One", "Two" }, Classes = { "inset" }, Width = 220 },
        ["TreeView"] = () => new TreeView { ItemsSource = new[] { "Root" }, Width = 220 },
        ["Expander"] = () => new Expander { Header = "Expander", Content = new TextBlock { Text = "Body" }, Width = 220 },
        ["Badge"] = () => new CupertinoBadge { Value = 5 },
        ["Icon"] = () => new CupertinoIcon { Glyph = "house", Size = 28 },
        ["GlassSurface"] = () => new GlassSurface { Width = 140, Height = 44, CornerRadius = new global::Avalonia.CornerRadius(22) },
        ["SheetPresenter"] = () => new CupertinoSheetPresenter
        {
            Width = 240,
            Height = 120,
            Content = new TextBlock
            {
                Text = "Sheet",
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            },
        },
        ["SwipeView"] = () => new CupertinoSwipeView
        {
            Width = 240,
            Height = 56,
            Content = new Border
            {
                Background = Brushes.White,
                Padding = new global::Avalonia.Thickness(16, 0),
                Child = new TextBlock
                {
                    Text = "Message",
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                },
            },
            TrailingActions = new Button { Content = "Delete", Classes = { "swipe" } },
        },
        ["RefreshContainer"] = () => new RefreshContainer { Width = 220, Height = 120, Content = new TextBlock { Text = "Pull" } },
        ["Section"] = () => new Section { Header = "SECTION", Content = new TextBlock { Text = "Body" }, Width = 260 },
        ["CalendarDatePicker"] = () => new CalendarDatePicker
        {
            SelectedDate = new DateTime(2026, 8, 3),
            Width = 200,
        },
        ["DatePicker"] = () => new DatePicker
        {
            SelectedDate = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
            Width = 220,
        },
        ["TimePicker"] = () => new TimePicker
        {
            SelectedTime = new TimeSpan(9, 41, 0),
            ClockIdentifier = "12HourClock",
            Width = 220,
        },
        ["SplitView"] = () => new SplitView
        {
            Width = 240,
            Height = 120,
            DisplayMode = SplitViewDisplayMode.Inline,
            IsPaneOpen = true,
            OpenPaneLength = 96,
            Pane = new TextBlock
            {
                Text = "Sidebar",
                Margin = new global::Avalonia.Thickness(12),
            },
            Content = new TextBlock
            {
                Text = "Content",
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            },
        },
        ["TabStrip"] = () => new TabStrip { ItemsSource = new[] { "One", "Two" }, Width = 240 },
        ["ListCell"] = () => new CupertinoListCell
        {
            Title = "Notifications",
            Subtitle = "Banners and sounds",
            Detail = "On",
            AccessoryKind = CupertinoListAccessory.Disclosure,
            Width = 300,
        },
        ["FormRow"] = () => new CupertinoFormRow
        {
            Label = "Name",
            HelpText = "Required",
            Content = new TextBox { Text = "Ada" },
            Width = 320,
        },
        ["PageControl"] = () => new CupertinoPageControl
        {
            NumberOfPages = 5,
            CurrentPage = 2,
        },
        ["SearchView"] = () => new CupertinoSearchView
        {
            ItemsSource = new[] { "Ada", "Grace" },
            Text = "a",
            Width = 320,
        },
        ["DateTimePicker"] = () => new CupertinoDateTimePicker
        {
            SelectedDateTime = new DateTimeOffset(2026, 8, 3, 9, 41, 0, TimeSpan.Zero),
            // Pinned so the reference image does not depend on the host's date pattern.
            DateFormat = "d MMM yyyy",
        },
    };

    public static IEnumerable<object[]> Controls() =>
        Factories.Keys.Select(k => new object[] { k });

    [AvaloniaTheory]
    [MemberData(nameof(Controls))]
    public void Control_paints_something(string name)
    {
        var control = Factories[name]();
        control.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        control.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;

        var luma = Probe.Render("control-" + name, control, 360, 200, Colors.White);

        var distinct = new HashSet<byte>();
        for (var y = 0; y < 200; y++)
            for (var x = 0; x < 360; x++)
                distinct.Add(luma[x, y]);

        Assert.True(distinct.Count > 1, $"{name} rendered a blank frame");
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0,
            $"{name} laid out empty");
    }
}
