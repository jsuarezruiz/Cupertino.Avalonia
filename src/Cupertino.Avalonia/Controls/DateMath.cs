using System.Globalization;

namespace Cupertino.Controls;

internal static class DateMath
{
    internal static DayOfWeek CultureFirstDayOfWeek => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    internal static DateTimeOffset AtOffset(DateTime date, TimeSpan preferredOffset)
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

    internal static int ClampMinuteIncrement(int value) => Math.Clamp(value, 1, 59);
}
