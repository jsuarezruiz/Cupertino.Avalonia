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
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateTimeProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, DateTimeOffset?>(
            nameof(SelectedDateTime), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<DateTimeOffset?> MinimumProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, DateTimeOffset?>(nameof(Minimum));

    public static readonly StyledProperty<DateTimeOffset?> MaximumProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, DateTimeOffset?>(nameof(Maximum));

    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, string?>(nameof(DateFormat));

    public static readonly StyledProperty<string?> ClockIdentifierProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, string?>(nameof(ClockIdentifier));

    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<CupertinoDateTimePicker, int>(nameof(MinuteIncrement), 1);

    public DateTimeOffset? SelectedDateTime { get => GetValue(SelectedDateTimeProperty); set => SetValue(SelectedDateTimeProperty, value); }
    public DateTimeOffset? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public DateTimeOffset? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }
    public string? ClockIdentifier { get => GetValue(ClockIdentifierProperty); set => SetValue(ClockIdentifierProperty, value); }
    public int MinuteIncrement { get => GetValue(MinuteIncrementProperty); set => SetValue(MinuteIncrementProperty, value); }

    public event EventHandler? SelectedDateTimeChanged;

    private CupertinoDatePicker? _date;
    private CupertinoTimePicker? _time;
    private bool _syncing;

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
                _date.SelectedDate = SelectedDateTime;
                _date.MinimumDate = Minimum;
                _date.MaximumDate = Maximum;
                _date.DateFormat = DateFormat;
            }
            if (_time is not null)
            {
                _time.ClockIdentifier = ClockIdentifier;
                _time.MinuteIncrement = MinuteIncrement;
                if (SelectedDateTime?.Date == Minimum?.Date)
                    _time.MinimumTime = Minimum?.TimeOfDay;
                else
                    _time.MinimumTime = null;
                if (SelectedDateTime?.Date == Maximum?.Date)
                    _time.MaximumTime = Maximum?.TimeOfDay;
                else
                    _time.MaximumTime = null;
                _time.SelectedTime = SelectedDateTime?.TimeOfDay;
            }
        }
        finally { _syncing = false; }
    }

    private void OnDateChanged(object? sender, EventArgs e)
    {
        if (_syncing || _date?.SelectedDate is not { } date)
            return;
        var time = _time?.SelectedTime ?? SelectedDateTime?.TimeOfDay ?? TimeSpan.Zero;
        SetCurrentValue(SelectedDateTimeProperty, new DateTimeOffset(date.Date + time, date.Offset));
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        if (_syncing || _time?.SelectedTime is not { } time)
            return;
        var date = _date?.SelectedDate ?? SelectedDateTime ?? DateTimeOffset.Now;
        SetCurrentValue(SelectedDateTimeProperty, new DateTimeOffset(date.Date + time, date.Offset));
    }

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

        var dayStart = new DateTimeOffset(result.Date, result.Offset);
        var dayEnd = dayStart.AddDays(1) - TimeSpan.FromTicks(1);
        var minimumTime = Minimum is { } min && min.Date == result.Date
            ? min.TimeOfDay
            : TimeSpan.Zero;
        var maximumTime = Maximum is { } max && max.Date == result.Date
            ? max.TimeOfDay
            : dayEnd.TimeOfDay;
        if (minimumTime > maximumTime)
            minimumTime = maximumTime;

        var time = CupertinoTimePicker.NormalizeTimeValue(
            result.TimeOfDay, EffectiveMinuteIncrement, minimumTime, maximumTime);
        result = new DateTimeOffset(result.Date + time, result.Offset);

        if (Minimum is { } finalMinimum && result < finalMinimum)
            result = finalMinimum;
        if (Maximum is { } finalMaximum && result > finalMaximum)
            result = finalMaximum;
        return result;
    }

    private int EffectiveMinuteIncrement => Math.Clamp(MinuteIncrement, 1, 59);
}
