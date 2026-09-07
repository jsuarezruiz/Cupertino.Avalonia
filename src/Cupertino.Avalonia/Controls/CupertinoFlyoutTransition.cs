using System.ComponentModel;
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
    private sealed class PropertyOwner : AvaloniaObject
    {
    }

    private static readonly TransformOperations CollapsedMaterialTransform =
        TransformOperations.Parse("scale(0.5)");

    private static readonly TransformOperations ExpandedTransform =
        TransformOperations.Parse("scale(1)");

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

    private static readonly ConditionalWeakTable<PopupFlyoutBase, FlyoutState> Flyouts = new();
    private static readonly ConditionalWeakTable<ContextMenu, ContextMenuState> ContextMenus = new();
    private static readonly ConditionalWeakTable<Popup, PopupState> Popups = new();
    private static readonly ConditionalWeakTable<TopLevel, ActiveSession> ActiveSessions = new();

    private static bool _initialized;

    static CupertinoFlyoutTransition()
    {
        IsEnabledProperty.Changed.Subscribe(
            new AnonymousObserver<AvaloniaPropertyChangedEventArgs<bool>>(OnIsEnabledChanged));
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(AvaloniaObject control) => control.GetValue(IsEnabledProperty);

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(AvaloniaObject control, bool value) =>
        control.SetValue(IsEnabledProperty, value);

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
            Attach(popup);
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
                               control.Classes.Contains("source-transition-material"))
                       ?? panel;
        var glass = material as GlassSurface;
        var content = FindContent(panel, glass);
        var origin = panel.TranslatePoint(default, root);
        if (origin is not { } point || panel.Bounds.Width <= 0 || panel.Bounds.Height <= 0)
            return null;

        var targetBounds = new Rect(point, panel.Bounds.Size);
        var anchorBounds = GetAnchorBounds(anchor, root, targetBounds, placement);
        return StartSession(
            root, anchor, panel, material, content, glass,
            anchorBounds, targetBounds, closeOwner);
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
            root, anchor, panel, material, content, material as GlassSurface,
            new Rect(point, anchor.Bounds.Size), targetBounds, closeOwner);
    }

    private static TransitionSession StartSession(
        TopLevel root,
        Control anchor,
        Control panel,
        Control material,
        Control content,
        GlassSurface? glass,
        Rect anchorBounds,
        Rect targetBounds,
        Action closeOwner)
    {
        var source = anchorBounds.Center;
        var collapsedOrigin = new RelativePoint(
            Math.Clamp((source.X - targetBounds.X) / targetBounds.Width, 0, 1),
            Math.Clamp((source.Y - targetBounds.Y) / targetBounds.Height, 0, 1),
            RelativeUnit.Relative);
        var active = ActiveSessions.GetOrCreateValue(root);
        active.Session?.FinishForReplacement();

        var session = new TransitionSession(
            active, root, panel, material, content, glass,
            CollapsedMaterialTransform, collapsedOrigin, closeOwner);
        active.Session = session;
        session.Open();
        return session;
    }

    private static Control FindPopoverPanel(Control presenter) =>
        presenter.Classes.Contains("cupertino-popover")
            ? presenter
            : presenter.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => control.Classes.Contains("cupertino-popover"))
              ?? presenter;

    private static Control FindContent(Control panel, GlassSurface? glass)
    {
        if (glass?.Child is Control child)
            return child;

        return panel.GetVisualDescendants()
                   .OfType<Control>()
                   .FirstOrDefault(control => control.Classes.Contains("source-transition-content"))
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

    private static Transitions CreateMaterialTransitions(TimeSpan duration, Easing easing) =>
    [
        new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(160),
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.BlurRadiusProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.SaturationProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.RefractionStrengthProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.DepthEffectProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.ShadowOpacityProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.ShadowBlurProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = GlassSurface.ShadowOffsetProperty,
            Duration = duration,
            Easing = easing,
        },
    ];

    private static Transitions CreatePlainMaterialTransitions(TimeSpan duration, Easing easing) =>
    [
        new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(160),
            Easing = easing,
        },
    ];

    private static Transitions CreateBorderMaterialTransitions(TimeSpan duration, Easing easing) =>
    [
        new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = duration,
            Easing = easing,
        },
        new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(160),
            Easing = easing,
        },
        new BoxShadowsTransition
        {
            Property = Border.BoxShadowProperty,
            Duration = duration,
            Easing = easing,
        },
    ];

    private static Transitions CreateContentTransitions(TimeSpan duration, Easing easing) =>
    [
        new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = duration,
            Easing = easing,
        },
    ];

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

            _preparedPresenter = presenter;
            _preparedOpacity = presenter.Opacity;
            presenter.Opacity = 0;
        }

        public void Opened(object? sender, EventArgs args)
        {
            _session?.Dispose();
            _session = owner.Target is { } anchor && owner.Popup.Child is { } presenter
                ? CreateSession(anchor, presenter, owner.Popup.Placement, CompleteClose)
                : null;
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

        public void Opening(object? sender, CancelEventArgs args)
        {
            if (CupertinoAccessibility.ReduceMotion)
                return;

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

        public void Opened(object? sender, EventArgs args)
        {
            _session?.Dispose();
            var anchor = owner.PlacementTarget ?? owner.TemplatedParent as Control;
            if (anchor is not null && owner.Child is { } presenter)
            {
                _session = CreateSession(anchor, presenter, owner.Placement, owner.Close);
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
        }

        private void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs args) =>
            owner.Close();
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
        private readonly GlassValues? _glassValues;
        private readonly BoxShadows? _borderShadow;
        private bool _closing;
        private bool _disposed;

        internal TransitionSession(
            ActiveSession active,
            TopLevel root,
            Control panel,
            Control material,
            Control content,
            GlassSurface? glass,
            TransformOperations collapsedMaterialTransform,
            RelativePoint collapsedMaterialOrigin,
            Action closeOwner)
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
            _glassValues = glass is null ? null : new GlassValues(glass);
            _borderShadow = (material as Border)?.BoxShadow;
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
            _material.Opacity = Math.Min(_materialOpacity, 0.12);
            _content.Opacity = 0;
            SetCompactGlass();
            SetCompactBorderShadow();
            _glass?.Pulse();

            _root.RequestAnimationFrame(_ =>
            {
                if (_disposed || _closing)
                    return;

                var easing = new Cupertino.Animation.UnderdampedSpringEasing
                {
                    DampingRatio = 0.8,
                    OmegaDuration = 12.0,
                };
                _material.Transitions = _glass is null
                    ? _material is Border
                        ? CreateBorderMaterialTransitions(TimeSpan.FromMilliseconds(300), easing)
                        : CreatePlainMaterialTransitions(TimeSpan.FromMilliseconds(300), easing)
                    : CreateMaterialTransitions(TimeSpan.FromMilliseconds(300), easing);
                _content.Transitions = CreateContentTransitions(
                    TimeSpan.FromMilliseconds(90), new QuadraticEaseOut());
                RestoreGlassValues();
                RestoreBorderShadow();
                _material.RenderTransform = ExpandedTransform;
                _material.Opacity = _materialOpacity;
                _content.Opacity = _contentOpacity;
            });
        }

        public void Close()
        {
            if (_disposed || _closing)
                return;

            _closing = true;
            _panel.IsHitTestVisible = false;
            _glass?.Pulse();
            var easing = new QuadraticEaseIn();
            _material.Transitions = _glass is null
                ? _material is Border
                    ? CreateBorderMaterialTransitions(TimeSpan.FromMilliseconds(200), easing)
                    : CreatePlainMaterialTransitions(TimeSpan.FromMilliseconds(200), easing)
                : CreateMaterialTransitions(TimeSpan.FromMilliseconds(200), easing);
            _content.Transitions = CreateContentTransitions(TimeSpan.FromMilliseconds(150), easing);
            _material.RenderTransform = _collapsedMaterialTransform;
            _content.Opacity = 0;
            SetCompactGlass();
            SetCompactBorderShadow();

            DispatcherTimer.RunOnce(() =>
            {
                if (!_disposed)
                    _material.Opacity = 0;
            }, TimeSpan.FromMilliseconds(40));

            DispatcherTimer.RunOnce(() =>
            {
                if (!_disposed)
                    FinishClose();
            }, TimeSpan.FromMilliseconds(220));
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
            RestoreGlassValues();
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

        private void SetCompactGlass()
        {
            if (_glass is null || _glassValues is not { } values)
                return;

            _glass.BlurRadius = values.BlurRadius * 0.72;
            _glass.Saturation = 1 + (values.Saturation - 1) * 0.45;
            _glass.RefractionStrength = values.RefractionStrength * 0.35;
            _glass.DepthEffect = values.DepthEffect * 0.5;
            _glass.ShadowOpacity = 0;
            _glass.ShadowBlur = values.ShadowBlur * 0.45;
            _glass.ShadowOffset = values.ShadowOffset * 0.35;
        }

        private void SetCompactBorderShadow()
        {
            if (_material is Border border)
                border.BoxShadow = CompactBorderShadow;
        }

        private void RestoreGlassValues()
        {
            if (_glass is null || _glassValues is not { } values)
                return;

            _glass.BlurRadius = values.BlurRadius;
            _glass.Saturation = values.Saturation;
            _glass.RefractionStrength = values.RefractionStrength;
            _glass.DepthEffect = values.DepthEffect;
            _glass.ShadowOpacity = values.ShadowOpacity;
            _glass.ShadowBlur = values.ShadowBlur;
            _glass.ShadowOffset = values.ShadowOffset;
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

    private readonly record struct GlassValues(
        double BlurRadius,
        double Saturation,
        double RefractionStrength,
        double DepthEffect,
        double ShadowOpacity,
        double ShadowBlur,
        double ShadowOffset)
    {
        public GlassValues(GlassSurface glass)
            : this(
                glass.BlurRadius,
                glass.Saturation,
                glass.RefractionStrength,
                glass.DepthEffect,
                glass.ShadowOpacity,
                glass.ShadowBlur,
                glass.ShadowOffset)
        {
        }
    }
}
