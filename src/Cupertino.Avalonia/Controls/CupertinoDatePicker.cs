using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cupertino.Controls;

public enum CupertinoPickerDisplayMode
{
    Compact,
    Inline,
}

/// <summary>
/// A compact date field that opens a calendar popover.
/// </summary>
[TemplatePart("PART_FlyoutButton", typeof(Button))]
[PseudoClasses(":dropdownopen", ":inline")]
public class CupertinoDatePicker : TemplatedControl
{
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Format of the capsule label; <c>d MMM yyyy</c> in the current culture by default.
    /// </summary>
    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, string?>(nameof(DateFormat));

    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, bool>(nameof(IsDropDownOpen));

    public static readonly StyledProperty<CupertinoPickerDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, CupertinoPickerDisplayMode>(nameof(DisplayMode));

    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DateTimeOffset?>(nameof(MinimumDate));

    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DateTimeOffset?>(nameof(MaximumDate));

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DayOfWeek>(nameof(FirstDayOfWeek),
            CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

    public DateTimeOffset? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
    public CupertinoPickerDisplayMode DisplayMode { get => GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    public DateTimeOffset? MinimumDate { get => GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    public DateTimeOffset? MaximumDate { get => GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>
    /// Text shown on the capsule.
    /// </summary>
    public string DisplayText => SelectedDate is { } d
        ? d.ToString(DateFormat ?? "d MMM yyyy", CultureInfo.CurrentCulture)
        : "Select";

    public static readonly DirectProperty<CupertinoDatePicker, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<CupertinoDatePicker, string>(
            nameof(DisplayText), o => o.DisplayText);

    private Button? _field;
    private bool _normalizingDateRange;

    public event EventHandler? SelectedDateChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _field = e.NameScope.Find<Button>("PART_FlyoutButton");
        if (_field is not null)
            _field.Click += (_, _) => SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen);
        PseudoClasses.Set(":inline", DisplayMode == CupertinoPickerDisplayMode.Inline);
    }

    private void OnDayPicked(object? sender, DateTime date) =>
        SetCurrentValue(IsDropDownOpenProperty, false);

    private void ShowPopover()
    {
        if (_field is null)
            return;

        var calendar = new CupertinoCalendarView
        {
            Width = 330,
            SelectedDate = SelectedDate,
            MinimumDate = MinimumDate,
            MaximumDate = MaximumDate,
            FirstDayOfWeek = FirstDayOfWeek,
        };
        calendar.DayPicked += OnDayPicked;
        calendar.PropertyChanged += (_, args) =>
        {
            if (args.Property == CupertinoCalendarView.SelectedDateProperty)
                SetCurrentValue(SelectedDateProperty, calendar.SelectedDate);
        };

        CupertinoPopover.Show(_field, calendar, 30,
                              () => SetCurrentValue(IsDropDownOpenProperty, false));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsDropDownOpenProperty)
        {
            PseudoClasses.Set(":dropdownopen", IsDropDownOpen);
            if (IsDropDownOpen)
                ShowPopover();
            else if (_field is not null) CupertinoPopover.Close(_field);
        }

        if (change.Property == SelectedDateProperty)
        {
            var coerced = CoerceDate(SelectedDate);
            if (coerced != SelectedDate)
            {
                SetCurrentValue(SelectedDateProperty, coerced);
                return;
            }
            RaisePropertyChanged(DisplayTextProperty, string.Empty, DisplayText);
            SelectedDateChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == DateFormatProperty)
            RaisePropertyChanged(DisplayTextProperty, string.Empty, DisplayText);
        else if (change.Property == DisplayModeProperty)
        {
            PseudoClasses.Set(":inline", DisplayMode == CupertinoPickerDisplayMode.Inline);
            if (DisplayMode == CupertinoPickerDisplayMode.Inline)
                SetCurrentValue(IsDropDownOpenProperty, false);
        }
        else if (change.Property == MinimumDateProperty || change.Property == MaximumDateProperty)
        {
            if (!_normalizingDateRange)
                NormalizeDateRange(change.Property);
            var coerced = CoerceDate(SelectedDate);
            if (coerced != SelectedDate)
                SetCurrentValue(SelectedDateProperty, coerced);
        }
    }

    private void NormalizeDateRange(AvaloniaProperty changedProperty)
    {
        if (MinimumDate is not { } minimum || MaximumDate is not { } maximum
            || minimum.Date <= maximum.Date)
            return;

        _normalizingDateRange = true;
        try
        {
            if (changedProperty == MinimumDateProperty)
                SetCurrentValue(MaximumDateProperty, minimum);
            else
                SetCurrentValue(MinimumDateProperty, maximum);
        }
        finally
        {
            _normalizingDateRange = false;
        }
    }

    private DateTimeOffset? CoerceDate(DateTimeOffset? value)
    {
        if (value is null)
            return null;
        if (MinimumDate is { } minimum && value.Value.Date < minimum.Date)
            return DateAtOffset(minimum.Date, value.Value.Offset);
        if (MaximumDate is { } maximum && value.Value.Date > maximum.Date)
            return DateAtOffset(maximum.Date, value.Value.Offset);
        return value;
    }

    private static DateTimeOffset DateAtOffset(DateTime date, TimeSpan preferredOffset)
    {
        try
        {
            return new DateTimeOffset(date, preferredOffset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new DateTimeOffset(date, TimeSpan.Zero);
        }
    }
}
