using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// One entry in a <see cref="CupertinoNavigationPage"/> stack.
/// </summary>
public sealed class NavigationEntry
{
    internal NavigationEntry(string route, string title, Control content, Control host, object? parameter = null)
    {
        Route = route;
        Title = title;
        Content = content;
        Host = host;
        Parameter = parameter;
    }

    public string Route { get; }
    public string Title { get; }
    public Control Content { get; }
    public object? Parameter { get; }

    /// <summary>
    /// The opaque backing used during page transitions.
    /// </summary>
    internal Control Host { get; }
}

public enum CupertinoNavigationKind
{
    Push,
    Pop,
    PopToRoot,
    PopToRoute,
    Restore,
}

public sealed class CupertinoNavigatingEventArgs : CancelEventArgs
{
    internal CupertinoNavigatingEventArgs(CupertinoNavigationKind kind, NavigationEntry? from, NavigationEntry? to)
    {
        Kind = kind;
        From = from;
        To = to;
    }

    public CupertinoNavigationKind Kind { get; }
    public NavigationEntry? From { get; }
    public NavigationEntry? To { get; }
}

public sealed class CupertinoNavigationCompletedEventArgs : EventArgs
{
    internal CupertinoNavigationCompletedEventArgs(CupertinoNavigationKind kind, NavigationEntry? current)
    {
        Kind = kind;
        Current = current;
    }

    public CupertinoNavigationKind Kind { get; }
    public NavigationEntry? Current { get; }
}

public sealed record CupertinoNavigationStateEntry(string Route, string Title, object? Parameter);
public sealed record CupertinoNavigationState(IReadOnlyList<CupertinoNavigationStateEntry> Entries);

/// <summary>
/// Provides a navigation stack with interactive transitions.
/// </summary>
[TemplatePart("PART_Bar", typeof(CupertinoNavigationBar))]
[TemplatePart("PART_Host", typeof(Panel))]
public class CupertinoNavigationPage : TemplatedControl
{
    public static readonly StyledProperty<Control?> RootContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationPage, Control?>(nameof(RootContent));

    public static readonly StyledProperty<string?> RootTitleProperty =
        AvaloniaProperty.Register<CupertinoNavigationPage, string?>(nameof(RootTitle));

    /// <summary>
    /// Gets or sets the leading content shown at the root.
    /// </summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationPage, object?>(nameof(LeadingContent));

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationPage, object?>(nameof(TrailingContent));

