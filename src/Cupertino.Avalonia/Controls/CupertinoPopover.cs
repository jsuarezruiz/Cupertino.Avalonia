using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// Hosts glass popovers in the window overlay layer.
/// </summary>
public static class CupertinoPopover
{
    private const double AnchorGap = 8;

    private const double EdgeMargin = 24;

    private sealed class Session
    {
        public OverlayLayer Layer = null!;
        public Panel Host = null!;
        public Control Dismisser = null!;
        public Control Panel = null!;
        public Control ContentHost = null!;
        public Rect TargetBounds;
        public CupertinoFlyoutTransition.TransitionSession? Motion;
        public IDisposable? SizeSubscription;
        public EventHandler<VisualTreeAttachmentEventArgs>? AnchorDetachedHandler;
        public EventHandler<KeyEventArgs>? KeyDownHandler;
        public IInputElement? PreviousFocus;
        public TopLevel Root = null!;
        public Action? OnClosed;
        public bool Closing;
        public bool Closed;
    }

    private static readonly Dictionary<Control, Session> Open = new();

    /// <summary>
    /// Gets whether an anchor has an open popover.
    /// </summary>
    public static bool IsOpen(Control anchor) => Open.ContainsKey(anchor);

    /// <summary>
    /// Shows content beside an anchor, moves keyboard focus into it, and supports Escape dismissal.
    /// </summary>
    public static void Show(Control anchor, Control content, double cornerRadius, Action? onClosed)
    {
        if (Open.ContainsKey(anchor))
            return;

        var layer = OverlayLayer.GetOverlayLayer(anchor);
        if (layer is null || TopLevel.GetTopLevel(anchor) is not { } root)
            return;

        var glass = new GlassSurface
        {
            CornerRadius = new CornerRadius(cornerRadius),
            BlurRadius = 36,
            Saturation = 1.8,
            GlassThickness = 0.35,
            RefractionStrength = 0.10,
            ChromaticAberration = 0,
            DepthEffect = 0.25,
            ShadowOpacity = 0.14,
            ShadowBlur = 18,
            ShadowOffset = 6,
            IsAdaptive = true,
        };
        glass.Bind(GlassSurface.TintProperty,
            glass.GetResourceObservable("CupertinoPopoverTint"));

        var contentHost = new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(cornerRadius),
            ClipToBounds = true,
        };

        var panel = new Grid
        {
            ClipToBounds = false,
        };
        panel.Children.Add(glass);
        panel.Children.Add(contentHost);

        // Use an invisible light-dismiss layer.
        var dismisser = new Border { Background = Brushes.Transparent };

        var host = new Panel
        {
            ClipToBounds = false,
            Opacity = 0,
            Focusable = true,
        };
        host.Children.Add(dismisser);
        host.Children.Add(panel);

        var session = new Session
        {
            Layer = layer,
            Host = host,
            Dismisser = dismisser,
            Panel = panel,
            ContentHost = contentHost,
            OnClosed = onClosed,
            PreviousFocus = root.FocusManager?.GetFocusedElement(),
            Root = root,
        };
        Open[anchor] = session;

        KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.Cycle);
        session.KeyDownHandler = (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(anchor);
                e.Handled = true;
            }
        };
        host.KeyDown += session.KeyDownHandler;

        session.AnchorDetachedHandler = (_, _) => CloseCore(anchor, session, animate: false);
        anchor.DetachedFromVisualTree += session.AnchorDetachedHandler;

        dismisser.PointerPressed += (_, _) => Close(anchor);

        session.SizeSubscription = layer.GetObservable(Visual.BoundsProperty).Subscribe(
            new AnonymousObserver<Rect>(b =>
            {
                host.Width = b.Width;
                host.Height = b.Height;
                if (panel.DesiredSize.Width > 0)
                    Position(anchor, session, b);
            }));

        layer.Children.Add(host);

        // Position after layout and before animation.
        host.Width = layer.Bounds.Width;
        host.Height = layer.Bounds.Height;
        layer.UpdateLayout();
        Position(anchor, session, layer.Bounds);
        layer.UpdateLayout();

        session.Motion = CupertinoFlyoutTransition.CreateSession(
            anchor, panel, glass, contentHost, session.TargetBounds,
            () => FinishClose(anchor, session));
        host.Opacity = 1;
        host.Focus();
        Dispatcher.UIThread.Post(() =>
        {
            if (session.Closed || session.Closing || !host.IsKeyboardFocusWithin)
                return;
            content.GetSelfAndVisualDescendants().OfType<InputElement>()
                .FirstOrDefault(control => control.Focusable && control.IsEffectivelyEnabled && control.IsEffectivelyVisible)?.Focus();
        }, DispatcherPriority.Loaded);
    }

    // Keep the popover inside the window.
    private static void Position(Control anchor, Session session, Rect layerBounds)
    {
        if (layerBounds.Width <= 0 || TopLevel.GetTopLevel(anchor) is not { } root)
            return;

        var panel = session.Panel;

        var origin = anchor.TranslatePoint(default, root);
        if (origin is not { } p)
            return;

        panel.Measure(new Size(layerBounds.Width, layerBounds.Height));
        var size = panel.DesiredSize;

        var x = Math.Clamp(p.X, EdgeMargin, Math.Max(EdgeMargin, layerBounds.Width - size.Width - EdgeMargin));

        // Prefer the side with more room.
        var below = p.Y + anchor.Bounds.Height + AnchorGap;
        var above = p.Y - AnchorGap - size.Height;
        var roomBelow = layerBounds.Height - below - EdgeMargin;
        var roomAbove = p.Y - AnchorGap - EdgeMargin;
        var fitsBelow = roomBelow >= size.Height;
        var fitsAbove = roomAbove >= size.Height;

        double y;
        if (fitsAbove && (!fitsBelow || roomAbove > roomBelow))
            y = above;
        else if (fitsBelow)
            y = below;
        else if (fitsAbove)
            y = above;
        else
            y = roomAbove > roomBelow
                ? EdgeMargin
                : Math.Max(EdgeMargin, layerBounds.Height - size.Height - EdgeMargin);

        panel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        panel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        panel.Margin = new Thickness(x, y, 0, 0);

        session.TargetBounds = new Rect(x, y, size.Width, size.Height);
    }

    /// <summary>
    /// Dismisses an anchor's popover.
    /// </summary>
    public static void Close(Control anchor)
    {
        if (!Open.TryGetValue(anchor, out var session))
            return;

        CloseCore(anchor, session, animate: true);
    }

    internal static bool IsClosing(Control anchor) =>
        Open.TryGetValue(anchor, out var session) && session.Closing;

    internal static void CloseImmediately(Control anchor)
    {
        if (Open.TryGetValue(anchor, out var session))
            CloseCore(anchor, session, animate: false);
    }

    internal static void Reposition(Control anchor)
    {
        if (Open.TryGetValue(anchor, out var session))
            Position(anchor, session, session.Layer.Bounds);
    }

    private static void CloseCore(Control anchor, Session session, bool animate)
    {
        if (session.Closed)
            return;
        if (session.Closing)
        {
            if (!animate)
                FinishClose(anchor, session);
            return;
        }
        session.Closing = true;

        if (!animate || CupertinoAccessibility.ReduceMotion)
        {
            FinishClose(anchor, session);
            return;
        }

        if (session.Motion is { } motion)
            motion.Close();
        else
            FinishClose(anchor, session);
    }

    private static void FinishClose(Control anchor, Session session)
    {
        if (session.Closed)
            return;
        session.Closed = true;
        var restoreFocus = session.Host.IsKeyboardFocusWithin;
        var motion = session.Motion;
        session.Motion = null;
        session.SizeSubscription?.Dispose();
        session.SizeSubscription = null;
        if (session.AnchorDetachedHandler is { } handler)
        {
            anchor.DetachedFromVisualTree -= handler;
            session.AnchorDetachedHandler = null;
        }
        if (session.KeyDownHandler is { } keyHandler)
        {
            session.Host.KeyDown -= keyHandler;
            session.KeyDownHandler = null;
        }
        session.Layer.Children.Remove(session.Host);
        if (session.ContentHost is Border contentHost)
            contentHost.Child = null;
        motion?.Dispose();
        if (Open.TryGetValue(anchor, out var current) && ReferenceEquals(current, session))
            Open.Remove(anchor);

        if (restoreFocus && session.PreviousFocus is Control previous
            && previous.IsEffectivelyVisible && previous.IsEffectivelyEnabled
            && TopLevel.GetTopLevel(previous) == session.Root)
            previous.Focus();
        session.PreviousFocus = null;

        var onClosed = session.OnClosed;
        session.OnClosed = null;
        onClosed?.Invoke();
    }
}
