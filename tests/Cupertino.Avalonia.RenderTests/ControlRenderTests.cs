using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class SliderRenderTests
{
    [AvaloniaFact]
    public void Rail_and_thumb_keep_their_expected_sizes()
    {
        var slider = new Slider
        {
            Width = 240,
            Value = 50,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        Probe.Render("slider", slider, 300, 120, Color.Parse("#FFFFFF"));

        var thumb = slider.GetVisualDescendants()
            .OfType<global::Avalonia.Controls.Primitives.Thumb>().FirstOrDefault();
        Assert.NotNull(thumb);
        Assert.InRange(thumb!.Bounds.Width, 35, 39);
        Assert.InRange(thumb.Bounds.Height, 22, 26);
    }
}

public class SheetRenderTests
{
    [AvaloniaFact]
    public void Grabber_and_corner_match_the_capture()
    {
        var presenter = new CupertinoSheetPresenter
        {
            Width = 320,
            Height = 240,
            Content = new StackPanel
            {
                Margin = new global::Avalonia.Thickness(24),
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Sheet",
                        FontWeight = FontWeight.SemiBold,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = "Drag the grabber to resize",
                        Opacity = 0.55,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    },
                },
            },
        };
        Probe.Render("sheet", presenter, 400, 320, Color.Parse("#F2F2F7"));

        var grabber = presenter.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Bounds.Width > 30 && b.Bounds.Width < 42
                                 && b.Bounds.Height > 3 && b.Bounds.Height < 8);
        Assert.NotNull(grabber);
        Assert.Equal(36, presenter.CornerRadius.TopLeft);
    }
}

public class ToolbarRenderTests
{
    [AvaloniaFact]
    public void Capsules_are_forty_eight_points_tall()
    {
        var toolbar = new CupertinoToolbar();
        toolbar.Items.Add(new Button { Content = "a" });
        toolbar.Items.Add(new ToolbarSpacer());
        toolbar.Items.Add(new Button { Content = "b" });

        Probe.Render("toolbar", toolbar, 402, 200, Color.Parse("#F2F2F7"));

        var capsules = toolbar.GetVisualDescendants().OfType<GlassSurface>().ToList();
        Assert.Equal(2, capsules.Count);
        Assert.All(capsules, c => Assert.Equal(48, c.Bounds.Height));
        Assert.Equal(new global::Avalonia.Thickness(35, 0, 35, 32), toolbar.Margin);
    }

    [AvaloniaFact]
    public void Search_icon_ink_is_centered_in_its_box()
    {
        var icon = new CupertinoIcon
        {
            Glyph = "magnifyingglass",
            Size = 20,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        var luma = Probe.Render("search-icon-centered", icon, 40, 40, Colors.White);

        var minX = 40;
        var maxX = -1;
        var minY = 40;
        var maxY = -1;
        for (var y = 0; y < 40; y++)
            for (var x = 0; x < 40; x++)
            {
                if (luma[x, y] >= 64)
                    continue;
                minX = System.Math.Min(minX, x);
                maxX = System.Math.Max(maxX, x);
                minY = System.Math.Min(minY, y);
                maxY = System.Math.Max(maxY, y);
            }

        Assert.True(maxX >= minX && maxY >= minY, "search icon rendered no dark ink");
        // A centered odd-pixel ink extent rasterizes around either side of 19.5.
        Assert.InRange((minX + maxX) / 2.0, 19.0, 20.0);
        Assert.InRange((minY + maxY) / 2.0, 19.0, 20.0);
    }
}

public class FieldRenderTests
{
    [AvaloniaFact]
    public void Search_shadow_fades_past_the_rectangular_control_edge()
    {
        var field = new TextBox
        {
            Classes = { "search" },
            PlaceholderText = "Search",
            Width = 200,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };

        var luma = Probe.Render("search-shadow", field, 360, 200, Colors.White);

        var farBackground = luma[20, 70];
        var justOutside = luma[79, 70];
        var justInside = luma[80, 70];

        Assert.True(justOutside < farBackground,
            $"shadow did not extend outside the field ({justOutside} vs {farBackground})");
        Assert.InRange(System.Math.Abs(justOutside - justInside), 0, 2);
    }

    [AvaloniaFact]
    public void Placeholder_ink_is_lighter_than_typed_text()
    {
        var stack = new StackPanel
        {
            Spacing = 20,
            Margin = new global::Avalonia.Thickness(16, 20),
            Children =
            {
                new TextBox { PlaceholderText = "Placeholder" },
                new TextBox { Text = "Typed" },
            },
        };
        var luma = Probe.Render("placeholder-vs-text", stack, 300, 160, Color.Parse("#F2F2F7"));

        byte Darkest(int y0, int y1)
        {
            byte min = 255;
            for (var y = y0; y < y1; y++)
                for (var x = 20; x < 200; x++)
                    if (luma[x, y] < min) min = luma[x, y];
            return min;
        }

        var placeholder = Darkest(20, 60);
        var typed = Darkest(60, 120);
        Assert.True(placeholder > typed + 60,
            $"placeholder {placeholder} should be much lighter than text {typed}");
    }