    public Control? RootContent { get => GetValue(RootContentProperty); set => SetValue(RootContentProperty, value); }
    public string? RootTitle { get => GetValue(RootTitleProperty); set => SetValue(RootTitleProperty, value); }
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }

    // The outgoing page travels a third of the incoming page's distance.
    private const double ParallaxFraction = 1.0 / 3;
    private const double ScrimPeak = 0.08;
    private const double ShadowWidth = 24;
    private const double EdgeGrabWidth = 22;
    private const double DragThreshold = 3;
    private const double FlickVelocity = 10;

    /// <summary>
    /// Gets or sets whether edge-swipe navigation is enabled.
    /// </summary>
    public static readonly StyledProperty<bool> IsBackGestureEnabledProperty =
        AvaloniaProperty.Register<CupertinoNavigationPage, bool>(nameof(IsBackGestureEnabled), true);

    public bool IsBackGestureEnabled
    {
        get => GetValue(IsBackGestureEnabledProperty);
        set => SetValue(IsBackGestureEnabledProperty, value);
    }

    private readonly List<NavigationEntry> _stack = new();
    private CupertinoNavigationBar? _bar;
    private Panel? _host;
    private Button? _back;
    private Avalonia.Controls.Shapes.Path? _backChevron;
    private GlassSurface? _backHost;
    private Border? _scrim;
    private Border? _edgeShadow;
    private DispatcherTimer? _driver;
    private Action? _transitionCompletion;
    private bool _dragArmed;
    private bool _dragging;
    private double _dragLastX;
    private double _dragVelocity;
    private double _dragProgress;
    private Control? _dragFront;
    private Control? _dragBehind;
    private Control? _barTitle;
    private bool _titleInverted;
    private double _titleSlide;
    private bool _fadeBackWithDepth;
    private bool _hideBackOnFinish;

    /// <summary>
    /// Gets the number of pushed pages.
    /// </summary>
    public int Depth => _stack.Count;

    public bool CanGoBack => _stack.Count > 0;
    public IReadOnlyList<NavigationEntry> Stack => _stack;
    public NavigationEntry? CurrentEntry => _stack.Count > 0 ? _stack[^1] : null;

    /// <summary>
    /// Gets the current page.
    /// </summary>
    public Control? CurrentContent => _stack.Count > 0 ? _stack[^1].Content : RootContent;

    /// <summary>
    /// Raised after navigation completes.
    /// </summary>
    public event EventHandler? Navigated;
    public event EventHandler<CupertinoNavigatingEventArgs>? Navigating;
    public event EventHandler<CupertinoNavigationCompletedEventArgs>? NavigationCompleted;

    private bool CanNavigate(CupertinoNavigationKind kind, NavigationEntry? from, NavigationEntry? to)
    {
        var args = new CupertinoNavigatingEventArgs(kind, from, to);
        Navigating?.Invoke(this, args);
        return !args.Cancel;
    }

    private void CompleteNavigation(CupertinoNavigationKind kind) =>
        NavigationCompleted?.Invoke(this, new CupertinoNavigationCompletedEventArgs(kind, CurrentEntry));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CompleteActiveTransition();
        CancelBackDrag(animate: false);
        RemoveHostHandlers(_host);
        base.OnApplyTemplate(e);

        _bar = e.NameScope.Find<CupertinoNavigationBar>("PART_Bar");
        _host = e.NameScope.Find<Panel>("PART_Host");

        // The stack owns back-button visibility.
        _backChevron = new Avalonia.Controls.Shapes.Path
        {
            StrokeThickness = 2.4,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Width = 16,
            Height = 16,
            Stretch = Stretch.None,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = TransformOperations.Parse("scale(1.5)"),
        };
        _backChevron.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty,
                     this.GetResourceObservable("CupertinoLabelBrush"));
        UpdateDirectionChrome();

        // Avoid inherited platform button styles.
        _back = new Button
        {
            Content = _backChevron,
            Classes = { "plain" },
            IsVisible = false,
            Padding = new Thickness(12, 0),
            MinWidth = 44,
            Height = 36,
        };
        _backHost = new GlassSurface
        {
            Height = 36,
            CornerRadius = new CornerRadius(18),
            BlurRadius = 18,
            GlassThickness = 0,
            Saturation = 1,
            RefractionStrength = 0,
            ChromaticAberration = 0,
            DepthEffect = 0,
            LightIntensity = 0,
            FresnelStrength = 0,
            Magnification = 1,
            ShadowOpacity = 0.10,
            ShadowBlur = 12,
            ShadowOffset = 2,
            ShadowContactWeight = 0.2,
            Child = _back,
            IsVisible = false,
        };
        _backHost.Bind(GlassSurface.TintProperty, this.GetResourceObservable("CupertinoBarButtonTint"));
        _back.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && _backHost is not null)
                _backHost.IsVisible = _back.IsVisible;
        };
        _back.Click += (_, _) => Pop();

        if (_bar is not null)
        {
            _bar.LeadingContent = _backHost;
            _bar.TrailingContent = TrailingContent;
        }

        AddHostHandlers(_host);

        if (_stack.Count > 0)
            ShowCurrentEntry(_stack[^1]);
        else
            ShowRoot();
    }

    private void AddHostHandlers(Panel? host)
    {
        if (host is null)
            return;
        host.AddHandler(PointerPressedEvent, OnHostPressed, RoutingStrategies.Tunnel);
        host.AddHandler(PointerMovedEvent, OnHostMoved, RoutingStrategies.Tunnel);
        host.AddHandler(PointerReleasedEvent, OnHostReleased, RoutingStrategies.Tunnel);
        host.AddHandler(PointerCaptureLostEvent, OnHostCaptureLost,
                        RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void RemoveHostHandlers(Panel? host)
    {
        if (host is null)
            return;
        host.RemoveHandler(PointerPressedEvent, OnHostPressed);
        host.RemoveHandler(PointerMovedEvent, OnHostMoved);
        host.RemoveHandler(PointerReleasedEvent, OnHostReleased);
        host.RemoveHandler(PointerCaptureLostEvent, OnHostCaptureLost);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RootContentProperty)
        {
            if (_stack.Count == 0)
                ShowRoot();
            else
                _rootHost = RootContent is { } root ? Backed(root) : null;
        }
        else if (change.Property == RootTitleProperty)
        {
            if (_stack.Count == 0)
                ShowRoot();
        }
        else if (change.Property == TrailingContentProperty && _bar is not null)
        {
            _bar.TrailingContent = TrailingContent;
        }
        else if (change.Property == LeadingContentProperty)
        {
            UpdateLeading();
        }
        else if (change.Property == FlowDirectionProperty)
        {
            UpdateDirectionChrome();
        }
    }

    private Control? _rootHost;

    private void ShowRoot()
    {
        if (_host is null)
            return;

        if (RootContent is { } root)
        {
            if (_rootHost is not Border existing || !ReferenceEquals(existing.Child, root))
                _rootHost = Backed(root);
            ShowOnly(_rootHost);
            AttachScroller(root);
        }
        else
        {
            _rootHost = null;
            _host.Children.Clear();
        }

        if (_bar is not null)
        {
            _bar.Title = RootTitle;
            _bar.IsLargeTitle = true;
        }
        if (_back is not null)
            _back.IsVisible = false;
        if (_bar is not null)
            _bar.LeadingContent = _backHost;

        UpdateLeading();
    }

    private void UpdateLeading()
    {
        if (_bar is null)
            return;

        if (_stack.Count == 0 && LeadingContent is { } leading)
        {
            _bar.LeadingContent = leading;
            return;
        }
        _bar.LeadingContent = _backHost;
    }

    // Connect the page scroller to collapsing navigation chrome.
    private void AttachScroller(Control page)
    {
        if (_bar is null)
            return;

        Dispatcher.UIThread.Post(
            () => _bar.Scroller = page.GetSelfAndVisualDescendants()
                                      .OfType<ScrollViewer>().FirstOrDefault(),
            DispatcherPriority.Loaded);
    }

    private Border Backed(Control content)
    {
        var border = new Border { Child = content };
        border.Bind(Border.BackgroundProperty,
                    this.GetResourceObservable("CupertinoGroupedBackgroundBrush"));
        return border;
    }

    private void ShowOnly(Control page)
    {
        if (_host is null)
            return;

        if (page.GetVisualParent() is Panel previous && !ReferenceEquals(previous, _host))
            previous.Children.Remove(page);
        _host.Children.Clear();
        if (!_host.Children.Contains(page))
            _host.Children.Add(page);
    }

    /// <summary>
    /// Pushes a page.
    /// </summary>
    public void Push(string title, Control content) => TryPush(title, title, content);

    /// <summary>
    /// Pushes a route after completing any active transition.
    /// </summary>
    public bool TryPush(string route, string title, Control content, object? parameter = null)
    {
        if (_host is null || _dragging)
            return false;

        // Complete active transitions before mutating the stack.
        CompleteActiveTransition();

        var behind = _host.Children.LastOrDefault();
        var entry = new NavigationEntry(route, title, content, Backed(content), parameter);
        if (!CanNavigate(CupertinoNavigationKind.Push, CurrentEntry, entry))
            return false;
        _stack.Add(entry);

        if (_bar is not null)
        {
            _bar.Title = title;
            _bar.IsLargeTitle = false;
        }
        if (_back is not null)
            _back.IsVisible = true;
        UpdateLeading();

        if (behind is null || CupertinoAccessibility.ReduceMotion)
        {
            if (behind is not null)
                _host.Children.Remove(behind);
            _host.Children.Add(entry.Host);
            CompleteNavigation(CupertinoNavigationKind.Push);
        }
        else
        {
            ResetBarChrome();
            _barTitle = _bar?.InlineTitle;
            _titleInverted = false;
            _titleSlide = DirectionSign * 48;
            _fadeBackWithDepth = _stack.Count == 1;

            AddTransitionChrome(entry.Host, behind);
            SetDepthProgress(entry.Host, behind, 0);
            RunTransition(entry.Host, behind, 0, 1, () =>
            {
                FinishTransition(keep: entry.Host, drop: behind);
                CompleteNavigation(CupertinoNavigationKind.Push);
            });
        }

        AttachScroller(content);
        Navigated?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Pops the top page.
    /// </summary>
    public void Pop() => TryPop();

    public bool TryPop()
    {
        if (_host is null || _dragging)
            return false;

        CompleteActiveTransition();
        if (_stack.Count == 0)
            return false;

        var front = _stack[^1].Host;
        var behind = _stack.Count > 1 ? _stack[^2].Host : _rootHost;
        if (behind is null)
            return false;
        var destination = _stack.Count > 1 ? _stack[^2] : null;
        if (!CanNavigate(CupertinoNavigationKind.Pop, _stack[^1], destination))
            return false;

        var animated = !CupertinoAccessibility.ReduceMotion;
        CommitPopState(deferBackHide: animated);

        if (!animated)
        {
            _host.Children.Remove(front);
            if (!_host.Children.Contains(behind))
                _host.Children.Add(behind);
            behind.RenderTransform = null;
            CompleteNavigation(CupertinoNavigationKind.Pop);
        }
        else
        {
            ResetBarChrome(keepBackDeferral: true);
            _barTitle = _bar?.InlineTitle;
            _titleInverted = true;
            _titleSlide = -DirectionSign * 48;
            _fadeBackWithDepth = _hideBackOnFinish;

            AddTransitionChrome(front, behind);
            SetDepthProgress(front, behind, 1);
            RunTransition(front, behind, 1, 0, () =>
            {
                FinishTransition(keep: behind, drop: front);
                CompleteNavigation(CupertinoNavigationKind.Pop);
            });
        }
        return true;
    }

    /// <summary>
    /// Returns to the root in one transition.
    /// </summary>
    public void PopToRoot() => TryPopToRoot();

    public bool TryPopToRoot()
    {
        if (_host is null || _dragging)
            return false;

        CompleteActiveTransition();
        if (_stack.Count == 0 || _rootHost is not { } root)
            return false;

        if (!CanNavigate(CupertinoNavigationKind.PopToRoot, _stack[^1], null))
            return false;

        var front = _stack[^1].Host;
        _stack.Clear();

        var animated = !CupertinoAccessibility.ReduceMotion;
        if (_bar is not null)
        {
            _bar.Title = RootTitle;
            _bar.IsLargeTitle = true;
        }
        if (_back is not null && !animated)
            _back.IsVisible = false;
        UpdateLeading();
        AttachScroller(RootContent!);
        Navigated?.Invoke(this, EventArgs.Empty);

        if (!animated)
        {
            _host.Children.Remove(front);
            if (!_host.Children.Contains(root))
                _host.Children.Add(root);
            root.RenderTransform = null;
            CompleteNavigation(CupertinoNavigationKind.PopToRoot);
        }
        else
        {
            ResetBarChrome();
            _barTitle = _bar?.InlineTitle;
            _titleInverted = true;
            _titleSlide = -DirectionSign * 48;
            _fadeBackWithDepth = true;
            _hideBackOnFinish = true;

            AddTransitionChrome(front, root);
            SetDepthProgress(front, root, 1);
            RunTransition(front, root, 1, 0, () =>
            {
                FinishTransition(keep: root, drop: front);
                CompleteNavigation(CupertinoNavigationKind.PopToRoot);
            });
        }
        return true;
    }

    /// <summary>
    /// Pops to the most recent matching route.
    /// </summary>
    public bool PopToRoute(string route)
    {
        if (_host is null || _dragging || string.IsNullOrWhiteSpace(route))
            return false;

        CompleteActiveTransition();
        var index = _stack.FindLastIndex(entry => string.Equals(entry.Route, route, StringComparison.Ordinal));
        if (index < 0)
            return false;
        if (index == _stack.Count - 1)
            return true;

        var from = _stack[^1];
        var destination = _stack[index];
        if (!CanNavigate(CupertinoNavigationKind.PopToRoute, from, destination))
            return false;

        ResetBarChrome();
        _stack.RemoveRange(index + 1, _stack.Count - index - 1);
        ShowCurrentEntry(destination);
        Navigated?.Invoke(this, EventArgs.Empty);
        CompleteNavigation(CupertinoNavigationKind.PopToRoute);
        return true;
    }

    public NavigationEntry? FindRoute(string route) =>
        _stack.LastOrDefault(entry => string.Equals(entry.Route, route, StringComparison.Ordinal));

    public CupertinoNavigationState CaptureState() => new(
        _stack.Select(entry => new CupertinoNavigationStateEntry(entry.Route, entry.Title, entry.Parameter)).ToArray());

    /// <summary>
    /// Atomically restores routes through an app-supplied factory.
    /// </summary>
    public bool RestoreState(
        CupertinoNavigationState state,
        Func<CupertinoNavigationStateEntry, Control?> pageFactory)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pageFactory);
        if (_host is null || _dragging)
            return false;

        CompleteActiveTransition();

        var rebuilt = new List<NavigationEntry>(state.Entries.Count);
        foreach (var saved in state.Entries)
        {
            var content = pageFactory(saved);
            if (content is null)
                return false;
            rebuilt.Add(new NavigationEntry(saved.Route, saved.Title, content, Backed(content), saved.Parameter));
        }

        var destination = rebuilt.Count > 0 ? rebuilt[^1] : null;
        if (!CanNavigate(CupertinoNavigationKind.Restore, CurrentEntry, destination))
            return false;

        ResetBarChrome();
        _stack.Clear();
        _stack.AddRange(rebuilt);
        if (destination is not null)
            ShowCurrentEntry(destination);
        else
            ShowRoot();
        Navigated?.Invoke(this, EventArgs.Empty);
        CompleteNavigation(CupertinoNavigationKind.Restore);
        return true;
    }

    private void ShowCurrentEntry(NavigationEntry entry)
    {
        if (_host is null)
            return;
        ShowOnly(entry.Host);
        entry.Host.RenderTransform = null;
        if (_bar is not null)
        {
            _bar.Title = entry.Title;
            _bar.IsLargeTitle = false;
        }
        if (_back is not null)
            _back.IsVisible = true;
        UpdateLeading();
        AttachScroller(entry.Content);
    }

    private void CommitPopState(bool deferBackHide = false)
    {
        _stack.RemoveAt(_stack.Count - 1);

        if (_bar is not null)
        {
            _bar.Title = _stack.Count > 0 ? _stack[^1].Title : RootTitle;
            _bar.IsLargeTitle = _stack.Count == 0;
        }
        if (_back is not null)
        {
            if (_stack.Count > 0)
                _back.IsVisible = true;
            else if (deferBackHide)
                _hideBackOnFinish = true;
            else
                _back.IsVisible = false;
        }
        UpdateLeading();
        AttachScroller(_stack.Count > 0 ? _stack[^1].Content : RootContent!);
        Navigated?.Invoke(this, EventArgs.Empty);
    }

    private double HostWidth => Bounds.Width > 0 ? Bounds.Width : 402;
    // Avalonia mirrors local coordinates in RTL; keep transition math logical.
    private const double DirectionSign = 1;

    private void UpdateDirectionChrome()
    {
        if (_backChevron is not null)
        {
            _backChevron.Data = this.TryFindResource("CupertinoChevronLeftGeometry", out var geometry)
                ? geometry as Geometry : null;
        }

        if (_edgeShadow is not null)
            _edgeShadow.Background = CreateEdgeShadowBrush();
    }

    private static LinearGradientBrush CreateEdgeShadowBrush() => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#00000000"), 0),
            new GradientStop(Color.Parse("#2E000000"), 1),
        },
    };

    private static TransformOperations Translate(double x) =>
        TransformOperations.Parse(
            $"translateX({x.ToString(System.Globalization.CultureInfo.InvariantCulture)}px)");

    // Depth: 0 is revealed; 1 is fully pushed.
    private void SetDepthProgress(Control front, Control behind, double depth)
    {
        var w = HostWidth;
        front.RenderTransform = Translate(DirectionSign * (1 - depth) * w);
        behind.RenderTransform = Translate(-DirectionSign * w * ParallaxFraction * depth);
        if (_scrim is not null)
            _scrim.Opacity = ScrimPeak * depth;
        if (_edgeShadow is not null)
        {
            _edgeShadow.Opacity = Math.Min(1, depth * 3);
            _edgeShadow.RenderTransform = Translate((1 - depth) * w - ShadowWidth);
        }

        if (_barTitle is not null)
        {
            var p = _titleInverted ? 1 - depth : depth;
            _barTitle.Opacity = p;
            _barTitle.RenderTransform = Translate(_titleSlide * (1 - p));
        }
        if (_fadeBackWithDepth && _back is not null)
            _back.Opacity = depth;
    }

    // Clear local values so resting bindings regain ownership.
    private void ResetBarChrome(bool keepBackDeferral = false)
    {
        if (_barTitle is not null)
        {
            _barTitle.ClearValue(OpacityProperty);
            _barTitle.RenderTransform = null;
            _barTitle = null;
        }
        if (_back is not null)
        {
            if (_hideBackOnFinish && !keepBackDeferral)
                _back.IsVisible = false;
            _back.ClearValue(OpacityProperty);
        }
        _fadeBackWithDepth = false;
        if (!keepBackDeferral)
            _hideBackOnFinish = false;
    }

    private void AddTransitionChrome(Control front, Control behind)
    {
        if (_host is null)
            return;

        _scrim ??= new Border { Background = Brushes.Black, Opacity = 0, IsHitTestVisible = false };
        _edgeShadow ??= new Border
        {
            Width = ShadowWidth,
            Opacity = 0,
            IsHitTestVisible = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Background = CreateEdgeShadowBrush(),
        };

        foreach (var c in new[] { _scrim, _edgeShadow, front })
            _host.Children.Remove(c);
        if (!_host.Children.Contains(behind))
            _host.Children.Add(behind);

        var i = _host.Children.IndexOf(behind);
        _host.Children.Insert(i + 1, _scrim);
        _host.Children.Insert(i + 2, _edgeShadow);
        _host.Children.Insert(i + 3, front);
    }

    private void FinishTransition(Control keep, Control drop)
    {
        if (_host is null)
            return;
        if (_scrim is not null)
            _host.Children.Remove(_scrim);
        if (_edgeShadow is not null)
            _host.Children.Remove(_edgeShadow);
        _host.Children.Remove(drop);
        drop.RenderTransform = null;
        keep.RenderTransform = null;
        ResetBarChrome();
    }

    // Animate only the remaining distance after an interactive gesture.
    private void RunTransition(Control front, Control behind, double from, double to, Action onDone)
    {
        CompleteActiveTransition();

        if (CupertinoAccessibility.ReduceMotion)
        {
            SetDepthProgress(front, behind, to);
            onDone();
            return;
        }

        var duration = Math.Max(120, 350 * Math.Abs(to - from));
        var easing = new Animation.CriticallyDampedEasing { OmegaDuration = 8.4 };
        var started = DateTime.UtcNow;

        var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _driver = driver;
        _transitionCompletion = () =>
        {
            SetDepthProgress(front, behind, to);
            onDone();
        };
        driver.Tick += (_, _) =>
        {
            var t = (DateTime.UtcNow - started).TotalMilliseconds / duration;
            if (t >= 1)
            {
                if (ReferenceEquals(_driver, driver))
                    CompleteActiveTransition();
                return;
            }
            SetDepthProgress(front, behind, from + (to - from) * easing.Ease(t));
        };
        driver.Start();
    }

    private void CompleteActiveTransition()
    {
        var completion = _transitionCompletion;
        if (completion is null)
            return;

        _transitionCompletion = null;
        _driver?.Stop();
        _driver = null;
        completion();
    }

    private void OnHostPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsBackGestureEnabled || _host is null || _stack.Count == 0
            || _dragging || _driver?.IsEnabled == true)
            return;

        var x = e.GetPosition(_host).X;
        if (x > EdgeGrabWidth)
            return;

        _dragArmed = true;
        _dragLastX = x;
        _dragVelocity = 0;
    }

    private void OnHostMoved(object? sender, PointerEventArgs e)
    {
        if (_host is null || (!_dragArmed && !_dragging))
            return;

        var x = e.GetPosition(_host).X;
        var dx = x - _dragLastX;
        _dragLastX = x;

        if (!_dragging)
        {
            if (x < DragThreshold)
                return;
            BeginBackDrag();
            if (!_dragging)
                return;
        }

        _dragVelocity = _dragVelocity * 0.7 + dx * 0.3;
        _dragProgress = Math.Clamp(1 - x / HostWidth, 0, 1);
        SetDepthProgress(_dragFront!, _dragBehind!, _dragProgress);
        e.Pointer.Capture(_host);
        e.Handled = true;
    }

    private void BeginBackDrag()
    {
        _dragArmed = false;
        var behind = _stack.Count > 1 ? _stack[^2].Host : _rootHost;
        if (behind is null)
            return;

        _dragFront = _stack[^1].Host;
        _dragBehind = behind;
        _dragging = true;

        ResetBarChrome();
        _barTitle = _bar?.InlineTitle;
        _titleInverted = false;
        _titleSlide = DirectionSign * 48;
        _fadeBackWithDepth = _stack.Count == 1;

        AddTransitionChrome(_dragFront, behind);
        SetDepthProgress(_dragFront, behind, 1);
    }

    private void OnHostReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragArmed = false;
        if (!_dragging || _dragFront is null || _dragBehind is null)
            return;
        _dragging = false;

        var front = _dragFront;
        var behind = _dragBehind;
        _dragFront = null;
        _dragBehind = null;

        var wantsPop = _dragProgress < 0.5 || _dragVelocity > FlickVelocity;
        var destination = _stack.Count > 1 ? _stack[^2] : null;
        if (wantsPop && CanNavigate(CupertinoNavigationKind.Pop, _stack[^1], destination))
        {
            RunTransition(front, behind, _dragProgress, 0, () =>
            {
                CommitPopState(deferBackHide: true);
                FinishTransition(keep: behind, drop: front);
                CompleteNavigation(CupertinoNavigationKind.Pop);
            });
        }
        else
        {
            RunTransition(front, behind, _dragProgress, 1, () =>
            {
                if (_host is null)
                    return;
                if (_scrim is not null)
                    _host.Children.Remove(_scrim);
                if (_edgeShadow is not null)
                    _host.Children.Remove(_edgeShadow);
                _host.Children.Remove(behind);
                behind.RenderTransform = null;
                front.RenderTransform = null;
                ResetBarChrome();
            });
        }
        e.Handled = true;
    }

    private void OnHostCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragArmed && !_dragging)
        {
            _dragArmed = false;
            return;
        }

        CancelBackDrag(animate: true);
    }

    private void CancelBackDrag(bool animate)
    {
        _dragArmed = false;
        if (!_dragging || _dragFront is null || _dragBehind is null)
            return;

        _dragging = false;
        var front = _dragFront;
        var behind = _dragBehind;
        _dragFront = null;
        _dragBehind = null;

        void Cleanup()
        {
            if (_host is null)
                return;
            if (_scrim is not null)
                _host.Children.Remove(_scrim);
            if (_edgeShadow is not null)
                _host.Children.Remove(_edgeShadow);
            _host.Children.Remove(behind);
            behind.RenderTransform = null;
            front.RenderTransform = null;
            ResetBarChrome();
        }

        if (animate && this.IsAttachedToVisualTree() && !CupertinoAccessibility.ReduceMotion)
            RunTransition(front, behind, _dragProgress, 1, Cleanup);
        else
        {
            SetDepthProgress(front, behind, 1);
            Cleanup();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CompleteActiveTransition();
        CancelBackDrag(animate: false);
        base.OnDetachedFromVisualTree(e);
    }
}
