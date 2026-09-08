using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Cupertino.Gallery;
using Cupertino.Gallery.Pages;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class GalleryIntegrationTests
{
    [AvaloniaTheory]
    [InlineData(false, 402, false, 1.0)]
    [InlineData(true, 402, false, 1.0)]
    [InlineData(false, 900, false, 1.0)]
    [InlineData(true, 402, true, 1.5)]
    public void Every_catalog_page_navigates_renders_scrolls_and_returns(
        bool dark, int width, bool rtl, double textScale)
    {
        var oldMotion = CupertinoAccessibility.ReduceMotion;
        var oldScale = CupertinoAccessibility.TextScaleFactor;
        var oldSink = Logger.Sink;
        var diagnostics = new BindingDiagnostics();
        var window = new Window
        {
            Width = width,
            Height = 844,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        };
        try
        {
            CupertinoAccessibility.ReduceMotion = true;
            CupertinoAccessibility.TextScaleFactor = textScale;
            Logger.Sink = diagnostics;
            var shell = new ShellView();
            window.Content = shell;
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var wide = width >= ShellView.WideLayoutBreakpoint;
            var nav = shell.FindControl<CupertinoNavigationPage>(
                wide ? "WideDetailNav" : "Nav")!;
            var catalogNav = shell.FindControl<CupertinoNavigationPage>(
                wide ? "WideCatalogNav" : "Nav")!;
            var root = Assert.IsType<RootPage>(catalogNav.RootContent);
            var catalog = root.GetVisualDescendants().OfType<ListBox>()
                .SelectMany(list => list.Items.OfType<CatalogEntry>()
                    .Select(entry => (List: list, Entry: entry))).ToArray();
            Assert.NotEmpty(catalog);
            var visited = new List<Type>();

            foreach (var (list, entry) in catalog)
            {
                list.SelectedItem = entry;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var page = Assert.IsAssignableFrom<Control>(nav.CurrentContent);
                Assert.NotSame(root, page);
                Assert.Equal(wide ? 0 : 1, nav.Depth);
                Assert.True(page.Bounds.Width > 0 && page.Bounds.Height > 0, entry.Title);
                visited.Add(page.GetType());
                Capture(window, page.GetType().Name, dark, width, rtl, "top");

                // Scroll the page itself; nested lists keep their own positions.
                var scroll = page.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (scroll is not null && scroll.Extent.Height > scroll.Viewport.Height)
                {
                    scroll.ScrollToEnd();
                    window.UpdateLayout();
                    Capture(window, page.GetType().Name, dark, width, rtl, "bottom");
                    Assert.True(scroll.Offset.Y > 0, entry.Title);
                }

                Assert.All(page.GetVisualDescendants(), visual =>
                {
                    Assert.True(double.IsFinite(visual.Bounds.Width), entry.Title);
                    Assert.True(double.IsFinite(visual.Bounds.Height), entry.Title);
                });
                Assert.True(diagnostics.Messages.Count == 0,
                    entry.Title + ": " + string.Join(Environment.NewLine, diagnostics.Messages));
                if (wide)
                {
                    nav.RootContent = new HomePage();
                    window.UpdateLayout();
                    Assert.Null(page.GetVisualParent());
                }
                else
                {
                    Assert.True(nav.TryPop());
                    window.UpdateLayout();
                    Assert.Same(root, nav.CurrentContent);
                    Assert.Null(page.GetVisualParent());
                }
                Assert.Null(list.SelectedItem);
            }

            var expected = typeof(ButtonPage).Assembly.GetTypes().Where(type =>
                type.Namespace == typeof(ButtonPage).Namespace &&
                typeof(UserControl).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null &&
                type != typeof(SideBySidePage)); // Requires the iOS native comparison host.
            Assert.Equal(expected.OrderBy(t => t.Name), visited.OrderBy(t => t.Name));
        }
        finally
        {
            window.Close();
            Logger.Sink = oldSink;
            CupertinoAccessibility.TextScaleFactor = oldScale;
            CupertinoAccessibility.ReduceMotion = oldMotion;
        }
    }

    [AvaloniaFact]
    public void Gallery_preserves_the_open_sample_when_its_layout_changes()
    {
        var oldMotion = CupertinoAccessibility.ReduceMotion;
        var shell = new ShellView();
        var window = new Window { Width = 600, Height = 844, Content = shell };
        try
        {
            CupertinoAccessibility.ReduceMotion = true;
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var compactNav = shell.FindControl<CupertinoNavigationPage>("Nav")!;
            var wideLayout = shell.FindControl<Grid>("WideLayout")!;
            var wideCatalogNav = shell.FindControl<CupertinoNavigationPage>("WideCatalogNav")!;
            var wideDetailNav = shell.FindControl<CupertinoNavigationPage>("WideDetailNav")!;
            Assert.True(compactNav.IsVisible);
            Assert.False(wideLayout.IsVisible);

            var compactRoot = Assert.IsType<RootPage>(compactNav.RootContent);
            var button = compactRoot.ControlEntries.Single(entry => entry.Title == "Button");
            var list = compactRoot.GetVisualDescendants().OfType<ListBox>()
                .Single(item => item.Items.Contains(button));
            list.SelectedItem = button;
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<ButtonPage>(compactNav.CurrentContent);

            window.Width = 1000;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.False(compactNav.IsVisible);
            Assert.True(wideLayout.IsVisible);
            Assert.IsType<RootPage>(wideCatalogNav.RootContent);
            Assert.IsType<ButtonPage>(wideDetailNav.CurrentContent);
            Assert.True(wideCatalogNav.Bounds.Width > 0);
            Assert.True(wideDetailNav.Bounds.Width > wideCatalogNav.Bounds.Width);

            window.Width = 600;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.True(compactNav.IsVisible);
            Assert.False(wideLayout.IsVisible);
            Assert.IsType<ButtonPage>(compactNav.CurrentContent);
            Assert.True(compactNav.TryPop());
            Assert.Same(compactRoot, compactNav.CurrentContent);
        }
        finally
        {
            window.Close();
            CupertinoAccessibility.ReduceMotion = oldMotion;
        }
    }

    [AvaloniaTheory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void Reopening_settings_preserves_and_displays_current_preferences(
        bool dark, bool reduceMotion, bool reduceTransparency)
    {
        var app = Application.Current!;
        var oldTheme = app.RequestedThemeVariant;
        var oldMotion = CupertinoAccessibility.ReduceMotion;
        var oldGlass = CupertinoAccessibility.ReduceTransparency;
        var window = new Window { Width = 402, Height = 844 };
        try
        {
            var variant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            app.RequestedThemeVariant = variant;
            CupertinoAccessibility.ReduceMotion = reduceMotion;
            CupertinoAccessibility.ReduceTransparency = reduceTransparency;
            for (var i = 0; i < 2; i++)
            {
                var page = new SettingsPage();
                window.Content = page;
                window.Show();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(variant, app.RequestedThemeVariant);
                Assert.Equal(dark ? 2 : 1, page.FindControl<TabStrip>("AppearanceStrip")!.SelectedIndex);
                Assert.Equal(reduceMotion, CupertinoAccessibility.ReduceMotion);
                Assert.Equal(reduceTransparency, CupertinoAccessibility.ReduceTransparency);
                Assert.Equal(reduceMotion, page.FindControl<ToggleSwitch>("ReduceMotion")!.IsChecked);
                Assert.Equal(reduceTransparency, page.FindControl<ToggleSwitch>("ReduceGlass")!.IsChecked);
                window.Content = null;
            }
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = oldTheme;
            CupertinoAccessibility.ReduceMotion = oldMotion;
            CupertinoAccessibility.ReduceTransparency = oldGlass;
        }
    }

    [AvaloniaFact]
    public void Catalog_search_filters_categories_and_clears_without_navigation()
    {
        var shell = new ShellView();
        var window = new Window { Width = 402, Height = 844, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var nav = shell.FindControl<CupertinoNavigationPage>("Nav")!;
            var root = Assert.IsType<RootPage>(nav.RootContent);
            var count = Entries().Count();
            var filter = root.FindControl<TextBox>("Filter")!;
            filter.Text = "datepicker";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { "CalendarDatePicker", "DatePicker" }, Entries());
            Assert.Equal(0, nav.Depth);
            filter.Text = "no such control";
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(Entries());
            filter.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(count, Entries().Count());
            Assert.Same(root, nav.CurrentContent);

            IEnumerable<string> Entries() => root.ControlEntries.Concat(root.CupertinoEntries)
                .Concat(root.PresentationEntries).Concat(root.ShowcaseEntries).Concat(root.StyleEntries)
                .Select(entry => entry.Title);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Source_view_can_be_detached_and_reattached_with_a_new_theme()
    {
        var page = new SourcePage("<Button Content=\"Example\" />");
        var window = new Window { Width = 402, Height = 844, Content = page };
        try
        {
            window.Show();
            using (var frame = window.CaptureRenderedFrame())
                Assert.NotNull(frame);
            window.Content = null;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.Content = page;
            using (var frame = window.CaptureRenderedFrame())
                Assert.NotNull(frame);
            var editor = Assert.Single(page.GetVisualDescendants().OfType<global::AvaloniaEdit.TextEditor>());
            Assert.Equal("<Button Content=\"Example\" />", editor.Text);
            Assert.True(editor.IsReadOnly);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Settings_controls_expose_descriptive_accessible_names_and_apply_changes()
    {
        var oldTheme = Application.Current!.RequestedThemeVariant;
        var oldMotion = CupertinoAccessibility.ReduceMotion;
        var oldGlass = CupertinoAccessibility.ReduceTransparency;
        var window = new Window { Width = 402, Height = 844 };
        try
        {
            var page = new SettingsPage();
            window.Content = page;
            window.Show();
            var motion = page.FindControl<ToggleSwitch>("ReduceMotion")!;
            var glass = page.FindControl<ToggleSwitch>("ReduceGlass")!;
            Assert.Equal("Reduce motion", ControlAutomationPeer.CreatePeerForElement(motion)!.GetName());
            Assert.Equal("Reduce transparency", ControlAutomationPeer.CreatePeerForElement(glass)!.GetName());
            motion.IsChecked = !oldMotion;
            glass.IsChecked = !oldGlass;
            Assert.Equal(!oldMotion, CupertinoAccessibility.ReduceMotion);
            Assert.Equal(!oldGlass, CupertinoAccessibility.ReduceTransparency);
            page.FindControl<TabStrip>("AppearanceStrip")!.SelectedIndex = 2;
            Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
            var direction = page.FindControl<TabStrip>("DirectionStrip")!;
            direction.SelectedIndex = 2;
            Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
            var reopened = new SettingsPage();
            window.Content = reopened;
            Assert.Equal(2, reopened.FindControl<TabStrip>("DirectionStrip")!.SelectedIndex);
            reopened.FindControl<TabStrip>("DirectionStrip")!.SelectedIndex = 0;
        }
        finally
        {
            window.Close();
            Application.Current.RequestedThemeVariant = oldTheme;
            CupertinoAccessibility.ReduceMotion = oldMotion;
            CupertinoAccessibility.ReduceTransparency = oldGlass;
        }
    }

    private static void Capture(Window window, string page, bool dark, int width, bool rtl, string position)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(width, frame.PixelSize.Width);
        Assert.True(frame.PixelSize.Height > 0);
        var directory = Environment.GetEnvironmentVariable("CUPERTINO_GALLERY_CAPTURES");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory,
                $"{page}-{(dark ? "dark" : "light")}-{width}-{(rtl ? "rtl" : "ltr")}-{position}.png"),
                global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
    }

    private sealed class BindingDiagnostics : ILogSink
    {
        public List<string> Messages { get; } = new();
        public bool IsEnabled(LogEventLevel level, string area) =>
            level >= LogEventLevel.Warning && area == LogArea.Binding;
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
            Log(level, area, source, messageTemplate, Array.Empty<object?>());
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate,
            params object?[] propertyValues)
        {
            if (IsEnabled(level, area))
                Messages.Add(messageTemplate + " " + string.Join(", ", propertyValues));
        }
    }
}
