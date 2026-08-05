using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class PhotosShowcasePage : UserControl
{
    private int _viewerTransitionGeneration;
    private int _searchTransitionGeneration;
    private static readonly string[] Names =
    [
        "glass", "ocean", "forest", "city", "desert", "alps", "kyoto", "lisbon",
        "fjord", "dunes", "reef", "aurora", "meadow", "harbor", "canyon", "mesa",
    ];

    private static readonly object PhotoCacheLock = new();
    private static readonly Dictionary<string, Bitmap> PhotoCache = new();

    static PhotosShowcasePage()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposePhotoCache();
    }

    public PhotosShowcasePage()
    {
        InitializeComponent();

        this.FindControl<CupertinoNavigationBar>("LibraryBar")!.Scroller =
            this.FindControl<ScrollViewer>("LibraryScroller");

        var grid = this.FindControl<UniformGrid>("LibraryGrid")!;
        foreach (var name in Names)
        {
            var tile = new Border
            {
                Margin = new Avalonia.Thickness(0.5),
                Height = 145,
                ClipToBounds = true,
                Child = new Image { Source = Photo(name), Stretch = Stretch.UniformToFill },
            };
            tile.PointerPressed += (_, _) => OpenViewer(name);
            grid.Children.Add(tile);
        }
        this.FindControl<TextBlock>("LibraryCount")!.Text = $"{Names.Length} Photos";

        grid.LayoutUpdated += (_, _) =>
        {
            var side = Math.Floor(grid.Bounds.Width / 3);
            if (side <= 0)
                return;
            foreach (var child in grid.Children)
                if (child is Border cell && Math.Abs(cell.Height - side) > 0.5)
                    cell.Height = side;
        };

        var pinned = this.FindControl<StackPanel>("PinnedRow")!;
        foreach (var (name, label) in new[] { ("meadow", "Favorites"), ("harbor", "Trips") })
        {
            pinned.Children.Add(new Border
            {
                Width = 150,
                Height = 150,
                CornerRadius = new Avalonia.CornerRadius(12),
                ClipToBounds = true,
                Child = new Panel
                {
                    Children =
                    {
                        new Image { Source = Photo(name), Stretch = Stretch.UniformToFill },
                        new Border
                        {
                            Height = 56,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                            Background = new LinearGradientBrush
                            {
                                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                                EndPoint = new Avalonia.RelativePoint(0, 1, Avalonia.RelativeUnit.Relative),
                                GradientStops =
                                {
                                    new GradientStop(Color.Parse("#00000000"), 0),
                                    new GradientStop(Color.Parse("#8C000000"), 1),
                                },
                            },
                        },
                        new TextBlock
                        {
                            Text = label, FontSize = 15,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brushes.White,
                            Margin = new Avalonia.Thickness(12, 0, 0, 10),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                        },
                    },
                },
            });
        }
    }

    private static Bitmap Photo(string name)
    {
        lock (PhotoCacheLock)
        {
            if (PhotoCache.TryGetValue(name, out var bitmap))
                return bitmap;

            using var stream = AssetLoader.Open(
                new Uri($"avares://Cupertino.Gallery/Assets/Photos/{name}.jpg"));
            bitmap = new Bitmap(stream);
            PhotoCache[name] = bitmap;
            return bitmap;
        }
    }

    private static void DisposePhotoCache()
    {
        lock (PhotoCacheLock)
        {
            foreach (var bitmap in PhotoCache.Values)
                bitmap.Dispose();
            PhotoCache.Clear();
        }
    }

    private void OpenViewer(string name)
    {
        _viewerTransitionGeneration++;
        this.FindControl<Image>("ViewerImage")!.Source = Photo(name);
        this.FindControl<TextBlock>("ViewerTitle")!.Text =
            char.ToUpperInvariant(name[0]) + name[1..];
        var viewer = this.FindControl<Panel>("Viewer")!;
        viewer.IsVisible = true;
        viewer.Opacity = 1;
    }

    private void OnCloseViewer(object? sender, RoutedEventArgs e)
    {
        var viewer = this.FindControl<Panel>("Viewer")!;
        var generation = ++_viewerTransitionGeneration;
        viewer.Opacity = 0;
        // Hide after fade-out.
        Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            if (generation == _viewerTransitionGeneration && viewer.Opacity == 0)
                viewer.IsVisible = false;
        },
            TimeSpan.FromMilliseconds(220));
    }

    private static readonly Avalonia.Animation.Transitions MorphTransitions =
    [
        new Avalonia.Animation.DoubleTransition
        {
            Property = WidthProperty,
            Duration = TimeSpan.FromMilliseconds(350),
            Easing = new Cupertino.Animation.CriticallyDampedEasing(),
        },
        new Avalonia.Animation.TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = TimeSpan.FromMilliseconds(350),
            Easing = new Cupertino.Animation.CriticallyDampedEasing(),
        },
    ];

    private void OnOpenSearch(object? sender, RoutedEventArgs e)
    {
        var generation = ++_searchTransitionGeneration;
        var layer = this.FindControl<Panel>("SearchLayer")!;
        layer.IsVisible = true;
        layer.Opacity = 1;
        var box = this.FindControl<TextBox>("SearchBox")!;
        box.Text = string.Empty;
        RefillSearch(string.Empty);

        var dock = this.FindControl<Panel>("SearchDock")!;
        var field = this.FindControl<GlassSurface>("SearchField")!;
        var cancel = this.FindControl<Button>("SearchCancel")!;
        var content = this.FindControl<StackPanel>("SearchFieldContent")!;

        // Wait for layout bounds.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (generation != _searchTransitionGeneration || !layer.IsVisible)
                return;
            var final = Math.Max(140, dock.Bounds.Width - cancel.Bounds.Width - 12);
            if (CupertinoAccessibility.ReduceMotion)
            {
                field.Width = final;
                content.Opacity = 1;
                cancel.Opacity = 1;
                box.Focus();
                return;
            }

            var circle = this.GetVisualDescendants().OfType<GlassSurface>()
                .FirstOrDefault(g => g.Name == "AccessoryHost");
            var start = circle?.TranslatePoint(new Avalonia.Point(0, 0), dock)
                        ?? new Avalonia.Point(dock.Bounds.Width - 52, 0);
            field.Transitions = null;
            field.Width = 52;
            field.RenderTransform = TransformOperations.Parse($"translateX({start.X:F0}px)");

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (generation != _searchTransitionGeneration || !layer.IsVisible)
                    return;
                field.Transitions = MorphTransitions;
                field.Width = final;
                field.RenderTransform = TransformOperations.Parse("translateX(0px)");
                content.Opacity = 1;
                cancel.Opacity = 1;
                box.Focus();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnCloseSearch(object? sender, RoutedEventArgs e)
    {
        var generation = ++_searchTransitionGeneration;
        var layer = this.FindControl<Panel>("SearchLayer")!;
        var field = this.FindControl<GlassSurface>("SearchField")!;
        var dock = this.FindControl<Panel>("SearchDock")!;
        this.FindControl<StackPanel>("SearchFieldContent")!.Opacity = 0;
        this.FindControl<Button>("SearchCancel")!.Opacity = 0;

        if (!CupertinoAccessibility.ReduceMotion)
        {
            var circle = this.GetVisualDescendants().OfType<GlassSurface>()
                .FirstOrDefault(g => g.Name == "AccessoryHost");
            var back = circle?.TranslatePoint(new Avalonia.Point(0, 0), dock)
                       ?? new Avalonia.Point(dock.Bounds.Width - 52, 0);
            field.Width = 52;
            field.RenderTransform = TransformOperations.Parse($"translateX({back.X:F0}px)");
        }

        layer.Opacity = 0;
        Avalonia.Threading.DispatcherTimer.RunOnce(() =>
        {
            if (generation == _searchTransitionGeneration && layer.Opacity == 0)
                layer.IsVisible = false;
        },
            TimeSpan.FromMilliseconds(220));
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) =>
        RefillSearch(this.FindControl<TextBox>("SearchBox")?.Text?.Trim() ?? string.Empty);

    private void RefillSearch(string query)
    {
        var grid = this.FindControl<UniformGrid>("SearchGrid")!;
        grid.Children.Clear();
        var matches = Names
            .Where(n => query.Length == 0 || n.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var name in matches)
        {
            var tile = new Border
            {
                Margin = new Avalonia.Thickness(0.5),
                Height = 133,
                ClipToBounds = true,
                Child = new Image { Source = Photo(name), Stretch = Stretch.UniformToFill },
            };
            tile.PointerPressed += (_, _) => OpenViewer(name);
            grid.Children.Add(tile);
        }
        this.FindControl<TextBlock>("SearchEmpty")!.IsVisible = matches.Count == 0;
    }
}
