using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Cupertino.Controls;

/// <summary>
/// A navigation bar with collapsing large-title and inline-title modes.
/// </summary>
[TemplatePart("PART_Backdrop", typeof(Control))]
[TemplatePart("PART_InlineTitle", typeof(Control))]
[TemplatePart("PART_LargeTitle", typeof(Control))]
[TemplatePart("PART_Leading", typeof(Control))]
[TemplatePart("PART_Trailing", typeof(Control))]
public class CupertinoNavigationBar : TemplatedControl
{
    private const double TitleBaseMargin = 72;
    private const double TitleButtonGap = 12;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, string?>(nameof(Title));

    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, object?>(nameof(LeadingContent));

    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, object?>(nameof(TrailingContent));

    /// <summary>
    /// Gets or sets whether the large title is enabled.
    /// </summary>
    public static readonly StyledProperty<bool> IsLargeTitleProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, bool>(nameof(IsLargeTitle), true);

    /// <summary>
    /// Gets or sets the scroller that collapses the title.
    /// </summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollerProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, ScrollViewer?>(nameof(Scroller));

    /// <summary>
    /// Gets or sets collapse progress from 0 to 1.
    /// </summary>
    public static readonly StyledProperty<double> CollapseProgressProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, double>(nameof(CollapseProgress));

