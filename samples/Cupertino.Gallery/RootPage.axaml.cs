using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Cupertino.Controls;

namespace Cupertino.Gallery;

public partial class RootPage : UserControl
{
    private readonly IReadOnlyList<CatalogEntry> _all;

    public Avalonia.Collections.AvaloniaList<CatalogEntry> ControlEntries { get; } = new();
    public Avalonia.Collections.AvaloniaList<CatalogEntry> CupertinoEntries { get; } = new();
    public Avalonia.Collections.AvaloniaList<CatalogEntry> PresentationEntries { get; } = new();
    public Avalonia.Collections.AvaloniaList<CatalogEntry> ShowcaseEntries { get; } = new();
    public Avalonia.Collections.AvaloniaList<CatalogEntry> StyleEntries { get; } = new();

    public event EventHandler<CatalogEntry>? EntryChosen;

    public event EventHandler? SettingsChosen;

    private bool _ready;

    public RootPage() : this(Array.Empty<CatalogEntry>())
    {
    }

    public RootPage(IReadOnlyList<CatalogEntry> entries)
    {
        _all = entries;
        DataContext = this;
        InitializeComponent();
        Refill(string.Empty);
        this.FindControl<CupertinoDatePicker>("HighlightDate")!.SelectedDate =
            DateTimeOffset.Now;
        this.FindControl<CupertinoTimePicker>("HighlightTime")!.SelectedTime =
            DateTimeOffset.Now.TimeOfDay;

        var heroVersion = this.FindControl<TextBlock>("HeroVersion")!;
        heroVersion.IsVisible = VersionInfo.Library is not null;
        heroVersion.Text = VersionInfo.Library is { } version ? "Version " + version : null;

        var hero = this.FindControl<Panel>("Hero")!;
        hero.PointerPressed += (_, _) => hero.Opacity = 0.75;
        hero.PointerExited += (_, _) => hero.Opacity = 1;
        hero.PointerReleased += (_, e) =>
        {
            var wasPressed = hero.Opacity < 1;
            hero.Opacity = 1;
            if (wasPressed && new Rect(hero.Bounds.Size).Contains(e.GetPosition(hero)))
                SettingsChosen?.Invoke(this, EventArgs.Empty);
        };

        // Ignore the ListBox's initial selection.
        Loaded += (_, _) => _ready = true;
    }

    private void Refill(string query)
    {
        var matches = query.Length == 0
            ? _all
            : _all.Where(x => x.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                  .ToList();

        // Preserve the lists and their scroll positions.
        ControlEntries.Clear();
        ControlEntries.AddRange(matches.Where(x => x.Category == "Controls")
                                       .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase));
        CupertinoEntries.Clear();
        CupertinoEntries.AddRange(matches.Where(x => x.Category == "Cupertino")
                                         .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase));
        PresentationEntries.Clear();
        PresentationEntries.AddRange(matches.Where(x => x.Category == "Presentation")
                                            .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase));
        ShowcaseEntries.Clear();
        ShowcaseEntries.AddRange(matches.Where(x => x.Category == "Showcases")
                                        .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase));
        StyleEntries.Clear();
        StyleEntries.AddRange(matches.Where(x => x.Category == "Styles")
                                     .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase));

        this.FindControl<Section>("HighlightsSection")!.IsVisible = query.Length == 0;
        this.FindControl<Section>("ControlsSection")!.IsVisible = ControlEntries.Count > 0;
        this.FindControl<Section>("CupertinoSection")!.IsVisible = CupertinoEntries.Count > 0;
        this.FindControl<Section>("PresentationSection")!.IsVisible = PresentationEntries.Count > 0;
        this.FindControl<Section>("ShowcasesSection")!.IsVisible = ShowcaseEntries.Count > 0;
        this.FindControl<Section>("StylesSection")!.IsVisible = StyleEntries.Count > 0;
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e) =>
        Refill(this.FindControl<TextBox>("Filter")?.Text?.Trim() ?? string.Empty);

    private async void OnHighlightDialog(object? sender, RoutedEventArgs e) =>
        await Dialog.ShowAsync(this,
            "Built for iOS",
            "Dialogs use the same typography, spacing, material and action roles as the rest of the theme.",
            new DialogAction("Not now", DialogActionRole.Cancel),
            new DialogAction("Continue", DialogActionRole.Preferred));

    private async void OnHighlightSheet(object? sender, RoutedEventArgs e) =>
        await CupertinoSheet.ShowAsync(this, new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "A native-feeling sheet",
                    FontWeight = FontWeight.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0),
                },
                new TextBlock
                {
                    Text = "Drag the grabber or tap outside to dismiss.",
                    TextAlignment = TextAlignment.Center,
                    Opacity = 0.55,
                },
            },
        });

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.AddedItems.Count == 0 || e.AddedItems[0] is not CatalogEntry entry)
            return;

        EntryChosen?.Invoke(this, entry);
        if (sender is ListBox list)
            list.SelectedItem = null;
    }
}
