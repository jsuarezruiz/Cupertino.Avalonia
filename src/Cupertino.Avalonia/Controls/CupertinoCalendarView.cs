using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Renders a month grid for compact date pickers.
/// </summary>
public class CupertinoMonthGrid : Control
{
    public static readonly StyledProperty<DateTime> DisplayMonthProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTime>(nameof(DisplayMonth), DateTime.Today);

    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(Foreground), Brushes.Black);

    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(AccentBrush), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush> SelectionBrushProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(SelectionBrush), Brushes.Transparent);

    public static readonly StyledProperty<IBrush> SelectionForegroundProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(SelectionForeground), Brushes.White);

    public static readonly StyledProperty<IBrush> WeekdayBrushProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(WeekdayBrush), Brushes.Gray);

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Monday);

    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTimeOffset?>(nameof(MinimumDate));

    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTimeOffset?>(nameof(MaximumDate));

    public DateTime DisplayMonth { get => GetValue(DisplayMonthProperty); set => SetValue(DisplayMonthProperty, value); }
    public DateTimeOffset? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public IBrush SelectionForeground { get => GetValue(SelectionForegroundProperty); set => SetValue(SelectionForegroundProperty, value); }
    public IBrush WeekdayBrush { get => GetValue(WeekdayBrushProperty); set => SetValue(WeekdayBrushProperty, value); }
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    public DateTimeOffset? MinimumDate { get => GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    public DateTimeOffset? MaximumDate { get => GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }

    // Scale all geometry from the square cell size.
    private const double DiscRatio = 1.0;
    private const double DayFontRatio = 0.48;
    private const double WeekdayFontRatio = 0.30;
    // Weekday-band height relative to a day cell.
    private const double WeekdayRowRatio = 0.314;

    private double Cell => Bounds.Width / 7;
    private double WeekdayRowHeight => Cell * WeekdayRowRatio;

    static CupertinoMonthGrid()
    {
        AffectsRender<CupertinoMonthGrid>(DisplayMonthProperty, SelectedDateProperty,
                                          ForegroundProperty, AccentBrushProperty,
                                          SelectionBrushProperty, SelectionForegroundProperty,
                                          WeekdayBrushProperty, FirstDayOfWeekProperty,
                                          MinimumDateProperty, MaximumDateProperty);
        FocusableProperty.OverrideDefaultValue<CupertinoMonthGrid>(true);
    }

    /// <summary>
    /// Raised when a day is tapped.
    /// </summary>
    public event EventHandler<DateTime>? DayPicked;

    private int ColumnOf(DateTime date) => ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;

    private int RowCount()
    {
        var first = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        return (int)Math.Ceiling((ColumnOf(first) + days) / 7.0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 7 * 55.9 : availableSize.Width;
        var cell = width / 7;
        return new Size(width, cell * WeekdayRowRatio + RowCount() * cell);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        var cell = Cell;
        var colPitch = cell;
        var weekdayRow = cell * WeekdayRowRatio;
        var culture = CultureInfo.CurrentCulture;
        var typeface = new Typeface(TextElement.GetFontFamily(this));
        var bold = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.SemiBold);

        var abbrev = culture.DateTimeFormat.AbbreviatedDayNames;
        for (var c = 0; c < 7; c++)
        {
            var dow = (DayOfWeek)(((int)FirstDayOfWeek + c) % 7);
            var ft = new FormattedText(abbrev[(int)dow].ToUpper(culture), culture,
                                       FlowDirection, typeface,
                                       cell * WeekdayFontRatio, WeekdayBrush);
            context.DrawText(ft, new Point(colPitch * (c + 0.5) - ft.Width / 2,
                                           weekdayRow / 2 - ft.Height / 2));
        }

        var first = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        var today = DateTime.Today;
        var selected = SelectedDate?.Date;

        for (var d = 1; d <= days; d++)
        {
            var date = new DateTime(first.Year, first.Month, d);
            var index = ColumnOf(first) + d - 1;
            var cx = colPitch * (index % 7 + 0.5);
            var cy = weekdayRow + cell * (index / 7 + 0.5);

            var isSelected = selected == date;
            var isToday = today == date;

            if (isSelected)
            {
                var r = cell * DiscRatio / 2;
                context.DrawEllipse(SelectionBrush, null, new Point(cx, cy), r, r);
            }

            var isEnabled = IsDateEnabled(date);
            var brush = isSelected ? SelectionForeground
                : !isEnabled ? WeekdayBrush
                : isToday ? AccentBrush : Foreground;
            var face = isSelected ? bold : typeface;
            var ft = new FormattedText(d.ToString(culture), culture, FlowDirection,
                                       face, cell * DayFontRatio, brush);
            context.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }
    }

    private DateTime? DateAt(Point p)
    {
        if (p.Y < WeekdayRowHeight)
            return null;

        var visualCol = (int)(p.X / Cell);
        var row = (int)((p.Y - WeekdayRowHeight) / Cell);
        if (visualCol is < 0 or > 6 || row < 0)
            return null;

        // Avalonia already mirrors local coordinates in RTL.
        var col = visualCol;

        var first = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        var day = row * 7 + col - ColumnOf(first) + 1;
        if (day < 1 || day > DateTime.DaysInMonth(first.Year, first.Month))
            return null;

        var date = new DateTime(first.Year, first.Month, day);
        return IsDateEnabled(date) ? date : null;
    }

    private bool IsDateEnabled(DateTime date) =>
        (MinimumDate is null || date >= MinimumDate.Value.Date) &&
        (MaximumDate is null || date <= MaximumDate.Value.Date);

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (DateAt(e.GetPosition(this)) is not { } date)
            return;

        SetCurrentValue(SelectedDateProperty, new DateTimeOffset(date));
        DayPicked?.Invoke(this, date);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.Key switch
        {
            Key.Left => FlowDirection == FlowDirection.RightToLeft ? 1 : -1,
            Key.Right => FlowDirection == FlowDirection.RightToLeft ? -1 : 1,
            Key.Up => -7,
            Key.Down => 7,
            _ => 0,
        };
        if (step == 0)
            return;

        var current = SelectedDate?.Date ?? new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        var nextTicks = Math.Clamp(
            current.Date.Ticks + step * TimeSpan.TicksPerDay,
            DateTime.MinValue.Ticks,
            DateTime.MaxValue.Date.Ticks);
        var next = new DateTime(nextTicks, current.Kind);
        if (MinimumDate is { } minimum && next < minimum.Date)
            next = minimum.Date;
        if (MaximumDate is { } maximum && next > maximum.Date)
            next = maximum.Date;
        SetCurrentValue(SelectedDateProperty, new DateTimeOffset(next));
        SetCurrentValue(DisplayMonthProperty, new DateTime(next.Year, next.Month, 1));
        e.Handled = true;
    }
}

