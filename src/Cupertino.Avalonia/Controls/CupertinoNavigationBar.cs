using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cupertino.Animation;

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

    /// <summary>
    /// Identifies the <see cref="Title"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, string?>(nameof(Title));

    /// <summary>
    /// Identifies the <see cref="LeadingContent"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> LeadingContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, object?>(nameof(LeadingContent));

    /// <summary>
    /// Identifies the <see cref="TrailingContent"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> TrailingContentProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, object?>(nameof(TrailingContent));

    /// <summary>
    /// Identifies the <see cref="IsLargeTitle"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsLargeTitleProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, bool>(nameof(IsLargeTitle), true);

    /// <summary>
    /// Identifies the <see cref="Scroller"/> property.
    /// </summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollerProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, ScrollViewer?>(nameof(Scroller));

    /// <summary>
    /// Identifies the <see cref="CollapseProgress"/> property.
    /// </summary>
    public static readonly StyledProperty<double> CollapseProgressProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, double>(nameof(CollapseProgress));

    /// <summary>
    /// Identifies the <see cref="CollapseDistance"/> property.
    /// </summary>
    public static readonly StyledProperty<double> CollapseDistanceProperty =
        AvaloniaProperty.Register<CupertinoNavigationBar, double>(nameof(CollapseDistance), 68.0);

    /// <summary>
    /// The title shown in both the large and inline presentations.
    /// </summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>
    /// Content displayed before the title in the navigation bar.
    /// </summary>
    public object? LeadingContent { get => GetValue(LeadingContentProperty); set => SetValue(LeadingContentProperty, value); }
    /// <summary>
    /// Content displayed after the title in the navigation bar.
    /// </summary>
    public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }
    /// <summary>
    /// Whether the bar supports the large-title presentation.
    /// </summary>
    public bool IsLargeTitle { get => GetValue(IsLargeTitleProperty); set => SetValue(IsLargeTitleProperty, value); }
    /// <summary>
    /// The scroll viewer whose vertical offset drives title collapse. Replacing it removes the old subscription.
    /// </summary>
    public ScrollViewer? Scroller { get => GetValue(ScrollerProperty); set => SetValue(ScrollerProperty, value); }
    /// <summary>
    /// The current normalized collapse amount, from zero (expanded) to one (collapsed).
    /// </summary>
    public double CollapseProgress { get => GetValue(CollapseProgressProperty); set => SetValue(CollapseProgressProperty, value); }
    /// <summary>
    /// The vertical scroll distance in logical pixels that maps to a fully collapsed bar.
    /// </summary>
    public double CollapseDistance { get => GetValue(CollapseDistanceProperty); set => SetValue(CollapseDistanceProperty, value); }

    /// <summary>
    /// Identifies the <see cref="LargeTitleOpacity"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> LargeTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(LargeTitleOpacity), o => o.LargeTitleOpacity);

    /// <summary>
    /// Identifies the <see cref="InlineTitleOpacity"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> InlineTitleOpacityProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(InlineTitleOpacity), o => o.InlineTitleOpacity);

    /// <summary>
    /// Identifies the <see cref="BackdropOpacity"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> BackdropOpacityProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(BackdropOpacity), o => o.BackdropOpacity);

    /// <summary>
    /// Identifies the <see cref="LargeTitleOffset"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoNavigationBar, double> LargeTitleOffsetProperty =
        AvaloniaProperty.RegisterDirect<CupertinoNavigationBar, double>(
            nameof(LargeTitleOffset), o => o.LargeTitleOffset);

    // Title opacities hand over without overlap. The breakpoints are the scroll offsets in points
    // (16/20/32/36/48) divided by the default collapse distance of 68, because the samples are
    // interpolated over normalized CollapseProgress.
    private static readonly (double Progress, double Opacity)[] LargeTitleSamples =
        [(0, 1), (16 / 68.0, 1), (20 / 68.0, 0.68), (32 / 68.0, 0.25), (36 / 68.0, 0), (1, 0)];

    private static readonly (double Progress, double Opacity)[] InlineTitleSamples =
        [(0, 0), (36 / 68.0, 0), (48 / 68.0, 0.51), (1, 1)];

    private IDisposable? _scrollSubscription;
    private IDisposable? _leadingBoundsSubscription;
    private IDisposable? _trailingBoundsSubscription;
    private Control? _inlineTitle;
    private double _largeTitleOpacity = 1;
    private double _inlineTitleOpacity;
    private double _backdropOpacity;
    private double _largeTitleOffset;
    private (string Text, FontFamily? Family, FontStyle Style, FontWeight Weight, double Size,
        double LetterSpacing, FlowDirection Direction)? _measuredTitle;
    private double _measuredTitleWidth;
    private Control? _leading;
    private Control? _trailing;

    /// <summary>
    /// The computed opacity of the expanded title.
    /// </summary>
    public double LargeTitleOpacity => IsLargeTitle ? Sample(LargeTitleSamples, CollapseProgress) : 0;
    /// <summary>
    /// The computed opacity of the compact title.
    /// </summary>
    public double InlineTitleOpacity => IsLargeTitle ? Sample(InlineTitleSamples, CollapseProgress) : 1;
    /// <summary>
    /// The computed opacity of the bar backdrop.
    /// </summary>
    public double BackdropOpacity => MotionCurve.Smoothstep(0.0, 0.45, CollapseProgress);
    /// <summary>
    /// The computed vertical translation of the expanded title, in logical pixels.
    /// </summary>
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
    internal Control? InlineTitle => _inlineTitle;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
        var wanted = MeasureTitle(title);

        var staysCentered = rowWidth <= 0 || rowWidth - 2 * symmetric >= wanted;
        title.HorizontalAlignment = staysCentered
            ? Avalonia.Layout.HorizontalAlignment.Center
            : Avalonia.Layout.HorizontalAlignment.Left;
        title.Margin = staysCentered
            ? new Thickness(symmetric, 0)
            : new Thickness(leading > 0 ? leading + TitleButtonGap : 0, 0,
                            trailing > 0 ? trailing + TitleButtonGap : 0, 0);
    }

    private double MeasureTitle(TextBlock title)
    {
        var text = title.Text ?? string.Empty;
        // Runs on every leading/trailing bounds change; key on the raw components
        // so a cache hit does not allocate a Typeface.
        var key = (text, title.FontFamily, title.FontStyle, title.FontWeight,
                   title.FontSize, title.LetterSpacing, FlowDirection);
        if (_measuredTitle == key)
            return _measuredTitleWidth;

        var width = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection,
            new Typeface(title.FontFamily, title.FontStyle, title.FontWeight),
            title.FontSize, null).Width;
        if (text.Length > 1)
            width += (text.Length - 1) * title.LetterSpacing;
        _measuredTitle = key;
        _measuredTitleWidth = width;
        return width;
    }

    private void RaiseDerived(DirectProperty<CupertinoNavigationBar, double> property, ref double previous, double current)
    {
        var old = previous;
        previous = current;
        if (old != current)
            RaisePropertyChanged(property, old, current);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScrollerProperty)
        {
            ConnectScroller();
        }
        else if (change.Property == CollapseProgressProperty || change.Property == IsLargeTitleProperty)
        {
            RaiseDerived(LargeTitleOpacityProperty, ref _largeTitleOpacity, LargeTitleOpacity);
            RaiseDerived(InlineTitleOpacityProperty, ref _inlineTitleOpacity, InlineTitleOpacity);
            RaiseDerived(BackdropOpacityProperty, ref _backdropOpacity, BackdropOpacity);
            RaiseDerived(LargeTitleOffsetProperty, ref _largeTitleOffset, LargeTitleOffset);
        }
        else if (change.Property == TitleProperty)
        {
            UpdateInlineTitleMargin();
        }
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateInlineTitleMargin();
    }

    // Bounds subscriptions survive detach: the template is not reapplied on reattach.
    /// <inheritdoc/>
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
