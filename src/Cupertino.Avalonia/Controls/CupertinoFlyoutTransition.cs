using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// Attaches the Cupertino opening and closing motion to supported flyout presenters.
/// </summary>
public static class CupertinoFlyoutTransition
{
    internal enum MotionProfile
    {
        Standard,
        Menu,
        Popover,
    }

    private sealed class PropertyOwner : AvaloniaObject
    {
    }

    // Class names the templates use to mark the parts the opening motion animates.
    private const string PopoverClass = "cupertino-popover";
    private const string MaterialClass = "cupertino-source-transition-material";
    private const string ContentClass = "cupertino-source-transition-content";

    private static readonly TransformOperations ExpandedTransform =
        TransformOperations.Parse("scale(1,1) translate(0px,0px)");

    private static readonly BoxShadows CompactBorderShadow = new(new BoxShadow
    {
        Blur = 8,
        OffsetY = 2,
        Color = Colors.Transparent,
    });

    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<PropertyOwner, AvaloniaObject, bool>("IsEnabled");

    /// <summary>
    /// Identifies the requested open state used by a raw <see cref="Popup"/> that
    /// needs a cancellable closing transition.
    /// </summary>
    public static readonly AttachedProperty<bool> IsOpenProperty =
        AvaloniaProperty.RegisterAttached<PropertyOwner, Popup, bool>(
            "IsOpen", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private static readonly ConditionalWeakTable<PopupFlyoutBase, FlyoutState> Flyouts = new();
    private static readonly ConditionalWeakTable<ContextMenu, ContextMenuState> ContextMenus = new();
    private static readonly ConditionalWeakTable<Popup, PopupState> Popups = new();
    private static readonly ConditionalWeakTable<TopLevel, ActiveSession> ActiveSessions = new();

    private static bool _initialized;

    static CupertinoFlyoutTransition()
    {
        IsEnabledProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<bool>>(OnIsEnabledChanged));
        IsOpenProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<bool>>(OnIsOpenChanged));
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(AvaloniaObject control) => control.GetValue(IsEnabledProperty);

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(AvaloniaObject control, bool value) =>
        control.SetValue(IsEnabledProperty, value);

    /// <inheritdoc cref="IsOpenProperty"/>
    public static bool GetIsOpen(Popup popup) => popup.GetValue(IsOpenProperty);

    /// <inheritdoc cref="IsOpenProperty"/>
    public static void SetIsOpen(Popup popup, bool value) => popup.SetValue(IsOpenProperty, value);

