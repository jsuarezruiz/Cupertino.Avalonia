using Avalonia;
using Avalonia.Animation;
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

    private const double EdgeMargin = 8;

    private sealed class Session
    {
        public OverlayLayer Layer = null!;
        public Panel Host = null!;
        public Control Dismisser = null!;
        public Control Panel = null!;
        public Avalonia.Media.Transformation.TransformOperations CollapsedTransform =
            Avalonia.Media.Transformation.TransformOperations.Parse("scale(0.35,0.12)");
        public IDisposable? SizeSubscription;
        public EventHandler<VisualTreeAttachmentEventArgs>? AnchorDetachedHandler;
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
    /// Shows content beside an anchor.
    /// </summary>
    public static void Show(Control anchor, Control content, double cornerRadius, Action? onClosed)
    {
        if (Open.ContainsKey(anchor))
            return;

        var layer = OverlayLayer.GetOverlayLayer(anchor);
        if (layer is null)
            return;

        var glass = new GlassSurface
        {
            CornerRadius = new CornerRadius(cornerRadius),
            BlurRadius = 28,
            Saturation = 1.8,
            GlassThickness = 0.35,
            RefractionStrength = 0.10,
            ChromaticAberration = 0,
            DepthEffect = 0.25,
            ShadowOpacity = 0.20,
            ShadowBlur = 30,
            ShadowOffset = 10,
            IsAdaptive = true,
            Child = content,
        };

        var panel = new Border
        {
            Child = glass,
            ClipToBounds = false,
            Opacity = 0,
        };

        // Use an invisible light-dismiss layer.
        var dismisser = new Border { Background = Brushes.Transparent };

        var host = new Panel { ClipToBounds = false };
        host.Children.Add(dismisser);
        host.Children.Add(panel);

        var session = new Session
        {
            Layer = layer,
            Host = host,
            Dismisser = dismisser,
            Panel = panel,
            OnClosed = onClosed,
        };
        Open[anchor] = session;

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
        panel.RenderTransform = session.CollapsedTransform;

        // Attach transitions after initial placement.
        if (CupertinoAccessibility.ReduceMotion)
        {
            panel.Opacity = 1;
            panel.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
            return;
        }

        DispatcherTimer.RunOnce(() =>
        {
            if (session.Closing
                || !Open.TryGetValue(anchor, out var current)
                || !ReferenceEquals(current, session))
                return;

            // Morph from the anchor while resampling the backdrop.
            panel.Transitions =
            [
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(180),
                    Easing = new Cupertino.Animation.CriticallyDampedEasing
                    {
                        OmegaDuration = 10,
                    },
                },
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(280),
                    Easing = new Cupertino.Animation.CriticallyDampedEasing
                    {
                        OmegaDuration = 8.4,
                    },
                },
            ];
            panel.Opacity = 1;
            panel.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
        }, TimeSpan.FromMilliseconds(16));
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

        // Scale from the anchor without moving its attachment point.
        var anchorX = p.X + anchor.Bounds.Width / 2;
        var anchorY = p.Y + anchor.Bounds.Height / 2;
        panel.RenderTransformOrigin = new RelativePoint(
            (anchorX - x) / size.Width,
            (anchorY - y) / size.Height,
            RelativeUnit.Relative);

        var scaleX = Math.Clamp(anchor.Bounds.Width / size.Width, 0.05, 0.95);
        var scaleY = Math.Clamp(anchor.Bounds.Height / size.Height, 0.05, 0.95);
        session.CollapsedTransform = Avalonia.Media.Transformation.TransformOperations.Parse(
            FormattableString.Invariant($"scale({scaleX},{scaleY})"));
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

        session.Panel.Transitions =
        [
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(130),
                Easing = new Avalonia.Animation.Easings.QuadraticEaseIn(),
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(160),
                Easing = new Avalonia.Animation.Easings.QuadraticEaseIn(),
            },
        ];
        session.Panel.Opacity = 0;
        session.Panel.RenderTransform = session.CollapsedTransform;

        DispatcherTimer.RunOnce(() => FinishClose(anchor, session), TimeSpan.FromMilliseconds(170));
    }

    private static void FinishClose(Control anchor, Session session)
    {
        if (session.Closed)
            return;
        session.Closed = true;
        session.SizeSubscription?.Dispose();
        session.SizeSubscription = null;
        if (session.AnchorDetachedHandler is { } handler)
        {
            anchor.DetachedFromVisualTree -= handler;
            session.AnchorDetachedHandler = null;
        }
        session.Layer.Children.Remove(session.Host);
        if (Open.TryGetValue(anchor, out var current) && ReferenceEquals(current, session))
            Open.Remove(anchor);

        var onClosed = session.OnClosed;
        session.OnClosed = null;
        onClosed?.Invoke();
    }
}
