using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Cupertino.Controls;

/// <summary>
/// Chooses how a date or time editor is presented.
/// </summary>
public enum CupertinoPickerDisplayMode
{
    /// <summary>
    /// A capsule field opens the editor in a popover.
    /// </summary>
    Compact,
    /// <summary>
    /// The editor is displayed in the normal layout.
    /// </summary>
    Inline,
}

/// <summary>
/// A compact date field that opens a calendar popover.
/// </summary>
[TemplatePart("PART_FlyoutButton", typeof(Button))]
[TemplatePart("PART_InlineCalendar", typeof(CupertinoCalendarView))]
[PseudoClasses(":dropdownopen", ":inline")]
public class CupertinoDatePicker : TemplatedControl
{
    /// <summary>
    /// Identifies the <see cref="SelectedDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Format of the capsule label; <c>d MMM yyyy</c> in the current culture by default.
    /// </summary>
    public static readonly StyledProperty<string?> DateFormatProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, string?>(nameof(DateFormat));

    /// <summary>
    /// Identifies the <see cref="IsDropDownOpen"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, bool>(nameof(IsDropDownOpen));

    /// <summary>
    /// Identifies the <see cref="DisplayMode"/> property.
    /// </summary>
    public static readonly StyledProperty<CupertinoPickerDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, CupertinoPickerDisplayMode>(nameof(DisplayMode));

    /// <summary>
    /// Identifies the <see cref="MinimumDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DateTimeOffset?>(nameof(MinimumDate));

    /// <summary>
    /// Identifies the <see cref="MaximumDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DateTimeOffset?>(nameof(MaximumDate));

    /// <summary>
    /// Identifies the <see cref="FirstDayOfWeek"/> property.
    /// </summary>
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<CupertinoDatePicker, DayOfWeek>(nameof(FirstDayOfWeek),
            CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);