    internal static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        FlyoutBase.TargetProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<Control?>>(change =>
            {
                if (change.Sender is PopupFlyoutBase flyout && change.NewValue.Value is not null)
                    Attach(flyout);
            }));
        Control.ContextMenuProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<ContextMenu?>>(change =>
            {
                if (change.NewValue.Value is ContextMenu menu)
                    Attach(menu);
            }));
    }

    private static void OnIsEnabledChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (!change.GetNewValue<bool>())
            return;

        if (change.Sender is ContextMenu menu)
            Attach(menu);
        else if (change.Sender is Popup popup)
        {
            Attach(popup);
            if (GetIsOpen(popup) && Popups.TryGetValue(popup, out var state))
                state.SetOpen(true);
        }
    }

    private static void OnIsOpenChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Sender is not Popup popup || !GetIsEnabled(popup))
            return;

        Attach(popup);
        if (Popups.TryGetValue(popup, out var state))
            state.SetOpen(change.GetNewValue<bool>());
    }

    private static void Attach(PopupFlyoutBase flyout)
    {
        if (Flyouts.TryGetValue(flyout, out _))
            return;

        var state = new FlyoutState(flyout);
        Flyouts.Add(flyout, state);
        flyout.Opening += state.Opening;
        flyout.Opened += state.Opened;
        flyout.Closing += state.Closing;
        flyout.Closed += state.Closed;
    }

    private static void Attach(ContextMenu menu)
    {
        if (ContextMenus.TryGetValue(menu, out _))
            return;

        var state = new ContextMenuState(menu);
        ContextMenus.Add(menu, state);
        menu.Opening += state.Opening;
        menu.Opened += state.Opened;
        menu.Closing += state.Closing;
        menu.Closed += state.Closed;
    }

    private static void Attach(Popup popup)
    {
        if (Popups.TryGetValue(popup, out _))
            return;

        var state = new PopupState(popup);
        Popups.Add(popup, state);
        popup.Opened += state.Opened;
        popup.Closed += state.Closed;
    }

    private static TransitionSession? CreateSession(
        Control anchor,
        Control presenter,
        PlacementMode placement,
        Action closeOwner)
    {
        if (TopLevel.GetTopLevel(anchor) is not { } root)
            return null;

        presenter.UpdateLayout();
        var panel = FindPopoverPanel(presenter);
        var material = panel.GetVisualDescendants()
                           .OfType<Control>()
                           .FirstOrDefault(control =>
                               control.Classes.Contains(MaterialClass))
                       ?? panel;
        var glass = material as GlassSurface;
        var content = FindContent(panel, glass);
        var origin = panel.TranslatePoint(default, root);
        if (origin is not { } point || panel.Bounds.Width <= 0 || panel.Bounds.Height <= 0)
            return null;

        var targetBounds = new Rect(point, panel.Bounds.Size);
        var anchorBounds = GetAnchorBounds(anchor, root, targetBounds, placement);
        return StartSession(
            root, panel, material, content, glass,
            anchorBounds, targetBounds, closeOwner,
            IsTopLevelMenuItem(anchor) ? MotionProfile.Menu : MotionProfile.Standard);
    }

    internal static TransitionSession? CreateSession(
        Control anchor,
        Control panel,
        Control material,
        Control content,
        Rect targetBounds,
        Action closeOwner)
    {
        if (TopLevel.GetTopLevel(anchor) is not { } root
            || anchor.TranslatePoint(default, root) is not { } point)
            return null;

        if (panel.TranslatePoint(default, root) is { } targetPoint
            && panel.Bounds.Width > 0
            && panel.Bounds.Height > 0)
            targetBounds = new Rect(targetPoint, panel.Bounds.Size);

        return StartSession(
            root, panel, material, content, material as GlassSurface,
            new Rect(point, anchor.Bounds.Size), targetBounds, closeOwner,
            MotionProfile.Popover);
    }

    private static TransitionSession StartSession(
        TopLevel root,
        Control panel,
        Control material,
        Control content,
        GlassSurface? glass,
        Rect anchorBounds,
        Rect targetBounds,
        Action closeOwner,
        MotionProfile profile)
    {
        var collapsedOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        var collapsedTransform = CreateCollapsedTransform(anchorBounds, targetBounds);
        var active = ActiveSessions.GetOrCreateValue(root);
        active.Session?.FinishForReplacement();

        var session = new TransitionSession(
            active, root, panel, material, content, glass,
            collapsedTransform, collapsedOrigin, closeOwner, profile);
        active.Session = session;
        session.Open();
        return session;
    }

    private static bool IsTopLevelMenuItem(Control anchor) =>
        anchor is MenuItem { Parent: Menu };

    private static void ConfigureTopLevelMenuPopup(Popup popup, Control anchor)
    {
        if (!IsTopLevelMenuItem(anchor))
            return;

        // UIKit's primary-action menu expands around the source instead of
        // opening from an edge. Keep submenus on their normal edge placement.
        popup.Placement = PlacementMode.Center;
        popup.VerticalOffset = 77.5;
        if (popup.Child is Panel { Children.Count: > 0 } presenter
            && presenter.Children[0] is Grid layout
            && layout.MinWidth < 248)
            layout.MinWidth = 248;

        if (anchor is not MenuItem menu)
            return;

        foreach (var entry in menu.Items)
        {
            if (entry is MenuItem item
                && !item.Classes.Contains("cupertino-primary-menu-action"))
                item.Classes.Add("cupertino-primary-menu-action");
            else if (entry is Separator separator
                     && !separator.Classes.Contains("cupertino-primary-menu-separator"))
                separator.Classes.Add("cupertino-primary-menu-separator");
        }
    }

    private static TransformOperations CreateCollapsedTransform(
        Rect sourceBounds,
        Rect targetBounds)
    {
        var scaleX = targetBounds.Width > 0 && sourceBounds.Width > 0
            ? Math.Clamp(sourceBounds.Width / targetBounds.Width, 0.08, 1)
            : 0.18;
        var scaleY = targetBounds.Height > 0 && sourceBounds.Height > 0
            ? Math.Clamp(sourceBounds.Height / targetBounds.Height, 0.08, 1)
            : 0.18;
        var offset = sourceBounds.Center - targetBounds.Center;
        var value = string.Create(CultureInfo.InvariantCulture,
            $"scale({scaleX:0.######},{scaleY:0.######}) translate({offset.X:0.######}px,{offset.Y:0.######}px)");
        return TransformOperations.Parse(value);
    }

    private static Control FindPopoverPanel(Control presenter) =>
        presenter.Classes.Contains(PopoverClass)
            ? presenter
            : presenter.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => control.Classes.Contains(PopoverClass))
              ?? presenter;

    private static Control FindContent(Control panel, GlassSurface? glass)
    {
        if (glass?.Child is Control child)
            return child;

        return panel.GetVisualDescendants()
                   .OfType<Control>()
                   .FirstOrDefault(control => control.Classes.Contains(ContentClass))
               ?? panel;
    }

    private static Rect GetAnchorBounds(
        Control anchor,
        Visual root,
        Rect targetBounds,
        PlacementMode placement)
    {
        if (placement == PlacementMode.Pointer)
            return new Rect(targetBounds.X - 20, targetBounds.Y - 12, 4, 4);

        var origin = anchor.TranslatePoint(default, root);
        return origin is { } point
            ? new Rect(point, anchor.Bounds.Size)
            : new Rect(targetBounds.Center, default(Size));
    }

    // Transform and opacity always animate. Glass material scalars (blur,
    // saturation, refraction, shadow) intentionally do NOT animate: each tick
    // would change the blur sigma and offscreen size, defeating the filter
    // cache and reallocating the backdrop surface every frame (2 FPS on WebGL).
    // The grow is carried by RenderTransform + Opacity, which compose on the GPU.
    private static Transitions CreateTransitions(
        Control target,
        TimeSpan duration,
        TimeSpan opacityDuration,
        Easing transformEasing,
        Easing opacityEasing)
    {
        var transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = duration,
                Easing = transformEasing,
            },
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = opacityDuration,
                Easing = opacityEasing,
            },
        };
        if (target is Border)
        {
            transitions.Add(new BoxShadowsTransition
            {
                Property = Border.BoxShadowProperty,
                Duration = duration,
                Easing = transformEasing,
            });
        }
        return transitions;
    }

    private sealed class FlyoutState(PopupFlyoutBase owner)
    {
        private TransitionSession? _session;
        private Control? _preparedPresenter;
        private double _preparedOpacity;
        private bool _completing;
        private Control? _watchedAnchor;

        public void Opening(object? sender, EventArgs args)
        {
            owner.Popup.ShouldUseOverlayLayer = true;
            if (CupertinoAccessibility.ReduceMotion || owner.Popup.Child is not { } presenter)
                return;

            if (!ReferenceEquals(_preparedPresenter, presenter))
            {
                RestorePreparedPresenter();
                _preparedPresenter = presenter;
                _preparedOpacity = presenter.Opacity;
            }
            presenter.Opacity = 0;
        }

        public void Opened(object? sender, EventArgs args)
        {
            _session?.Dispose();
            _session = owner.Target is { } anchor && owner.Popup.Child is { } presenter
                ? CreateSession(anchor, presenter, owner.Popup.Placement, CompleteClose)
                : null;
            UnwatchAnchor();
            if (owner.Target is { } watched)
            {
                _watchedAnchor = watched;
                watched.DetachedFromVisualTree += OnAnchorDetached;
            }
            RestorePreparedPresenter();
        }

        public void Closing(object? sender, CancelEventArgs args)
        {
            if (_completing || CupertinoAccessibility.ReduceMotion || _session is null)
                return;

            args.Cancel = true;
            _session.Close();
        }

        public void Closed(object? sender, EventArgs args)
        {
            UnwatchAnchor();
            RestorePreparedPresenter();
            _session?.Dispose();
            _session = null;
            _completing = false;
        }

        private void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs args) =>
            CompleteClose();

        private void UnwatchAnchor()
        {
            if (_watchedAnchor is { } anchor)
                anchor.DetachedFromVisualTree -= OnAnchorDetached;
            _watchedAnchor = null;
        }

        private void CompleteClose()
        {
            _completing = true;
            owner.Hide();
            _completing = false;
        }

        private void RestorePreparedPresenter()
        {
            if (_preparedPresenter is not { } presenter)
                return;

            presenter.Opacity = _preparedOpacity;
            _preparedPresenter = null;
        }
    }

    private sealed class ContextMenuState(ContextMenu owner)
    {
        private static WeakReference<ContextMenuState>? s_active;

        private TransitionSession? _session;
        private double _preparedOpacity;
        private bool _prepared;
        private bool _completing;
        private Control? _watchedAnchor;
        private Popup? _popup;
        private Border? _scrim;
        private OverlayLayer? _scrimLayer;
        private IDisposable? _scrimSize;

        public void Opening(object? sender, CancelEventArgs args)
        {
            if (args.Cancel || CupertinoAccessibility.ReduceMotion)
                return;

            // A cancelled opening never raises Opened, so keep the opacity saved before it.
            if (!_prepared)
                _preparedOpacity = owner.Opacity;
            _prepared = true;
            owner.Opacity = 0;
        }

        public void Opened(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
        {
            if (s_active?.TryGetTarget(out var active) == true
                && !ReferenceEquals(active, this))
                active.CompleteClose();
            s_active = new WeakReference<ContextMenuState>(this);

            _session?.Dispose();
            UnwatchAnchor();
            if (owner.Parent is Popup popup && popup.PlacementTarget is { } anchor)
            {
                _popup = popup;
                _session = CreateSession(anchor, owner, popup.Placement, CompleteClose);
                _watchedAnchor = anchor;
                anchor.DetachedFromVisualTree += OnAnchorDetached;
                ShowScrim(anchor);
            }
            RestorePreparedOpacity();
        }

        public void Closing(object? sender, CancelEventArgs args)
        {
            if (_scrim is { } scrim)
                scrim.Opacity = 0;
            if (_completing || CupertinoAccessibility.ReduceMotion || _session is null)
                return;

            args.Cancel = true;
            _session.Close();
        }

        public void Closed(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
        {
            if (s_active?.TryGetTarget(out var active) == true
                && ReferenceEquals(active, this))
                s_active = null;
            UnwatchAnchor();
            RemoveScrim();
            RestorePreparedOpacity();
            _session?.Dispose();
            _session = null;
            _popup = null;
            _completing = false;
        }

        private void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs args) =>
            CompleteClose();

        private void UnwatchAnchor()
        {
            if (_watchedAnchor is { } anchor)
                anchor.DetachedFromVisualTree -= OnAnchorDetached;
            _watchedAnchor = null;
        }

        private void ShowScrim(Control anchor)
        {
            RemoveScrim();
            if (OverlayLayer.GetOverlayLayer(anchor) is not { } layer)
                return;

            var scrim = new Border
            {
                Opacity = 0,
                IsHitTestVisible = false,
                Width = layer.Bounds.Width,
                Height = layer.Bounds.Height,
            };
            scrim.Bind(Border.BackgroundProperty,
                scrim.GetResourceObservable("CupertinoContextMenuScrimBrush"));
            _scrimSize = layer.GetObservable(Visual.BoundsProperty).Subscribe(
                new AnonymousObserver<Rect>(bounds =>
                {
                    scrim.Width = bounds.Width;
                    scrim.Height = bounds.Height;
                }));
            if (!CupertinoAccessibility.ReduceMotion)
                scrim.Transitions =
                [
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(250),
                    },
                ];
            layer.Children.Insert(Math.Max(0, layer.Children.Count - 1), scrim);
            _scrim = scrim;
            _scrimLayer = layer;
            DispatcherTimer.RunOnce(() =>
            {
                if (ReferenceEquals(_scrim, scrim))
                    scrim.Opacity = 1;
            }, TimeSpan.FromMilliseconds(16));
        }

        private void RemoveScrim()
        {
            _scrimSize?.Dispose();
            _scrimSize = null;
            if (_scrim is { } scrim)
                _scrimLayer?.Children.Remove(scrim);
            _scrim = null;
            _scrimLayer = null;
        }

        private void CompleteClose()
        {
            _completing = true;
            if (_popup is { } popup)
                popup.Close();
            else
                owner.Close();
            _completing = false;
        }

        private void RestorePreparedOpacity()
        {
            if (!_prepared)
                return;

            owner.Opacity = _preparedOpacity;
            _prepared = false;
        }
    }

    private sealed class PopupState(Popup owner)
    {
        private TransitionSession? _session;
        private Control? _watchedAnchor;
        private bool _completing;

        public void SetOpen(bool value)
        {
            if (value)
            {
                if ((owner.PlacementTarget ?? owner.TemplatedParent as Control) is { } sourceAnchor)
                    ConfigureTopLevelMenuPopup(owner, sourceAnchor);

                if (_session?.IsClosing == true)
                {
                    _session.Dispose();
                    _session = _watchedAnchor is { } anchor && owner.Child is { } presenter
                        ? CreateSession(anchor, presenter, owner.Placement, CompleteClose)
                        : null;
                }

                if (!owner.IsOpen)
                    owner.Open();
                return;
            }

            if (!owner.IsOpen || _completing)
                return;

            if (CupertinoAccessibility.ReduceMotion || _session is null)
                CompleteClose();
            else
                _session.Close();
        }

        public void Opened(object? sender, EventArgs args)
        {
            _session?.Dispose();
            if (_watchedAnchor is { } previous)
                previous.DetachedFromVisualTree -= OnAnchorDetached;
            _watchedAnchor = null;
            var anchor = owner.PlacementTarget ?? owner.TemplatedParent as Control;
            if (anchor is not null && owner.Child is { } presenter)
            {
                _session = CreateSession(anchor, presenter, owner.Placement, CompleteClose);
                _watchedAnchor = anchor;
                anchor.DetachedFromVisualTree += OnAnchorDetached;
            }
        }

        public void Closed(object? sender, EventArgs args)
        {
            if (_watchedAnchor is { } anchor)
                anchor.DetachedFromVisualTree -= OnAnchorDetached;
            _watchedAnchor = null;
            _session?.Dispose();
            _session = null;

            if (!_completing && GetIsOpen(owner))
                owner.SetCurrentValue(IsOpenProperty, false);
        }

        private void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _completing = true;
            owner.SetCurrentValue(IsOpenProperty, false);
            owner.Close();
            _completing = false;
        }

        private void CompleteClose()
        {
            if (!owner.IsOpen)
                return;

            _completing = true;
            owner.Close();
            _completing = false;
        }
    }

    internal sealed class TransitionSession : IDisposable
    {
        private readonly ActiveSession _active;
        private readonly TopLevel _root;
        private readonly Control _panel;
        private readonly Control _material;
        private readonly Control _content;
        private readonly GlassSurface? _glass;
        private readonly TransformOperations _collapsedMaterialTransform;
        private readonly RelativePoint _collapsedMaterialOrigin;
        private readonly Action _closeOwner;
        private readonly ITransform? _materialTransform;
        private readonly RelativePoint _materialTransformOrigin;
        private readonly double _materialOpacity;
        private readonly double _contentOpacity;
        private readonly ITransform? _contentTransform;
        private readonly RelativePoint _contentTransformOrigin;
        private readonly Transitions? _materialTransitions;
        private readonly Transitions? _contentTransitions;
        private readonly BoxShadows? _borderShadow;
        private readonly MotionProfile _profile;
        private bool _closing;
        private bool _disposed;

        internal bool IsClosing => _closing;

        internal TransitionSession(
            ActiveSession active,
            TopLevel root,
            Control panel,
            Control material,
            Control content,
            GlassSurface? glass,
            TransformOperations collapsedMaterialTransform,
            RelativePoint collapsedMaterialOrigin,
            Action closeOwner,
            MotionProfile profile)
        {
            _active = active;
            _root = root;
            _panel = panel;
            _material = material;
            _content = content;
            _glass = glass;
            _collapsedMaterialTransform = collapsedMaterialTransform;
            _collapsedMaterialOrigin = collapsedMaterialOrigin;
            _closeOwner = closeOwner;
            _materialTransform = material.RenderTransform;
            _materialTransformOrigin = material.RenderTransformOrigin;
            _materialOpacity = material.Opacity;
            _contentOpacity = content.Opacity;
            _contentTransform = content.RenderTransform;
            _contentTransformOrigin = content.RenderTransformOrigin;
            _materialTransitions = material.Transitions;
            _contentTransitions = content.Transitions;
            _borderShadow = (material as Border)?.BoxShadow;
            _profile = profile;
        }

        public void Open()
        {
            if (CupertinoAccessibility.ReduceMotion)
            {
                Dispose();
                return;
            }

            _material.RenderTransformOrigin = _collapsedMaterialOrigin;
            _material.RenderTransform = _collapsedMaterialTransform;
            _content.RenderTransformOrigin = _collapsedMaterialOrigin;
            _content.RenderTransform = _collapsedMaterialTransform;
            _material.Opacity = Math.Min(_materialOpacity, _profile switch
            {
                MotionProfile.Menu => 0.78,
                MotionProfile.Popover => 0.30,
                _ => 0.12,
            });
            _content.Opacity = 0;
            SetCompactBorderShadow();
            _glass?.Pulse();

            _root.RequestAnimationFrame(_ =>
            {
                if (_disposed || _closing)
                    return;

                var duration = TimeSpan.FromMilliseconds(_profile switch
                {
                    MotionProfile.Menu => 450,
                    MotionProfile.Popover => 450,
                    _ => 300,
                });
                var opacityDuration = TimeSpan.FromMilliseconds(_profile switch
                {
                    MotionProfile.Menu => 210,
                    MotionProfile.Popover => 170,
                    _ => 160,
                });
                var easing = new Cupertino.Animation.UnderdampedSpringEasing
                {
                    DampingRatio = _profile == MotionProfile.Menu ? 0.75 : 0.8,
                    OmegaDuration = _profile == MotionProfile.Menu ? 7.0 : 7.5,
                };
                _material.Transitions = CreateTransitions(
                    _material, duration, opacityDuration, easing, new SineEaseOut());
                _content.Transitions = CreateTransitions(
                    _content, duration, opacityDuration, easing, new SineEaseOut());
                RestoreBorderShadow();
                _material.RenderTransform = ExpandedTransform;
                _material.Opacity = _materialOpacity;
                _content.RenderTransform = ExpandedTransform;
                _content.Opacity = _contentOpacity;
            });
        }

        public void Close()
        {
            // Reduce Motion disposes the session when it opens; the owner must still close.
            if (_disposed)
            {
                _closeOwner();
                return;
            }
            if (_closing)
                return;

            _closing = true;
            _panel.IsHitTestVisible = false;
            _glass?.Pulse();
            var duration = TimeSpan.FromMilliseconds(_profile switch
            {
                MotionProfile.Menu => 230,
                MotionProfile.Popover => 190,
                _ => 200,
            });
            var opacityDuration = TimeSpan.FromMilliseconds(_profile switch
            {
                MotionProfile.Menu => 190,
                MotionProfile.Popover => 160,
                _ => 160,
            });
            Easing easing = _profile is MotionProfile.Menu or MotionProfile.Popover
                ? new SineEaseIn()
                : new QuadraticEaseIn();
            _material.Transitions = CreateTransitions(
                _material, duration, opacityDuration, easing, new SineEaseOut());
            _content.Transitions = CreateTransitions(
                _content, duration, opacityDuration, easing,
                _profile is MotionProfile.Menu or MotionProfile.Popover
                    ? new SineEaseIn()
                    : easing);
            _material.RenderTransform = _collapsedMaterialTransform;
            _content.RenderTransform = _collapsedMaterialTransform;
            _content.Opacity = 0;
            SetCompactBorderShadow();

            DispatcherTimer.RunOnce(() =>
            {
                if (!_disposed)
                    _material.Opacity = 0;
            }, TimeSpan.FromMilliseconds(_profile == MotionProfile.Popover ? 35 : 65));

            DispatcherTimer.RunOnce(() =>
            {
                if (!_disposed)
                    FinishClose();
            }, duration + TimeSpan.FromMilliseconds(20));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (ReferenceEquals(_active.Session, this))
                _active.Session = null;
            _panel.IsHitTestVisible = true;
            _material.Transitions = null;
            _content.Transitions = null;
            _material.RenderTransform = _materialTransform;
            _material.RenderTransformOrigin = _materialTransformOrigin;
            _material.Opacity = _materialOpacity;
            _content.Opacity = _contentOpacity;
            _content.RenderTransform = _contentTransform;
            _content.RenderTransformOrigin = _contentTransformOrigin;
            _material.Transitions = _materialTransitions;
            _content.Transitions = _contentTransitions;
            RestoreBorderShadow();
        }

        public void FinishForReplacement()
        {
            if (_disposed)
                return;

            _closeOwner();
            Dispose();
        }

        private void FinishClose()
        {
            _closeOwner();
            Dispose();
        }

        private void SetCompactBorderShadow()
        {
            if (_material is Border border)
                border.BoxShadow = CompactBorderShadow;
        }

        private void RestoreBorderShadow()
        {
            if (_material is Border border && _borderShadow is { } shadow)
                border.BoxShadow = shadow;
        }

    }

    internal sealed class ActiveSession
    {
        public TransitionSession? Session;
    }
}
