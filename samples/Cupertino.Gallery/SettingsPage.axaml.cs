using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class SettingsPage : UserControl
{
    private bool _syncingPreferences = true;
    private static int s_directionPreference;
    private static readonly (string Name, Avalonia.Media.Color? Color)[] Accents =
    [
        ("Blue", null),
        ("Orange", Avalonia.Media.Color.Parse("#FFFF9500")),
        ("Green", Avalonia.Media.Color.Parse("#FF34C759")),
        ("Purple", Avalonia.Media.Color.Parse("#FFAF52DE")),
        ("Pink", Avalonia.Media.Color.Parse("#FFFF2D55")),
        ("Red", Avalonia.Media.Color.Parse("#FFFF3B30")),
    ];

    public SettingsPage()
    {
        InitializeComponent();

        if (Application.Current is { } app)
        {
            var strip = this.FindControl<TabStrip>("AppearanceStrip")!;
            strip.SelectedIndex = app.RequestedThemeVariant == ThemeVariant.Light ? 1
                : app.RequestedThemeVariant == ThemeVariant.Dark ? 2
                : 0;
        }
        this.FindControl<TabStrip>("DirectionStrip")!.SelectedIndex = s_directionPreference;
        this.FindControl<ToggleSwitch>("ReduceGlass")!.IsChecked = CupertinoAccessibility.ReduceTransparency;
        this.FindControl<ToggleSwitch>("ReduceMotion")!.IsChecked = CupertinoAccessibility.ReduceMotion;
        _syncingPreferences = false;

        var row = this.FindControl<StackPanel>("AccentRow")!;
        var theme = Application.Current?.Styles.OfType<Cupertino.Themes.CupertinoTheme>()
            .FirstOrDefault();
        var rings = new List<(Border Ring, Avalonia.Media.Color? Color)>();

        void Select(Avalonia.Media.Color? selected)
        {
            foreach (var (ring, ringColor) in rings)
                ring.BorderBrush = ringColor == selected
                    ? new Avalonia.Media.SolidColorBrush(
                        ringColor ?? Avalonia.Media.Color.Parse("#FF007AFF"))
                    : Avalonia.Media.Brushes.Transparent;
        }

        foreach (var (name, color) in Accents)
        {
            var ring = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(21),
                BorderThickness = new Thickness(2),
                BorderBrush = Avalonia.Media.Brushes.Transparent,
                Child = new Avalonia.Controls.Shapes.Ellipse
                {
                    Width = 30,
                    Height = 30,
                    Fill = new Avalonia.Media.SolidColorBrush(
                        color ?? Avalonia.Media.Color.Parse("#FF007AFF")),
                },
            };
            rings.Add((ring, color));

            var dot = new Button { Padding = new Thickness(0), MinHeight = 0, Content = ring };
            dot.Classes.Add("plain");
            ToolTip.SetTip(dot, name);
            Avalonia.Automation.AutomationProperties.SetName(dot, name);
            dot.Click += (_, _) =>
            {
                if (theme is not null)
                    theme.Accent = color;
                Select(color);
            };
            row.Children.Add(dot);
        }
        Select(theme?.Accent);

        var libraryVersion = VersionInfo.Library;
        this.FindControl<Grid>("VersionRow")!.IsVisible = libraryVersion is not null;
        this.FindControl<global::Avalonia.Controls.Shapes.Rectangle>("VersionBottomSeparator")!.IsVisible = libraryVersion is not null;
        this.FindControl<TextBlock>("LibVersion")!.Text = libraryVersion;
        this.FindControl<TextBlock>("AvaloniaVersion")!.Text = VersionInfo.Avalonia;
        this.FindControl<TextBlock>("RuntimeVersion")!.Text = Environment.Version.ToString();
    }

    private void OnAppearanceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingPreferences || Application.Current is not { } app || sender is not TabStrip strip)
            return;

        app.RequestedThemeVariant = strip.SelectedIndex switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void OnReduceGlassChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncingPreferences)
            CupertinoAccessibility.ReduceTransparency =
                this.FindControl<ToggleSwitch>("ReduceGlass")?.IsChecked == true;
    }

    private void OnReduceMotionChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncingPreferences)
            CupertinoAccessibility.ReduceMotion =
                this.FindControl<ToggleSwitch>("ReduceMotion")?.IsChecked == true;
    }

    private void OnDirectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingPreferences || sender is not TabStrip strip)
            return;
        s_directionPreference = Math.Clamp(strip.SelectedIndex, 0, 2);
        var direction = s_directionPreference switch
        {
            1 => FlowDirection.LeftToRight,
            2 => FlowDirection.RightToLeft,
            _ => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight,
        };

        if (TopLevel.GetTopLevel(this) is { } topLevel)
            topLevel.FlowDirection = direction;
    }
}
