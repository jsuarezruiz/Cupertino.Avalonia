using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
    /// Identifies the <see cref="ClockIdentifier"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> ClockIdentifierProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, string?>(nameof(ClockIdentifier));

    /// <summary>
    /// Identifies the <see cref="MinuteIncrement"/> property.
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
    /// Identifies the <see cref="MaximumDuration"/> property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> MaximumDurationProperty =
        AvaloniaProperty.Register<CupertinoTimePicker, TimeSpan>(
            nameof(MaximumDuration), LargestCountdownDuration);

    /// <summary>
    /// The selected time or countdown duration, or null. Values are normalized to the active mode, minute interval, and bounds.
    /// </summary>
    public TimeSpan? SelectedTime { get => GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }
    /// <summary>
    /// Use 12HourClock for an AM/PM wheel; null follows the current culture and any other value selects the 24-hour clock.
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
    /// <summary>
    /// The countdown upper bound, clamped to zero through 23 hours 59 minutes.
    /// </summary>
    public TimeSpan MaximumDuration { get => GetValue(MaximumDurationProperty); set => SetValue(MaximumDurationProperty, value); }

    /// <summary>
    /// Whether the effective time mode and clock format use an AM/PM wheel.
    /// </summary>
    public bool Is12Hour => Mode == CupertinoTimePickerMode.Time && (ClockIdentifier is { } id
        ? id == "12HourClock"
        : CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal));

    /// <summary>
    /// The capsule label formatted for <see cref="ClockIdentifier"/>. Duration and placeholder text come from the CupertinoDurationFormat and CupertinoPickerPlaceholderText resources.
    /// </summary>
    public string DisplayText => SelectedTime is { } t
        ? Mode == CupertinoTimePickerMode.CountdownDuration
            ? string.Format(CultureInfo.CurrentCulture, Resource("CupertinoDurationFormat", "{0} hr {1} min"),
                            (int)t.TotalHours, t.Minutes)
            : DateTime.Today.Add(t).ToString(Is12Hour ? "h:mm\u202Ftt" : "HH:mm",
                                             CultureInfo.CurrentCulture)
        : Resource("CupertinoPickerPlaceholderText", "Select");

    private string Resource(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is string text ? text : fallback;

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
    private string _displayText = string.Empty;
    private readonly List<IDisposable> _wheelBindings = new();

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
        RaiseDisplayTextChanged();
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
        if (_inlineHost is not null)
            _inlineHost.Content = null;
        ReleaseWheelBody();
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
        var body = new ContentControl { Content = BuildWheelBody() };
        _popoverBody = body;
        CupertinoPopover.Show(_field, body, 30, () =>
        {
            if (version != _popoverVersion || !ReferenceEquals(_popoverBody, body))
                return;
            _popoverBody = null;
            ReleaseWheelBody();
            SetCurrentValue(IsDropDownOpenProperty, false);
        });
        if (!CupertinoPopover.IsOpen(_field))
        {
            _popoverBody = null;
            ReleaseWheelBody();
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private Panel BuildWheelBody()
    {
        ReleaseWheelBody();
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
            _wheelBindings.Add(w.Bind(CupertinoWheel.ForegroundProperty,
                   this.GetResourceObservable("CupertinoLabelBrush")));
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
        _wheelBindings.Add(bar.Bind(Border.BackgroundProperty,
                 this.GetResourceObservable("CupertinoPickerHighlightBrush")));

        var body = new Panel { Width = 242, Height = 245 };
        body.Children.Add(bar);
        body.Children.Add(new Panel { Height = 216, Children = { columns } });

        return body;
    }

    private void ReleaseWheelBody()
    {
        foreach (var wheel in new[] { _hours, _minutes, _period })
            if (wheel is not null)
                wheel.SelectionSettled -= OnWheelSettled;
        foreach (var binding in _wheelBindings)
            binding.Dispose();
        _wheelBindings.Clear();
        _hours = null;
        _minutes = null;
        _period = null;
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
            RaiseDisplayTextChanged();
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

            RaiseDisplayTextChanged();
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

    private void RaiseDisplayTextChanged()
    {
        var previous = _displayText;
        _displayText = DisplayText;
        if (previous != _displayText)
            RaisePropertyChanged(DisplayTextProperty, previous, _displayText);
    }

    private void UpdateModePresentation()
    {
        PseudoClasses.Set(":inline", DisplayMode == CupertinoPickerDisplayMode.Inline);
        PseudoClasses.Set(":duration", Mode == CupertinoTimePickerMode.CountdownDuration);
        if (DisplayMode == CupertinoPickerDisplayMode.Inline)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
            ResetPopover();
            if (_inlineHost is not null)
                _inlineHost.Content = BuildWheelBody();
        }
        else
        {
            if (_inlineHost?.Content is not null)
            {
                _inlineHost.Content = null;
                ReleaseWheelBody();
            }
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
        var step = DateMath.ClampMinuteIncrement(minuteIncrement);
        var lastMinute = 59 / step * step;

        // Compare the rows either side; the next one can fall in the following hour.
        var hour = (int)Math.Clamp(Math.Floor(clamped.TotalHours), 0, 23);
        var minuteOfHour = clamped.TotalMinutes - hour * 60;
        var lowerMinute = (int)Math.Floor(minuteOfHour / step) * step;
        var previous = new TimeSpan(hour, lowerMinute, 0);
        var next = NextRow(previous, step, lastMinute);
        var nextValid = next is { } n && n >= minimum && n <= maximum;
        var previousValid = previous >= minimum && previous <= maximum;
        if (nextValid && previousValid)
            // Ties settle on the lower row.
            return Math.Abs((previous - clamped).Ticks) <= Math.Abs((next!.Value - clamped).Ticks)
                ? previous
                : next.Value;
        if (nextValid)
            return next!.Value;
        if (previousValid)
            return previous;

        // A narrow range may contain no selectable row.
        return clamped;
    }

    private static TimeSpan? NextRow(TimeSpan row, int step, int lastMinute)
    {
        var minute = row.Minutes + step;
        var hour = row.Hours;
        if (minute > lastMinute)
        {
            minute = 0;
            hour++;
        }
        return hour > 23 ? null : new TimeSpan(hour, minute, 0);
    }

    private static TimeSpan ClampTimeOfDay(TimeSpan value) =>
        value < TimeSpan.Zero
            ? TimeSpan.Zero
            : value >= TimeSpan.FromDays(1)
                ? TimeSpan.FromDays(1) - TimeSpan.FromTicks(1)
                : value;

    private int EffectiveMinuteIncrement => DateMath.ClampMinuteIncrement(MinuteIncrement);

    private TimeSpan EffectiveMaximumDuration =>
        MaximumDuration < TimeSpan.Zero
            ? TimeSpan.Zero
            : MaximumDuration > LargestCountdownDuration
                ? LargestCountdownDuration
                : MaximumDuration;
}
