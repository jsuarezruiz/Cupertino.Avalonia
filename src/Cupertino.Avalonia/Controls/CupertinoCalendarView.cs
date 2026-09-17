using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Cupertino.Controls;

/// <summary>
/// Renders a month grid for compact date pickers.
/// </summary>
/// <remarks>
/// Days are laid out on the proleptic Gregorian calendar. Weekday names, month names and the
/// first day of the week follow the current culture; non-Gregorian calendar systems do not.
/// </remarks>
public class CupertinoMonthGrid : Control
{
    /// <summary>
    /// Identifies the <see cref="DisplayMonth"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTime> DisplayMonthProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTime>(nameof(DisplayMonth), DateTime.Today);

    /// <summary>
    /// Identifies the <see cref="SelectedDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="Foreground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(Foreground), Brushes.Black);

    /// <summary>
    /// Identifies the <see cref="AccentBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> AccentBrushProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(AccentBrush), Brushes.DodgerBlue);

    /// <summary>
    /// Identifies the <see cref="SelectionBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectionBrushProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(SelectionBrush), Brushes.Transparent);

    /// <summary>
    /// Identifies the <see cref="SelectionForeground"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectionForegroundProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(SelectionForeground), Brushes.White);

    /// <summary>
    /// Identifies the <see cref="WeekdayBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> WeekdayBrushProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, IBrush>(nameof(WeekdayBrush), Brushes.Gray);

    /// <summary>
    /// Identifies the <see cref="FirstDayOfWeek"/> property.
    /// </summary>
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DayOfWeek>(nameof(FirstDayOfWeek),
            DateMath.CultureFirstDayOfWeek);

    /// <summary>
    /// Identifies the <see cref="MinimumDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTimeOffset?>(nameof(MinimumDate));

    /// <summary>
    /// Identifies the <see cref="MaximumDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<CupertinoMonthGrid, DateTimeOffset?>(nameof(MaximumDate));

    /// <summary>
    /// A date in the displayed month; the day component does not change the month layout.
    /// </summary>
    public DateTime DisplayMonth { get => GetValue(DisplayMonthProperty); set => SetValue(DisplayMonthProperty, value); }
    /// <summary>
    /// The selected calendar date, or null for no selection. Date bounds compare local calendar dates rather than UTC instants.
    /// </summary>
    public DateTimeOffset? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    /// <summary>
    /// The text brush for enabled days that are neither selected nor today.
    /// </summary>
    public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    /// <summary>
    /// The text brush for today when that day is not selected.
    /// </summary>
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    /// <summary>
    /// The fill brush for the selected-day disc.
    /// </summary>
    public IBrush SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    /// <summary>
    /// The text brush for the selected day.
    /// </summary>
    public IBrush SelectionForeground { get => GetValue(SelectionForegroundProperty); set => SetValue(SelectionForegroundProperty, value); }
    /// <summary>
    /// The brush for weekday headings and disabled days.
    /// </summary>
    public IBrush WeekdayBrush { get => GetValue(WeekdayBrushProperty); set => SetValue(WeekdayBrushProperty, value); }
    /// <summary>
    /// The weekday in the first calendar column; each instance defaults to the current culture when it is created.
    /// </summary>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    /// <summary>
    /// The earliest selectable local calendar date, or null for no explicit lower bound.
    /// </summary>
    public DateTimeOffset? MinimumDate { get => GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    /// <summary>
    /// The latest selectable local calendar date, or null for no explicit upper bound.
    /// </summary>
    public DateTimeOffset? MaximumDate { get => GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }

    // Scale all geometry from the square cell size.
    private const double DiscRatio = 0.9;
    private const double DayFontRatio = 0.48;
    private const double WeekdayFontRatio = 0.30;
    // Weekday-band height relative to a day cell.
    private const double WeekdayRowRatio = 0.314;
    private const double SelectionPopMs = 180;
    private const double MonthSlideMs = 300;

    private double Cell => Bounds.Width / 7;
    private double WeekdayRowHeight => Cell * WeekdayRowRatio;

    private readonly Rendering.FormattedTextCache _textCache = new();
    private DateTime? _poppingSelection;
    private long _popStart;
    private DateTime? _slideFromMonth;
    private int _slideDirection;
    private long _slideStart;
    private DispatcherTimer? _animTimer;

    static CupertinoMonthGrid()
    {
        AffectsMeasure<CupertinoMonthGrid>(DisplayMonthProperty, FirstDayOfWeekProperty);
        AffectsRender<CupertinoMonthGrid>(DisplayMonthProperty, SelectedDateProperty,
                                          ForegroundProperty, AccentBrushProperty,
                                          SelectionBrushProperty, SelectionForegroundProperty,
                                          WeekdayBrushProperty, FirstDayOfWeekProperty,
                                          MinimumDateProperty, MaximumDateProperty, FlowDirectionProperty);
        FocusableProperty.OverrideDefaultValue<CupertinoMonthGrid>(true);
    }

    /// <summary>
    /// Creates a month grid showing the current month.
    /// </summary>
    public CupertinoMonthGrid()
    {
        SetCurrentValue(DisplayMonthProperty, DateTime.Today);
        SetCurrentValue(FirstDayOfWeekProperty, DateMath.CultureFirstDayOfWeek);
    }

    /// <summary>
    /// Raised when a day is tapped.
    /// </summary>
    public event EventHandler<DateTime>? DayPicked;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (CupertinoAccessibility.ReduceMotion)
            return;

        if (change.Property == SelectedDateProperty &&
            change.NewValue is DateTimeOffset selected)
        {
            _poppingSelection = selected.Date;
            _popStart = MotionClock.Now;
            StartAnimTimer();
        }
        else if (change.Property == DisplayMonthProperty &&
                 change.OldValue is DateTime oldMonth &&
                 change.NewValue is DateTime newMonth &&
                 (oldMonth.Year, oldMonth.Month) != (newMonth.Year, newMonth.Month))
        {
            _slideFromMonth = new DateTime(oldMonth.Year, oldMonth.Month, 1);
            _slideDirection = newMonth > oldMonth ? 1 : -1;
            _slideStart = MotionClock.Now;
            StartAnimTimer();
        }
    }

