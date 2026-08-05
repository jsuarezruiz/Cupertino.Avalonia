using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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

        this.FindControl<TextBlock>("HeroVersion")!.Text =
            "Version " + Cupertino.Themes.CupertinoTheme.Version;

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

        this.FindControl<Section>("ControlsSection")!.IsVisible = ControlEntries.Count > 0;
        this.FindControl<Section>("CupertinoSection")!.IsVisible = CupertinoEntries.Count > 0;
        this.FindControl<Section>("PresentationSection")!.IsVisible = PresentationEntries.Count > 0;
        this.FindControl<Section>("ShowcasesSection")!.IsVisible = ShowcaseEntries.Count > 0;
        this.FindControl<Section>("StylesSection")!.IsVisible = StyleEntries.Count > 0;
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e) =>
        Refill(this.FindControl<TextBox>("Filter")?.Text?.Trim() ?? string.Empty);

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.AddedItems.Count == 0 || e.AddedItems[0] is not CatalogEntry entry)
            return;

        EntryChosen?.Invoke(this, entry);
        if (sender is ListBox list)
            list.SelectedItem = null;
    }
}