    /// <summary>
    /// The selected calendar date, or null for no selection. Date bounds compare local calendar dates rather than UTC instants.
    /// </summary>
    public DateTimeOffset? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    /// <summary>
    /// The date label format string, or null to use d MMM yyyy with the current culture.
    /// </summary>
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }
    /// <summary>
    /// The requested popover state. A true value set before attachment opens once the template and overlay are available.
    /// </summary>
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
    /// <summary>
    /// Chooses an inline editor or a compact field with a popover.
    /// </summary>
    public CupertinoPickerDisplayMode DisplayMode { get => GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }
    /// <summary>
    /// The earliest selectable local calendar date, or null for no explicit lower bound.
    /// </summary>
    public DateTimeOffset? MinimumDate { get => GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    /// <summary>
    /// The latest selectable local calendar date, or null for no explicit upper bound.
    /// </summary>
    public DateTimeOffset? MaximumDate { get => GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    /// <summary>
    /// The weekday in the first calendar column; defaults to the current culture when the control type is initialized.
    /// </summary>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>
    /// Text shown on the capsule.
    /// </summary>
    public string DisplayText => SelectedDate is { } d
        ? d.ToString(DateFormat ?? "d MMM yyyy", CultureInfo.CurrentCulture)
        : "Select";

    /// <summary>
    /// Identifies the <see cref="DisplayText"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoDatePicker, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<CupertinoDatePicker, string>(
            nameof(DisplayText), o => o.DisplayText);

    private Button? _field;
    private int _popoverVersion;
    private CupertinoCalendarView? _calendar;
    private CupertinoCalendarView? _inlineCalendar;
    private bool _syncingCalendar;
    private bool _normalizingDateRange;

    /// <summary>
    /// Raised after the selected date changes and has been coerced to the current bounds.
    /// </summary>
    public event EventHandler? SelectedDateChanged;

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ResetPopover();
        if (_field is not null)
            _field.Click -= OnFieldClick;
        if (_inlineCalendar is not null)
            _inlineCalendar.PropertyChanged -= OnCalendarPropertyChanged;
        base.OnApplyTemplate(e);

        _field = e.NameScope.Find<Button>("PART_FlyoutButton");
        if (_field is not null)
            _field.Click += OnFieldClick;
        _inlineCalendar = e.NameScope.Find<CupertinoCalendarView>("PART_InlineCalendar");
        if (_inlineCalendar is not null)
            _inlineCalendar.PropertyChanged += OnCalendarPropertyChanged;
        SyncCalendar();
        PseudoClasses.Set(":inline", DisplayMode == CupertinoPickerDisplayMode.Inline);
        QueueOpen();
    }

    private void OnDayPicked(object? sender, DateTime date) =>
        SetCurrentValue(IsDropDownOpenProperty, false);

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
        if (_calendar is not null)
        {
            _calendar.DayPicked -= OnDayPicked;
            _calendar.PropertyChanged -= OnCalendarPropertyChanged;
            _calendar = null;
        }
    }

    private void ShowPopover()
    {
        if (_field is null || TopLevel.GetTopLevel(_field) is null ||
            DisplayMode != CupertinoPickerDisplayMode.Compact)
            return;
        if (_calendar is not null && CupertinoPopover.IsOpen(_field) && !CupertinoPopover.IsClosing(_field))
            return;
        ResetPopover();
        var version = _popoverVersion;
        _calendar = new CupertinoCalendarView { Width = 330 };
        SyncCalendar();
        _calendar.DayPicked += OnDayPicked;
        _calendar.PropertyChanged += OnCalendarPropertyChanged;
        CupertinoPopover.Show(_field, _calendar, 30, () =>
        {
            if (version != _popoverVersion)
                return;
            if (_calendar is not null)
            {
                _calendar.DayPicked -= OnDayPicked;
                _calendar.PropertyChanged -= OnCalendarPropertyChanged;
                _calendar = null;
            }
            SetCurrentValue(IsDropDownOpenProperty, false);
        });
    }

    private void OnCalendarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_syncingCalendar && e.Property == CupertinoCalendarView.SelectedDateProperty
            && sender is CupertinoCalendarView calendar)
            SetCurrentValue(SelectedDateProperty, calendar.SelectedDate);
    }

    private void SyncCalendar()
    {
        if (_syncingCalendar)
            return;
        _syncingCalendar = true;
        try
        {
            SynchronizeCalendar(_calendar);
            SynchronizeCalendar(_inlineCalendar);
        }
        finally { _syncingCalendar = false; }
        if (_field is not null)
            CupertinoPopover.Reposition(_field);
    }

    private void SynchronizeCalendar(CupertinoCalendarView? calendar)
    {
        if (calendar is null)
            return;
        // Set the range before selection, including for the hidden inline part;
        // its default 1900–2100 range must not coerce the owner's selected date.
        calendar.MinimumDate = null;
        calendar.MaximumDate = null;
        calendar.MinYear = Math.Min(1900, Math.Min(SelectedDate?.Year ?? 1900, MinimumDate?.Year ?? 1900));
        calendar.MaxYear = Math.Max(2100, Math.Max(SelectedDate?.Year ?? 2100, MaximumDate?.Year ?? 2100));
        calendar.MinimumDate = MinimumDate;
        calendar.MaximumDate = MaximumDate;
        calendar.FirstDayOfWeek = FirstDayOfWeek;
        calendar.SelectedDate = SelectedDate;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsDropDownOpenProperty)
        {
            PseudoClasses.Set(":dropdownopen", IsDropDownOpen);
            if (IsDropDownOpen)
                ShowPopover();
            else if (_field is not null)
                CupertinoPopover.Close(_field);
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
        if (change.Property == SelectedDateProperty || change.Property == MinimumDateProperty ||
            change.Property == MaximumDateProperty || change.Property == FirstDayOfWeekProperty)
            SyncCalendar();
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
