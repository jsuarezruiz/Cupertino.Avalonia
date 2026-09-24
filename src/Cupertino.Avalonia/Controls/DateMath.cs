using System.Globalization;
using System.Runtime.CompilerServices;

namespace Cupertino.Controls;

internal static class DateMath
{
    private static readonly ConditionalWeakTable<CultureInfo, CultureInfo> GregorianCultures = new();

    internal static DayOfWeek CultureFirstDayOfWeek => CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    // Pickers lay out Gregorian dates, so their labels must not use a Hijri, Persian or Buddhist calendar.
    internal static CultureInfo GregorianCulture => Gregorian(CultureInfo.CurrentCulture);

    // A writable culture can change after use, so only read-only ones are cached.
    internal static CultureInfo Gregorian(CultureInfo culture) =>
        culture.DateTimeFormat.Calendar is GregorianCalendar
            ? culture
            : culture.IsReadOnly
            ? GregorianCultures.GetValue(culture, CreateGregorian)
            : CreateGregorian(culture);

    private static CultureInfo CreateGregorian(CultureInfo source)
    {
        if (source.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() is not { } calendar)
            return CultureInfo.InvariantCulture;
        var clone = (CultureInfo)source.Clone();
        clone.DateTimeFormat.Calendar = calendar;
        return CultureInfo.ReadOnly(clone);
    }

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
