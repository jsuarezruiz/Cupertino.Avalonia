using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cupertino.Controls;

/// <summary>
/// Combines date and time selection.
/// </summary>
[TemplatePart("PART_Date", typeof(CupertinoDatePicker))]
[TemplatePart("PART_Time", typeof(CupertinoTimePicker))]
public class CupertinoDateTimePicker : TemplatedControl
{
    /// <summary>
    /// Identifies the <see cref="SelectedDateTime"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateTimeProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, DateTimeOffset?>(
            nameof(SelectedDateTime), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="Minimum"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MinimumProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, DateTimeOffset?>(nameof(Minimum));

    /// <summary>
    /// Identifies the <see cref="Maximum"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MaximumProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, DateTimeOffset?>(nameof(Maximum));

    /// <summary>
    /// Identifies the <see cref="DateFormat"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, string?>(nameof(DateFormat));

    /// <summary>
    /// Identifies the <see cref="ClockIdentifier"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> ClockIdentifierProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, string?>(nameof(ClockIdentifier));

    /// <summary>
    /// Identifies the <see cref="MinuteIncrement"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, int>(nameof(MinuteIncrement), 1);

    /// <summary>
    /// The selected date and time, or null. Values are constrained to Minimum and Maximum and normalized to the minute interval.
    /// </summary>
    public DateTimeOffset? SelectedDateTime { get => GetValue(SelectedDateTimeProperty); set => SetValue(SelectedDateTimeProperty, value); }
    /// <summary>
    /// The inclusive earliest selectable date-time, or null for no lower bound.
    /// </summary>
    public DateTimeOffset? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    /// <summary>
    /// The inclusive latest selectable date-time, or null for no upper bound.
    /// </summary>
    public DateTimeOffset? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>
    /// The date label format string, or null to use an abbreviated month in the current culture's date-field order.
    /// </summary>
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }
    /// <summary>
    /// Use 12HourClock or 24HourClock; null follows the current culture.
    /// </summary>
    public string? ClockIdentifier { get => GetValue(ClockIdentifierProperty); set => SetValue(ClockIdentifierProperty, value); }
    /// <summary>
    /// The minute-wheel interval, clamped to 1–59. Selections are normalized to the nearest permitted minute.
    /// </summary>
    public int MinuteIncrement { get => GetValue(MinuteIncrementProperty); set => SetValue(MinuteIncrementProperty, value); }

    /// <summary>
    /// Raised after the selected date-time changes and its constraints have been applied.
    /// </summary>
    public event EventHandler? SelectedDateTimeChanged;

    private CupertinoDatePicker? _date;
    private CupertinoTimePicker? _time;
    private bool _syncing;

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_date is not null)
            _date.SelectedDateChanged -= OnDateChanged;
        if (_time is not null)
            _time.SelectedTimeChanged -= OnTimeChanged;
        base.OnApplyTemplate(e);
        _date = e.NameScope.Find<CupertinoDatePicker>("PART_Date");
        _time = e.NameScope.Find<CupertinoTimePicker>("PART_Time");
        if (_date is not null)
            _date.SelectedDateChanged += OnDateChanged;
        if (_time is not null)
            _time.SelectedTimeChanged += OnTimeChanged;
        PushToParts();
    }

    private void PushToParts()
    {
        _syncing = true;
        try
        {
            if (_date is not null)
            {
                var minimumDate = SelectedDateTime is { } selectedMinimum
                    ? AtOffset(Minimum, selectedMinimum.Offset) : Minimum;
                var maximumDate = SelectedDateTime is { } selectedMaximum
                    ? AtOffset(Maximum, selectedMaximum.Offset) : Maximum;
                if (_date.MinimumDate != minimumDate || _date.MaximumDate != maximumDate)
                {
                    // Replace old bounds before selection, using its local calendar.
                    _date.MinimumDate = null;
                    _date.MaximumDate = null;
                    _date.MinimumDate = minimumDate;
                    _date.MaximumDate = maximumDate;
                }
                if (_date.SelectedDate != SelectedDateTime)
                    _date.SelectedDate = SelectedDateTime;
                if (_date.DateFormat != DateFormat)
                    _date.DateFormat = DateFormat;
            }
            if (_time is not null)
            {
                if (_time.ClockIdentifier != ClockIdentifier)
                    _time.ClockIdentifier = ClockIdentifier;
                if (_time.MinuteIncrement != MinuteIncrement)
                    _time.MinuteIncrement = MinuteIncrement;
                TimeSpan? minimumTime = null, maximumTime = null;
                if (SelectedDateTime is { } selected)
                    (minimumTime, maximumTime) = GetTimeBounds(selected);
                if (_time.MinimumTime != minimumTime)
                    _time.MinimumTime = minimumTime;
                if (_time.MaximumTime != maximumTime)
                    _time.MaximumTime = maximumTime;
                var time = SelectedDateTime?.TimeOfDay;
                if (_time.SelectedTime != time)
                    _time.SelectedTime = time;
            }
        }
        finally { _syncing = false; }
    }

    private void OnDateChanged(object? sender, EventArgs e)
    {
        if (_syncing || _date?.SelectedDate is not { } date)
            return;
        var time = _time?.SelectedTime ?? SelectedDateTime?.TimeOfDay ?? TimeSpan.Zero;
        SetDateAndTime(date, time);
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        if (_syncing || _time?.SelectedTime is not { } time)
            return;
        var date = _date?.SelectedDate ?? SelectedDateTime ?? DateTimeOffset.Now;
        SetDateAndTime(date, time);
    }

    private void SetDateAndTime(DateTimeOffset date, TimeSpan time)
    {
        var ticks = Math.Clamp(date.Date.Ticks + time.Ticks,
            Math.Max(DateTime.MinValue.Ticks, date.Offset.Ticks),
            Math.Min(DateTime.MaxValue.Ticks, DateTime.MaxValue.Ticks + date.Offset.Ticks));
        SetCurrentValue(SelectedDateTimeProperty, new DateTimeOffset(new DateTime(ticks), date.Offset));
    }

    private static DateTimeOffset? AtOffset(DateTimeOffset? value, TimeSpan offset) =>
        value is { } date
            ? new DateTimeOffset(new DateTime(Math.Clamp(date.UtcTicks + offset.Ticks,
                DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks)), offset)
            : null;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDateTimeProperty)
        {
            var coerced = Coerce(SelectedDateTime);
            if (coerced != SelectedDateTime)
            {
                SetCurrentValue(SelectedDateTimeProperty, coerced);
                return;
            }
            PushToParts();
            SelectedDateTimeChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty ||
                 change.Property == DateFormatProperty || change.Property == ClockIdentifierProperty ||
                 change.Property == MinuteIncrementProperty)
        {
            if (change.Property == MinuteIncrementProperty && MinuteIncrement != EffectiveMinuteIncrement)
            {
                SetCurrentValue(MinuteIncrementProperty, EffectiveMinuteIncrement);
                return;
            }
            var coerced = Coerce(SelectedDateTime);
            if (coerced != SelectedDateTime)
                SetCurrentValue(SelectedDateTimeProperty, coerced);
            PushToParts();
        }
    }

    private DateTimeOffset? Coerce(DateTimeOffset? value)
    {
        if (value is null)
            return null;
        var result = value.Value;
        if (Minimum is { } minimum && result < minimum)
            result = minimum;
        if (Maximum is { } maximum && result > maximum)
            result = maximum;

        var (minimumTime, maximumTime) = GetTimeBounds(result);
        var time = TimeMath.Normalize(
            result.TimeOfDay, EffectiveMinuteIncrement, minimumTime, maximumTime);
        result = new DateTimeOffset(result.Date + time, result.Offset);

        if (Minimum is { } finalMinimum && result < finalMinimum)
            result = finalMinimum;
        if (Maximum is { } finalMaximum && result > finalMaximum)
            result = finalMaximum;
        return result;
    }

    private (TimeSpan Minimum, TimeSpan Maximum) GetTimeBounds(DateTimeOffset date)
    {
        // Express both absolute limits in the selected offset before rounding.
        // The implicit UTC limits also constrain local times in years 1 and 9999.
        var origin = date.Date.Ticks;
        var minimum = Math.Clamp((Minimum?.UtcTicks ?? DateTime.MinValue.Ticks) + date.Offset.Ticks - origin,
            0, TimeSpan.TicksPerDay - 1);
        var maximum = Math.Clamp((Maximum?.UtcTicks ?? DateTime.MaxValue.Ticks) + date.Offset.Ticks - origin,
            0, TimeSpan.TicksPerDay - 1);
        return (TimeSpan.FromTicks(Math.Min(minimum, maximum)), TimeSpan.FromTicks(maximum));
    }

    private int EffectiveMinuteIncrement => DateMath.ClampMinuteIncrement(MinuteIncrement);
}