    /// <summary>
    /// Gets or sets the collapse distance.
    /// </summary>
    public static readonly StyledProperty<double> CollapseDistanceProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, double>(nameof(CollapseDistance), 68.0);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }
    public bool IsLargeTitle { get => GetValue(IsLargeTitleProperty); set => SetValue(IsLargeTitleProperty, value); }
    public ScrollViewer? Scroller { get => GetValue(ScrollerProperty); set => SetValue(ScrollerProperty, value); }
    public double CollapseProgress { get => GetValue(CollapseProgressProperty); set => SetValue(CollapseProgressProperty, value); }
    public double CollapseDistance { get => GetValue(CollapseDistanceProperty); set => SetValue(CollapseDistanceProperty, value); }

    /// <summary>
    /// Gets the large-title opacity.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> LargeTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(LargeTitleOpacity), o => o.LargeTitleOpacity);

    /// <summary>
    /// Gets the inline-title opacity.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> InlineTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(InlineTitleOpacity), o => o.InlineTitleOpacity);

    /// <summary>
    /// Gets the backdrop opacity.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> BackdropOpacityProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(BackdropOpacity), o => o.BackdropOpacity);

    /// <summary>
    /// Gets the large-title offset.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> LargeTitleOffsetProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(LargeTitleOffset), o => o.LargeTitleOffset);

    // Title opacities hand over without overlap.
    private static readonly (double Progress, double Opacity)[] LargeTitleSamples =
        [(0, 1), (16 / 68.0, 1), (20 / 68.0, 0.68), (32 / 68.0, 0.25), (36 / 68.0, 0), (1, 0)];

    private static readonly (double Progress, double Opacity)[] InlineTitleSamples =
        [(0, 0), (36 / 68.0, 0), (48 / 68.0, 0.51), (1, 1)];

    public double LargeTitleOpacity => IsLargeTitle ? Sample(LargeTitleSamples, CollapseProgress) : 0;
    public double InlineTitleOpacity => IsLargeTitle ? Sample(InlineTitleSamples, CollapseProgress) : 1;
    public double BackdropOpacity => Smoothstep(0.0, 0.45, CollapseProgress);
    public double LargeTitleOffset => -12.0 * CollapseProgress;

    private static double Sample((double Progress, double Opacity)[] samples, double progress)
    {
        if (progress <= samples[0].Progress)
            return samples[0].Opacity;
        for (var i = 1; i < samples.Length; i++)
        {
            if (progress > samples[i].Progress)
                continue;
            var (p0, o0) = samples[i - 1];
            var (p1, o1) = samples[i];
            return o0 + (o1 - o0) * (progress - p0) / (p1 - p0);
        }
        return samples[^1].Opacity;
    }

    private static double Smoothstep(double a, double b, double x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private IDisposable? _scrollSubscription;
    private IDisposable? _leadingBoundsSubscription;
    private IDisposable? _trailingBoundsSubscription;
    private Control? _inlineTitle;

    internal Control? InlineTitle => _inlineTitle;
    private Control? _leading;
    private Control? _trailing;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _inlineTitle = e.NameScope.Find<Control>("PART_InlineTitle");
        _leading = e.NameScope.Find<Control>("PART_Leading");
        _trailing = e.NameScope.Find<Control>("PART_Trailing");

        _leadingBoundsSubscription?.Dispose();
        _trailingBoundsSubscription?.Dispose();
        _leadingBoundsSubscription = _leading?.GetObservable(BoundsProperty).Subscribe(
            new Avalonia.Reactive.AnonymousObserver<Rect>(_ => UpdateInlineTitleMargin()));
        _trailingBoundsSubscription = _trailing?.GetObservable(BoundsProperty).Subscribe(
            new Avalonia.Reactive.AnonymousObserver<Rect>(_ => UpdateInlineTitleMargin()));
        UpdateInlineTitleMargin();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_scrollSubscription is null)
            ConnectScroller();
    }

    // Keep the title centred until surrounding content requires an inset.
    private void UpdateInlineTitleMargin()
    {
        if (_inlineTitle is not TextBlock title)
            return;

        var leading = _leading?.Bounds.Width ?? 0;
        var trailing = _trailing?.Bounds.Width ?? 0;
        var symmetric = Math.Max(TitleBaseMargin, Math.Max(leading, trailing) + TitleButtonGap);

        var rowWidth = (title.GetVisualParent() as Control)?.Bounds.Width ?? 0;
        var wanted = new FormattedText(
            title.Text ?? string.Empty,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection,
            new Typeface(title.FontFamily, title.FontStyle, title.FontWeight),
            title.FontSize, null).Width;
        if (title.Text is { Length: > 1 })
            wanted += (title.Text.Length - 1) * title.LetterSpacing;

        var staysCentered = rowWidth <= 0 || rowWidth - 2 * symmetric >= wanted;
        title.HorizontalAlignment = staysCentered
            ? Avalonia.Layout.HorizontalAlignment.Center
            : Avalonia.Layout.HorizontalAlignment.Left;
        title.Margin = staysCentered
            ? new Thickness(symmetric, 0)
            : new Thickness(leading + TitleButtonGap, 0, trailing + TitleButtonGap, 0);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScrollerProperty)
        {
            ConnectScroller();
        }
        else if (change.Property == CollapseProgressProperty || change.Property == IsLargeTitleProperty)
        {
            RaisePropertyChanged(LargeTitleOpacityProperty, double.NaN, LargeTitleOpacity);
            RaisePropertyChanged(InlineTitleOpacityProperty, double.NaN, InlineTitleOpacity);
            RaisePropertyChanged(BackdropOpacityProperty, double.NaN, BackdropOpacity);
            RaisePropertyChanged(LargeTitleOffsetProperty, double.NaN, LargeTitleOffset);
        }
        else if (change.Property == TitleProperty)
        {
            UpdateInlineTitleMargin();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateInlineTitleMargin();
    }

    // Bounds subscriptions survive detach: the template is not reapplied on reattach.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scrollSubscription?.Dispose();
        _scrollSubscription = null;
    }

    private void ConnectScroller()
    {
        _scrollSubscription?.Dispose();
        _scrollSubscription = null;

        if (!this.IsAttachedToVisualTree() || Scroller is not { } scroller)
            return;

        void Update(Vector offset)
        {
            var distance = Math.Max(1, CollapseDistance);
            SetCurrentValue(CollapseProgressProperty,
                            Math.Clamp(offset.Y / distance, 0, 1));
        }

        Update(scroller.Offset);
        _scrollSubscription = scroller.GetObservable(ScrollViewer.OffsetProperty).Subscribe(
            new Avalonia.Reactive.AnonymousObserver<Vector>(Update));
    }
}