/// <summary>
/// A navigable calendar view for compact date pickers.
/// </summary>
[TemplatePart("PART_Grid", typeof(CupertinoMonthGrid))]
[TemplatePart("PART_MonthWheel", typeof(CupertinoWheel))]
[TemplatePart("PART_NextButton", typeof(Button))]
[TemplatePart("PART_PreviousButton", typeof(Button))]
[TemplatePart("PART_Title", typeof(TextBlock))]
[TemplatePart("PART_TitleButton", typeof(Button))]
[TemplatePart("PART_YearWheel", typeof(CupertinoWheel))]
[PseudoClasses(":monthpicker")]
public class CupertinoCalendarView : TemplatedControl
{
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<DateTime> DisplayMonthProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTime>(nameof(DisplayMonth), DateTime.Today);

    /// <summary>
    /// Gets or sets whether month and year wheels are shown.
    /// </summary>
    public static readonly StyledProperty<bool> IsMonthPickerOpenProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, bool>(nameof(IsMonthPickerOpen));

    public DateTimeOffset? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public DateTime DisplayMonth { get => GetValue(DisplayMonthProperty); set => SetValue(DisplayMonthProperty, value); }
    public bool IsMonthPickerOpen { get => GetValue(IsMonthPickerOpenProperty); set => SetValue(IsMonthPickerOpenProperty, value); }

