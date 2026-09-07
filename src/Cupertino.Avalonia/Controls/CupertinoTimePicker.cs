using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Chooses between selecting a time of day and a countdown duration.
/// </summary>
public enum CupertinoTimePickerMode
{
    /// <summary>
    /// Selects a time of day.
    /// </summary>
    Time,
    /// <summary>
    /// Selects a duration from zero through MaximumDuration.
    /// </summary>
    CountdownDuration,
}

/// <summary>
/// Selects a time or countdown duration.
/// </summary>
[TemplatePart("PART_FlyoutButton", typeof(Button))]
[PseudoClasses(":dropdownopen", ":inline", ":duration")]
public class CupertinoTimePicker : TemplatedControl
{
    private static readonly TimeSpan LargestCountdownDuration =
        TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59);

    /// <summary>
    /// Identifies the <see cref="SelectedTime"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> SelectedTimeProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, TimeSpan?>(
            nameof(SelectedTime), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// "12HourClock" or "24HourClock"; follows the culture when unset.
    /// </summary>
    public static readonly StyledProperty<string?> ClockIdentifierProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, string?>(nameof(ClockIdentifier));

    /// <summary>
    /// Minute-wheel interval, normalized to the supported 1 through 59 range.
    /// </summary>
    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, int>(nameof(MinuteIncrement), 1);

    /// <summary>
    /// Identifies the <see cref="IsDropDownOpen"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, bool>(nameof(IsDropDownOpen));

    /// <summary>
    /// Identifies the <see cref="DisplayMode"/> property.
    /// </summary>
    public static readonly StyledProperty<CupertinoPickerDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, CupertinoPickerDisplayMode>(nameof(DisplayMode));

    /// <summary>
    /// Identifies the <see cref="Mode"/> property.
    /// </summary>
    public static readonly StyledProperty<CupertinoTimePickerMode> ModeProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, CupertinoTimePickerMode>(nameof(Mode));

    /// <summary>
    /// Identifies the <see cref="MinimumTime"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> MinimumTimeProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, TimeSpan?>(nameof(MinimumTime));

    /// <summary>
    /// Identifies the <see cref="MaximumTime"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan?> MaximumTimeProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, TimeSpan?>(nameof(MaximumTime));

    /// <summary>
    /// Gets or sets the countdown upper bound.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> MaximumDurationProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, TimeSpan>(
            nameof(MaximumDuration), TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));

    /// <summary>
    /// The selected time or countdown duration, or null. Values are normalized to the active mode, minute interval, and bounds.
    /// </summary>
    public TimeSpan? SelectedTime { get => GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }
    /// <summary>
    /// Use 12HourClock or 24HourClock; null follows the current culture.
    /// </summary>
    public string? ClockIdentifier { get => GetValue(ClockIdentifierProperty); set => SetValue(ClockIdentifierProperty, value); }
    /// <summary>
    /// The minute-wheel interval, clamped to 1–59. Selections are normalized to the nearest permitted minute.
    /// </summary>
    public int MinuteIncrement { get => GetValue(MinuteIncrementProperty); set => SetValue(MinuteIncrementProperty, value); }
    /// <summary>
    /// The requested popover state. A true value set before attachment opens once the template and overlay are available.
    /// </summary>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
    /// <summary>
    /// Chooses an inline editor or a compact field with a popover.
    /// </summary>
    public CupertinoPickerDisplayMode DisplayMode { get => GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    /// <summary>
    /// Chooses a time of day or a countdown duration.
    /// </summary>
    public CupertinoTimePickerMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    /// <summary>
    /// The inclusive lower time-of-day bound; ignored in countdown mode.
    /// </summary>
    public TimeSpan? MinimumTime { get => GetValue(MinimumTimeProperty); set => SetValue(MinimumTimeProperty, value); }
    /// <summary>
    /// The inclusive upper time-of-day bound; ignored in countdown mode.
    /// </summary>
    public TimeSpan? MaximumTime { get => GetValue(MaximumTimeProperty); set => SetValue(MaximumTimeProperty, value); }
    /// <inheritdoc cref="MaximumDurationProperty"/>
    public TimeSpan MaximumDuration { get => GetValue(MaximumDurationProperty); set => SetValue(MaximumDurationProperty, value); }

    /// <summary>
    /// Whether the effective time mode and clock format use an AM/PM wheel.
    /// </summary>
    public bool Is12Hour => Mode == CupertinoTimePickerMode.Time && (ClockIdentifier is { } id
        ? id == "12HourClock"
        : CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal));

    /// <summary>
    /// Gets the label formatted for <see cref="ClockIdentifier"/>.
    /// </summary>
    public string DisplayText => SelectedTime is { } t
        ? Mode == CupertinoTimePickerMode.CountdownDuration
            ? string.Format(CultureInfo.CurrentCulture, "{0} hr {1} min", (int)t.TotalHours, t.Minutes)
            : DateTime.Today.Add(t).ToString(Is12Hour ? "h:mm tt" : "HH:mm",
                                             CultureInfo.CurrentCulture)
        : "Select";

    /// <summary>
    /// Identifies the <see cref="DisplayText"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoTimePicker, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<CupertinoTimePicker, string>(
            nameof(DisplayText), o => o.DisplayText);

    private CupertinoWheel? _hours, _minutes, _period;
    private Button? _field;
    private int _popoverVersion;
    private ContentControl? _popoverBody;
    private ContentControl? _inlineHost;
    private bool _syncing;

    /// <summary>
    /// Raised after the selected time changes and has been normalized to the current constraints.
    /// </summary>
    public event EventHandler? SelectedTimeChanged;

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ResetPopover();
        if (_field is not null)
            _field.Click -= OnFieldClick;
        base.OnApplyTemplate(e);

        _field = e.NameScope.Find<Button>("PART_FlyoutButton");
        if (_field is not null)
            _field.Click += OnFieldClick;
        _inlineHost = e.NameScope.Find<ContentControl>("PART_InlineHost");
        UpdateModePresentation();
        QueueOpen();
    }

    private void OnFieldClick(object? sender, RoutedEventArgs e) =>
        SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        QueueOpen();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResetPopover();
        base.OnDetachedFromVisualTree(e);
    }

    private void QueueOpen() => Dispatcher.UIThread.Post(() =>
    {
        if (IsDropDownOpen && DisplayMode == CupertinoPickerDisplayMode.Compact)
            ShowPopover();
    }, DispatcherPriority.Loaded);

    private void ResetPopover()
    {
        ++_popoverVersion;
        if (_field is not null)
            CupertinoPopover.CloseImmediately(_field);
        _popoverBody = null;
    }

    private void ShowPopover()
    {
        if (_field is null || TopLevel.GetTopLevel(_field) is null ||
            DisplayMode != CupertinoPickerDisplayMode.Compact)
            return;
        if (_popoverBody is not null && CupertinoPopover.IsOpen(_field) && !CupertinoPopover.IsClosing(_field))
            return;
        ResetPopover();
        var version = _popoverVersion;
        _popoverBody = new ContentControl { Content = BuildWheelBody() };
        CupertinoPopover.Show(_field, _popoverBody, 30, () =>
        {
            if (version != _popoverVersion)
                return;
            _popoverBody = null;
            SetCurrentValue(IsDropDownOpenProperty, false);
        });
    }

    private Panel BuildWheelBody()
    {
        foreach (var wheel in new[] { _hours, _minutes, _period })
            if (wheel is not null)
                wheel.SelectionSettled -= OnWheelSettled;
        var culture = CultureInfo.CurrentCulture;
        var step = EffectiveMinuteIncrement;
        var durationHours = (int)Math.Floor(EffectiveMaximumDuration.TotalHours);

        _hours = new CupertinoWheel
        {
            TextAlignment = Avalonia.Media.TextAlignment.Right,
            Items = Mode == CupertinoTimePickerMode.CountdownDuration
                ? Enumerable.Range(0, durationHours + 1).Select(h => h.ToString(culture)).ToList()
                : Is12Hour
                ? Enumerable.Range(1, 12).Select(h => h.ToString(culture)).ToList()
                : Enumerable.Range(0, 24).Select(h => h.ToString("00", culture)).ToList(),
        };
        _minutes = new CupertinoWheel
        {
            TextAlignment = Avalonia.Media.TextAlignment.Left,
            Items = Enumerable.Range(0, 59 / step + 1)
                              .Select(i => (i * step).ToString("00", culture)).ToList(),
        };
        _period = new CupertinoWheel
        {
            TextAlignment = Avalonia.Media.TextAlignment.Left,
            ShouldLoop = false,
            IsVisible = Is12Hour && Mode == CupertinoTimePickerMode.Time,
            Items = new List<string> { culture.DateTimeFormat.AMDesignator,
                                       culture.DateTimeFormat.PMDesignator },
        };

        foreach (var w in new[] { _hours, _minutes, _period })
        {
            w.SelectionSettled += OnWheelSettled;
            w.Bind(CupertinoWheel.ForegroundProperty,
                   this.GetResourceObservable("CupertinoLabelBrush"));
        }

        PushToWheels();

        var columns = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 32,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
        columns.Children.Add(_hours);
        columns.Children.Add(_minutes);
        columns.Children.Add(_period);

        var bar = new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(17),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(12, 0),
        };
        bar.Bind(Border.BackgroundProperty,
                 this.GetResourceObservable("CupertinoPickerHighlightBrush"));

        var body = new Panel { Width = 242, Height = 245 };
        body.Children.Add(bar);
        body.Children.Add(new Panel { Height = 216, Children = { columns } });

        return body;
    }

    private void PushToWheels()
    {
        if (SelectedTime is not { } t)
            return;

        _syncing = true;
        try
        {
            var step = EffectiveMinuteIncrement;
            if (_minutes is not null)
                _minutes.SelectedIndex = t.Minutes / step;

            if (_hours is not null)
            {
                if (Mode == CupertinoTimePickerMode.CountdownDuration)
                {
                    _hours.SelectedIndex = Math.Clamp((int)t.TotalHours, 0, _hours.Items!.Count - 1);
                }
                else if (Is12Hour)
                {
                    var h12 = t.Hours % 12;
                    _hours.SelectedIndex = (h12 == 0 ? 12 : h12) - 1;
                    if (_period is not null)
                        _period.SelectedIndex = t.Hours >= 12 ? 1 : 0;
                }
                else
                {
                    _hours.SelectedIndex = t.Hours;
                }
            }
        }
        finally { _syncing = false; }
    }

    private void OnWheelSettled(object? sender, EventArgs e)
    {
        if (_syncing || _hours is null || _minutes is null)
            return;

        var hour = Mode == CupertinoTimePickerMode.CountdownDuration
            ? _hours.SelectedIndex
            : Is12Hour
            ? (_hours.SelectedIndex + 1) % 12 + (_period?.SelectedIndex == 1 ? 12 : 0)
            : _hours.SelectedIndex;

        var minute = _minutes.SelectedIndex * EffectiveMinuteIncrement;
        SetCurrentValue(SelectedTimeProperty, new TimeSpan(hour, minute, 0));
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedTimeProperty)
        {
            var coerced = CoerceTime(SelectedTime);
            if (coerced != SelectedTime)
            {
                SetCurrentValue(SelectedTimeProperty, coerced);
                return;
            }
            RaisePropertyChanged(DisplayTextProperty, string.Empty, DisplayText);
            if (!_syncing)
                PushToWheels();
            SelectedTimeChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == IsDropDownOpenProperty)
        {
            PseudoClasses.Set(":dropdownopen", IsDropDownOpen);
            if (IsDropDownOpen)
                ShowPopover();
            else if (_field is not null)
                CupertinoPopover.Close(_field);
        }
        else if (change.Property == DisplayModeProperty || change.Property == ModeProperty ||
                 change.Property == ClockIdentifierProperty || change.Property == MinuteIncrementProperty ||
                 change.Property == MaximumDurationProperty)
        {
            if (change.Property == MinuteIncrementProperty && MinuteIncrement != EffectiveMinuteIncrement)
            {
                SetCurrentValue(MinuteIncrementProperty, EffectiveMinuteIncrement);
                return;
            }
            if (change.Property == MaximumDurationProperty && MaximumDuration != EffectiveMaximumDuration)
            {
                SetCurrentValue(MaximumDurationProperty, EffectiveMaximumDuration);
                return;
            }

            RaisePropertyChanged(DisplayTextProperty, string.Empty, DisplayText);
            UpdateModePresentation();
            var coerced = CoerceTime(SelectedTime);
            if (coerced != SelectedTime)
                SetCurrentValue(SelectedTimeProperty, coerced);
        }
        else if (change.Property == MinimumTimeProperty || change.Property == MaximumTimeProperty)
        {
            var coerced = CoerceTime(SelectedTime);
            if (coerced != SelectedTime)
                SetCurrentValue(SelectedTimeProperty, coerced);
        }
    }

    private void UpdateModePresentation()
    {
        PseudoClasses.Set(":inline", DisplayMode == CupertinoPickerDisplayMode.Inline);
        PseudoClasses.Set(":duration", Mode == CupertinoTimePickerMode.CountdownDuration);
        if (DisplayMode == CupertinoPickerDisplayMode.Inline)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
            if (_inlineHost is not null)
                _inlineHost.Content = BuildWheelBody();
        }
        else
        {
            if (_inlineHost is not null)
                _inlineHost.Content = null;
            if (_popoverBody is not null)
            {
                _popoverBody.Content = BuildWheelBody();
                if (_field is not null)
                    CupertinoPopover.Reposition(_field);
            }
        }
    }

    private TimeSpan? CoerceTime(TimeSpan? value)
    {
        if (value is null)
            return null;
        var result = value.Value < TimeSpan.Zero ? TimeSpan.Zero : value.Value;
        if (Mode == CupertinoTimePickerMode.CountdownDuration)
        {
            var maximum = EffectiveMaximumDuration;
            result = result > maximum ? maximum : result;
            return NormalizeTimeValue(result, EffectiveMinuteIncrement,
                                      TimeSpan.Zero, maximum);
        }
        result = TimeSpan.FromTicks(result.Ticks % TimeSpan.FromDays(1).Ticks);
        var minimum = ClampTimeOfDay(MinimumTime ?? TimeSpan.Zero);
        var maximumTime = ClampTimeOfDay(MaximumTime ?? TimeSpan.FromDays(1) - TimeSpan.FromTicks(1));
        if (minimum > maximumTime)
            minimum = maximumTime;
        return NormalizeTimeValue(result, EffectiveMinuteIncrement, minimum, maximumTime);
    }

    internal static TimeSpan NormalizeTimeValue(
        TimeSpan value, int minuteIncrement, TimeSpan minimum, TimeSpan maximum)
    {
        var clamped = value < minimum ? minimum : value > maximum ? maximum : value;
        var step = Math.Clamp(minuteIncrement, 1, 59);
        TimeSpan? best = null;
        var bestDistance = long.MaxValue;

        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute += step)
            {
                var candidate = new TimeSpan(hour, minute, 0);
                if (candidate < minimum || candidate > maximum)
                    continue;
                var distance = Math.Abs((candidate - clamped).Ticks);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        // A narrow range may contain no selectable row.
        return best ?? clamped;
    }

    private static TimeSpan ClampTimeOfDay(TimeSpan value) =>
        value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value >= TimeSpan.FromDays(1)
                ? TimeSpan.FromDays(1) - TimeSpan.FromTicks(1)
                : value;

    private int EffectiveMinuteIncrement => Math.Clamp(MinuteIncrement, 1, 59);

    private TimeSpan EffectiveMaximumDuration =>
        MaximumDuration < TimeSpan.Zero
            ? TimeSpan.Zero
            : MaximumDuration > LargestCountdownDuration
                ? LargestCountdownDuration
                : MaximumDuration;
}