    private void StartAnimTimer()
    {
        _animTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16),
                                           DispatcherPriority.Render, OnAnimTick);
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        var now = MotionClock.Now;
        var popDone = _poppingSelection is null ||
                      now - _popStart >= SelectionPopMs;
        var slideDone = _slideFromMonth is null ||
                        now - _slideStart >= MonthSlideMs;
        if (popDone)
            _poppingSelection = null;
        if (slideDone)
            _slideFromMonth = null;
        if (popDone && slideDone)
            _animTimer?.Stop();
        InvalidateVisual();
        // Keep surrounding glass in step with the animation.
        GlassSurface.PulseBehind(this);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animTimer?.Stop();
        _poppingSelection = null;
        _slideFromMonth = null;
        // Formatted text and its brushes are rebuilt on demand; do not retain them while detached.
        _textCache.Clear();
    }

    private int ColumnOf(DateTime date) => ((int)date.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;

    private int RowCount()
    {
        var first = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        return (int)Math.Ceiling((ColumnOf(first) + days) / 7.0);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 7 * 55.9 : availableSize.Width;
        var cell = width / 7;
        return new Size(width, cell * WeekdayRowRatio + RowCount() * cell);
    }

    /// <inheritdoc/>
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
            var ft = _textCache.Get(abbrev[(int)dow].ToUpper(culture), culture,
                                       FlowDirection, typeface,
                                       cell * WeekdayFontRatio, WeekdayBrush);
            DrawText(context, ft, colPitch * (c + 0.5), weekdayRow / 2);
        }

        var current = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        if (_slideFromMonth is { } fromMonth)
        {
            var t = Math.Clamp(
                MotionClock.MillisecondsSince(_slideStart) / MonthSlideMs, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3);
            var width = Bounds.Width;
            var dayArea = new Rect(0, weekdayRow, width,
                                   Math.Max(0, Bounds.Height - weekdayRow));
            using (context.PushClip(dayArea))
            {
                DrawMonth(context, fromMonth, -_slideDirection * width * eased,
                          cell, weekdayRow, culture, typeface, bold);
                DrawMonth(context, current, _slideDirection * width * (1 - eased),
                          cell, weekdayRow, culture, typeface, bold);
            }
        }
        else
        {
            DrawMonth(context, current, 0, cell, weekdayRow, culture, typeface, bold);
        }
    }

    private void DrawMonth(DrawingContext context, DateTime first, double xOffset,
                           double cell, double weekdayRow, CultureInfo culture,
                           Typeface typeface, Typeface bold)
    {
        var days = DateTime.DaysInMonth(first.Year, first.Month);
        var today = DateTime.Today;
        var selected = SelectedDate?.Date;

        for (var d = 1; d <= days; d++)
        {
            var date = new DateTime(first.Year, first.Month, d);
            var index = ColumnOf(first) + d - 1;
            var cx = xOffset + cell * (index % 7 + 0.5);
            var cy = weekdayRow + cell * (index / 7 + 0.5);

            var isSelected = selected == date;
            var isToday = today == date;

            if (isSelected)
            {
                var r = cell * DiscRatio / 2;
                if (_poppingSelection == date)
                {
                    var t = Math.Clamp(
                        MotionClock.MillisecondsSince(_popStart) / SelectionPopMs, 0, 1);
                    r *= 0.5 + 0.5 * (1 - (1 - t) * (1 - t));
                }
                context.DrawEllipse(SelectionBrush, null, new Point(cx, cy), r, r);
            }

            var isEnabled = IsDateEnabled(date);
            var brush = isSelected ? SelectionForeground
                : !isEnabled ? WeekdayBrush
                : isToday ? AccentBrush : Foreground;
            var face = isSelected ? bold : typeface;
            var ft = _textCache.Get(d.ToString(culture), culture, FlowDirection,
                                       face, cell * DayFontRatio, brush);
            DrawText(context, ft, cx, cy);
        }
    }

    private void DrawText(DrawingContext context, FormattedText text, double cx, double cy)
    {
        // Keep Avalonia's mirrored columns and pointer coordinates, but undo
        // the reflection for glyphs; FlowDirection already handles text shaping.
        var scaleX = FlowDirection == FlowDirection.RightToLeft ? -1 : 1;
        using (context.PushTransform(Matrix.CreateScale(scaleX, 1) * Matrix.CreateTranslation(cx, cy)))
            context.DrawText(text, new Point(-text.Width / 2, -text.Height / 2));
    }

    private DateTime? DateAt(Point p)
    {
        if (p.Y < WeekdayRowHeight)
            return null;

        var col = (int)(p.X / Cell);
        var row = (int)((p.Y - WeekdayRowHeight) / Cell);
        if (col is < 0 or > 6 || row < 0)
            return null;

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

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (DateAt(e.GetPosition(this)) is not { } date)
            return;

        SetCurrentValue(SelectedDateProperty, new DateTimeOffset(date));
        DayPicked?.Invoke(this, date);
        e.Handled = true;
    }

    /// <inheritdoc/>
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
    /// <summary>
    /// Identifies the <see cref="SelectedDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="DisplayMonth"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTime> DisplayMonthProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTime>(nameof(DisplayMonth), DateTime.Today);

    /// <summary>
    /// Identifies the <see cref="IsMonthPickerOpen"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsMonthPickerOpenProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, bool>(nameof(IsMonthPickerOpen));

    /// <summary>
    /// The selected calendar date, or null for no selection. Date bounds compare local calendar dates rather than UTC instants.
    /// </summary>
    public DateTimeOffset? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    /// <summary>
    /// A date in the displayed month; the day component does not change the month layout.
    /// </summary>
    public DateTime DisplayMonth { get => GetValue(DisplayMonthProperty); set => SetValue(DisplayMonthProperty, value); }
    /// <summary>
    /// Whether the month and year wheels replace the day grid.
    /// </summary>
    public bool IsMonthPickerOpen { get => GetValue(IsMonthPickerOpenProperty); set => SetValue(IsMonthPickerOpenProperty, value); }

    /// <summary>
    /// Identifies the <see cref="MinYear"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MinYearProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, int>(nameof(MinYear), 1900);

    /// <summary>
    /// Identifies the <see cref="MaxYear"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxYearProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, int>(nameof(MaxYear), 2100);

    /// <summary>
    /// Identifies the <see cref="MinimumDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MinimumDateProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTimeOffset?>(nameof(MinimumDate));

    /// <summary>
    /// Identifies the <see cref="MaximumDate"/> property.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> MaximumDateProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DateTimeOffset?>(nameof(MaximumDate));

    /// <summary>
    /// Identifies the <see cref="FirstDayOfWeek"/> property.
    /// </summary>
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<CupertinoCalendarView, DayOfWeek>(nameof(FirstDayOfWeek),
            DateMath.CultureFirstDayOfWeek);

    /// <summary>
    /// The lowest year in the month picker and selectable calendar range, clamped to 1–9999; defaults to 1900.
    /// </summary>
    public int MinYear { get => GetValue(MinYearProperty); set => SetValue(MinYearProperty, value); }
    /// <summary>
    /// The highest year in the month picker and selectable calendar range, clamped to 1–9999; defaults to 2100.
    /// </summary>
    public int MaxYear { get => GetValue(MaxYearProperty); set => SetValue(MaxYearProperty, value); }
    /// <summary>
    /// The earliest selectable local calendar date, or null for no explicit lower bound.
    /// </summary>
    public DateTimeOffset? MinimumDate { get => GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    /// <summary>
    /// The latest selectable local calendar date, or null for no explicit upper bound.
    /// </summary>
    public DateTimeOffset? MaximumDate { get => GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    /// <summary>
    /// The weekday in the first calendar column; each instance defaults to the current culture when it is created.
    /// </summary>
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

    /// <summary>
    /// Creates a calendar view showing the current month.
    /// </summary>
    public CupertinoCalendarView()
    {
        SetCurrentValue(DisplayMonthProperty, DateTime.Today);
        SetCurrentValue(FirstDayOfWeekProperty, DateMath.CultureFirstDayOfWeek);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    private void NormalizeDateRange(AvaloniaProperty changedProperty) =>
        DateRange.Normalize(this, changedProperty, MinimumDateProperty, MaximumDateProperty, ref _normalizingDateRange);

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
        var maximumMonthEnd = new DateTime(maximumMonth.Year, maximumMonth.Month,
            DateTime.DaysInMonth(maximumMonth.Year, maximumMonth.Month));
        var maximum = MaximumDate?.Date ?? maximumMonthEnd;
        if (maximum > maximumMonthEnd)
            maximum = maximumMonthEnd;
        if (minimum > maximum)
            minimum = maximum;

        if (selected.Date < minimum)
            return DateMath.AtOffset(minimum, selected.Offset);
        if (selected.Date > maximum)
            return DateMath.AtOffset(maximum, selected.Offset);
        return selected;
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