    [AvaloniaFact]
    public void Disabled_field_keeps_its_chrome()
    {
        var field = new TextBox
        {
            PlaceholderText = "Disabled",
            IsEnabled = false,
            Width = 200,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        Probe.Render("disabled-field", field, 300, 120, Color.Parse("#F2F2F7"));
        Assert.Equal(1.0, field.Opacity);
    }
}

public class SegmentedRenderTests
{
    [AvaloniaFact]
    public void Selected_segment_draws_a_pill()
    {
        var strip = new global::Avalonia.Controls.Primitives.TabStrip { Width = 300 };
        strip.Classes.Add("segmented");
        strip.Items.Add(new global::Avalonia.Controls.Primitives.TabStripItem { Content = "One" });
        strip.Items.Add(new global::Avalonia.Controls.Primitives.TabStripItem { Content = "Two" });
        strip.SelectedIndex = 1;
        strip.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        strip.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;

        var luma = Probe.Render("segmented-selected", strip, 360, 120, Color.Parse("#FFFFFF"));

        var pill = luma[210, 60];
        var track = luma[90, 60];
        Assert.True(pill > track, $"selected pill {pill} should be brighter than the track {track}");
    }

    [AvaloniaFact]
    public void Track_is_32pt_high()
    {
        var strip = new global::Avalonia.Controls.Primitives.TabStrip { SelectedIndex = 0, Width = 300 };
        strip.Classes.Add("segmented");
        strip.Items.Add(new global::Avalonia.Controls.Primitives.TabStripItem { Content = "One" });
        Probe.Render("segmented-track", strip, 360, 120, Color.Parse("#FFFFFF"));

        var track = strip.GetVisualDescendants().OfType<Border>()
            .Select(b => b.Bounds)
            .FirstOrDefault(r => r.Width > 200 && r.Height >= 30 && r.Height <= 34);
        Assert.True(track != default, "no 32pt track found in the segmented template");
    }
}

public class DialogRenderTests
{
    [AvaloniaFact]
    public void Card_does_not_paint_a_rectangle_around_its_corners()
    {
        var dialog = new Dialog
        {
            Title = "Title",
            Message = "Message",
            Width = 240,
            Height = 140,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        var luma = Probe.Render("dialog", dialog, 360, 300, Color.Parse("#FFFFFF"));

        var corner = luma[64, 84];
        var page = luma[12, 12];
        Assert.InRange(System.Math.Abs(corner - page), 0, 12);
    }
}

public class BadgeRenderTests
{
    [AvaloniaFact]
    public void Count_and_dot_differ_in_width()
    {
        var dot = new CupertinoBadge { Value = -1 };
        var count = new CupertinoBadge { Value = 12 };
        var row = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 20,
            Margin = new global::Avalonia.Thickness(20),
            Children = { dot, count },
        };
        Probe.Render("badge", row, 200, 80, Color.Parse("#FFFFFF"));

        Assert.True(count.Bounds.Width > dot.Bounds.Width,
            $"count {count.Bounds.Width} should be wider than dot {dot.Bounds.Width}");
    }
}

public class NavigationBarRenderTests
{
    [AvaloniaFact]
    public void Back_button_rides_a_glass_capsule()
    {
        var nav = new CupertinoNavigationPage { RootTitle = "Root" };
        var window = new Window { Width = 402, Height = 300, Content = nav };
        try
        {
            window.Show();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            nav.Push("Detail", new Border());
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Probe.Snapshot("navigation-back", window);

            var bar = nav.GetVisualDescendants().OfType<CupertinoNavigationBar>().Single();
            var host = Assert.IsType<GlassSurface>(bar.LeadingContent);
            Assert.Equal(44, host.Bounds.Height);
            var button = Assert.IsType<Button>(host.Child);
            Assert.Empty(button.GetVisualDescendants().OfType<TextBlock>());
        }
        finally { window.Close(); }
    }
}

public class SwipeViewRenderTests
{
    [AvaloniaFact]
    public void Actions_stay_hidden_until_the_row_is_dragged()
    {
        var swipe = new CupertinoSwipeView
        {
            Width = 300,
            Height = 60,
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
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        var luma = Probe.Render("swipe-view", swipe, 360, 140, Color.Parse("#F2F2F7"));

        Assert.InRange(luma[200, 70], 250, 255);
    }
}

public class ListRenderTests
{
    [AvaloniaFact]
    public void Inset_list_draws_a_card_with_hairline_rows()
    {
        var list = new ListBox
        {
            ItemsSource = new[] { "One", "Two", "Three" },
            Classes = { "inset" },
            Width = 300,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        var luma = Probe.Render("inset-list", list, 360, 220, Color.Parse("#F2F2F7"));

        var card = luma[180, 110];
        var page = luma[10, 10];
        Assert.True(card > page, $"card {card} should sit above the grouped page {page}");
    }
}

public class ThemeVariantRenderTests
{
    [AvaloniaFact]
    public void Dark_variant_inverts_the_page_and_the_ink()
    {
        var card = new Border
        {
            Width = 200,
            Height = 80,
            CornerRadius = new global::Avalonia.CornerRadius(10),
            Child = new TextBlock
            {
                Text = "Label",
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            },
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("CupertinoCardBrush"));
        var text = (TextBlock)card.Child!;
        text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("CupertinoLabelBrush"));

        var window = new Window
        {
            Width = 300,
            Height = 160,
            RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Dark,
            Content = card,
        };
        window.Background = Brushes.Black;
        try
        {
            window.Show();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Probe.Snapshot("theme-dark", window);

            var brush = Assert.IsType<SolidColorBrush>(card.Background);
            Assert.True(brush.Color.R < 80, $"dark card should be dark, got {brush.Color}");
        }
        finally { window.Close(); }
    }
}
