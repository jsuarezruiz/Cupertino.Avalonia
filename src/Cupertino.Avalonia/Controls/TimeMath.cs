namespace Cupertino.Controls;

// Snapping a time of day to a minute grid, shared by the time picker and the date-time picker.
internal static class TimeMath
{
    internal static TimeSpan Normalize(
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
}