    /// <summary>
    /// Gets or sets the first available year.
    /// </summary>
    public static readonly StyledProperty<int> MinYearProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, int>(nameof(MinYear), 1900);

    public static readonly StyledProperty<int> MaxYearProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, int>(nameof(MaxYear), 2100);

    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTimeOffset?>(nameof(MinimumDate));

    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTimeOffset?>(nameof(MaximumDate));

    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Monday);

    public int MinYear { get => GetValue(MinYearProperty); set => SetValue(MinYearProperty, value); }
    public int MaxYear { get => GetValue(MaxYearProperty); set => SetValue(MaxYearProperty, value); }
    public DateTimeOffset? MinimumDate { get => GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    public DateTimeOffset? MaximumDate { get => GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>
    /// Raised when a day is selected.
    /// </summary>
    public event EventHandler<DateTime>? DayPicked;

    private CupertinoMonthGrid? _grid;
    private TextBlock? _title;
    private CupertinoWheel? _monthWheel, _yearWheel;
    private Button? _previousButton, _nextButton, _titleButton;
    private bool _syncingWheels;
    private bool _normalizingYearRange;
    private bool _normalizingDateRange;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_grid is not null)
            _grid.DayPicked -= OnDayPicked;
        if (_monthWheel is not null)
            _monthWheel.SelectionSettled -= OnWheelSettled;
        if (_yearWheel is not null)
            _yearWheel.SelectionSettled -= OnWheelSettled;
        if (_previousButton is not null)
            _previousButton.Click -= OnPreviousClick;
        if (_nextButton is not null)
            _nextButton.Click -= OnNextClick;
        if (_titleButton is not null)
            _titleButton.Click -= OnTitleClick;

        _grid = e.NameScope.Find<CupertinoMonthGrid>("PART_Grid");
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        if (_grid is not null)
        {
            _grid.MinimumDate = MinimumDate;
            _grid.MaximumDate = MaximumDate;
            _grid.FirstDayOfWeek = FirstDayOfWeek;
            _grid.DayPicked += OnDayPicked;
        }

        _previousButton = e.NameScope.Find<Button>("PART_PreviousButton");
        _nextButton = e.NameScope.Find<Button>("PART_NextButton");
        _titleButton = e.NameScope.Find<Button>("PART_TitleButton");
        if (_previousButton is not null)
            _previousButton.Click += OnPreviousClick;
        if (_nextButton is not null)
            _nextButton.Click += OnNextClick;
        if (_titleButton is not null)
            _titleButton.Click += OnTitleClick;

        _monthWheel = e.NameScope.Find<CupertinoWheel>("PART_MonthWheel");
        if (_monthWheel is not null)
        {
            _monthWheel.Items = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames
                                           .Take(12).ToList();
            _monthWheel.SelectionSettled += OnWheelSettled;
        }

        _yearWheel = e.NameScope.Find<CupertinoWheel>("PART_YearWheel");
        if (_yearWheel is not null)
        {
            _yearWheel.ShouldLoop = false;
            _yearWheel.SelectionSettled += OnWheelSettled;
            RebuildYearWheel();
        }

        PushToWheels();
        UpdateTitle();
        UpdateNavigationButtons();
        PseudoClasses.Set(":monthpicker", IsMonthPickerOpen);
    }

    private void OnPreviousClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Step(-1);

    private void OnNextClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Step(1);

    private void OnTitleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        SetCurrentValue(IsMonthPickerOpenProperty, !IsMonthPickerOpen);

    private void PushToWheels()
    {
        _syncingWheels = true;
        try
        {
            if (_monthWheel is not null)
                _monthWheel.SelectedIndex = DisplayMonth.Month - 1;
            if (_yearWheel is not null)
                _yearWheel.SelectedIndex = Math.Clamp(DisplayMonth.Year - MinYear, 0, MaxYear - MinYear);
        }
        finally { _syncingWheels = false; }
    }

    private void OnWheelSettled(object? sender, EventArgs e)
    {
        if (_syncingWheels || _monthWheel is null || _yearWheel is null)
            return;

        SetCurrentValue(DisplayMonthProperty,
                        new DateTime(MinYear + _yearWheel.SelectedIndex,
                                     _monthWheel.SelectedIndex + 1, 1));
    }

    private void OnDayPicked(object? sender, DateTime date) => DayPicked?.Invoke(this, date);

    private void Step(int months)
    {
        var (minimum, maximum) = EffectiveMonthRange();
        var target = Math.Clamp(
            MonthOrdinal(DisplayMonth) + months,
            MonthOrdinal(minimum),
            MonthOrdinal(maximum));
        SetCurrentValue(DisplayMonthProperty, MonthFromOrdinal(target, DisplayMonth.Kind));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DisplayMonthProperty)
        {
            var coerced = CoerceDisplayMonth(DisplayMonth);
            if (coerced != DisplayMonth)
            {
                SetCurrentValue(DisplayMonthProperty, coerced);
                return;
            }
            UpdateTitle();
            PushToWheels();
            UpdateNavigationButtons();
        }
        else if (change.Property == IsMonthPickerOpenProperty)
        {
            PseudoClasses.Set(":monthpicker", IsMonthPickerOpen);
            if (IsMonthPickerOpen)
                PushToWheels();
        }
        else if (change.Property == SelectedDateProperty && SelectedDate is { } d)
        {
            var coerced = CoerceSelectedDate(d);
            if (coerced != SelectedDate)
            {
                SetCurrentValue(SelectedDateProperty, coerced);
                return;
            }
            // Keep the displayed month aligned with external selections.
            SetCurrentValue(DisplayMonthProperty, new DateTime(d.Year, d.Month, 1));
        }
        else if (change.Property == MinimumDateProperty || change.Property == MaximumDateProperty ||
                 change.Property == FirstDayOfWeekProperty)
        {
            if ((change.Property == MinimumDateProperty || change.Property == MaximumDateProperty)
                && !_normalizingDateRange)
                NormalizeDateRange(change.Property);
            UpdateDateConstraints();
        }
        else if ((change.Property == MinYearProperty || change.Property == MaxYearProperty) &&
                 !_normalizingYearRange)
        {
            NormalizeYearRange(change.Property);
        }
    }

    private void NormalizeYearRange(AvaloniaProperty changedProperty)
    {
        var min = Math.Clamp(MinYear, DateTime.MinValue.Year, DateTime.MaxValue.Year);
        var max = Math.Clamp(MaxYear, DateTime.MinValue.Year, DateTime.MaxValue.Year);
        if (min > max)
        {
            if (changedProperty == MinYearProperty)
                max = min;
            else
                min = max;
        }

        _normalizingYearRange = true;
        try
        {
            if (MinYear != min)
                SetCurrentValue(MinYearProperty, min);
            if (MaxYear != max)
                SetCurrentValue(MaxYearProperty, max);
        }
        finally
        {
            _normalizingYearRange = false;
        }

        RebuildYearWheel();
        var selected = CoerceSelectedDate(SelectedDate);
        if (selected != SelectedDate)
            SetCurrentValue(SelectedDateProperty, selected);
        var display = CoerceDisplayMonth(DisplayMonth);
        if (display != DisplayMonth)
            SetCurrentValue(DisplayMonthProperty, display);
        else
            PushToWheels();
    }

    private void RebuildYearWheel()
    {
        if (_yearWheel is null)
            return;

        _yearWheel.Items = Enumerable.Range(MinYear, MaxYear - MinYear + 1)
                                     .Select(y => y.ToString(CultureInfo.CurrentCulture)).ToList();
    }

    private DateTime CoerceDisplayMonth(DateTime value)
    {
        var (minimum, maximum) = EffectiveMonthRange();
        var month = Math.Clamp(
            MonthOrdinal(value), MonthOrdinal(minimum), MonthOrdinal(maximum));
        return MonthFromOrdinal(month, value.Kind);
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

    private void UpdateDateConstraints()
    {
        if (_grid is not null)
        {
            _grid.MinimumDate = MinimumDate;
            _grid.MaximumDate = MaximumDate;
            _grid.FirstDayOfWeek = FirstDayOfWeek;
        }

        var selected = CoerceSelectedDate(SelectedDate);
        if (selected != SelectedDate)
            SetCurrentValue(SelectedDateProperty, selected);

        var display = CoerceDisplayMonth(DisplayMonth);
        if (display != DisplayMonth)
            SetCurrentValue(DisplayMonthProperty, display);
        else
            UpdateNavigationButtons();
    }

    private DateTimeOffset? CoerceSelectedDate(DateTimeOffset? value)
    {
        if (value is not { } selected)
            return null;

        var (minimumMonth, maximumMonth) = EffectiveMonthRange();
        var minimum = MinimumDate?.Date ?? minimumMonth;
        if (minimum < minimumMonth)
            minimum = minimumMonth;
        var maximum = MaximumDate?.Date
                      ?? new DateTime(maximumMonth.Year, maximumMonth.Month,
                          DateTime.DaysInMonth(maximumMonth.Year, maximumMonth.Month));
        var maximumMonthEnd = new DateTime(maximumMonth.Year, maximumMonth.Month,
            DateTime.DaysInMonth(maximumMonth.Year, maximumMonth.Month));
        if (maximum > maximumMonthEnd)
            maximum = maximumMonthEnd;
        if (minimum > maximum)
            minimum = maximum;

        if (selected.Date < minimum)
            return DateAtOffset(minimum, selected.Offset);
        if (selected.Date > maximum)
            return DateAtOffset(maximum, selected.Offset);
        return selected;
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

    private (DateTime Minimum, DateTime Maximum) EffectiveMonthRange()
    {
        var yearMinimum = new DateTime(MinYear, 1, 1);
        var yearMaximum = new DateTime(MaxYear, 12, 1);
        var minimum = yearMinimum;
        var maximum = yearMaximum;

        if (MinimumDate is { } minimumDate)
        {
            var dateMonth = new DateTime(minimumDate.Year, minimumDate.Month, 1);
            if (dateMonth > minimum)
                minimum = dateMonth;
        }
        if (MaximumDate is { } maximumDate)
        {
            var dateMonth = new DateTime(maximumDate.Year, maximumDate.Month, 1);
            if (dateMonth < maximum)
                maximum = dateMonth;
        }

        if (minimum > maximum)
        {
            var collapsed = minimum > yearMaximum ? yearMaximum : yearMinimum;
            return (collapsed, collapsed);
        }
        return (minimum, maximum);
    }

    private void UpdateNavigationButtons()
    {
        var (minimum, maximum) = EffectiveMonthRange();
        var current = MonthOrdinal(DisplayMonth);
        if (_previousButton is not null)
            _previousButton.IsEnabled = current > MonthOrdinal(minimum);
        if (_nextButton is not null)
            _nextButton.IsEnabled = current < MonthOrdinal(maximum);
    }

    private static int MonthOrdinal(DateTime value) => (value.Year - 1) * 12 + value.Month - 1;

    private static DateTime MonthFromOrdinal(int ordinal, DateTimeKind kind) =>
        new(ordinal / 12 + 1, ordinal % 12 + 1, 1, 0, 0, 0, kind);

    private void UpdateTitle()
    {
        if (_title is null)
            return;
        _title.Text = DisplayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }
}
