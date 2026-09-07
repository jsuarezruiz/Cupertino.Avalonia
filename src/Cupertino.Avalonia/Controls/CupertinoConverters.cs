using System.Globalization;
using Avalonia.Data.Converters;

namespace Cupertino.Controls;

/// <summary>
/// Value converters the Cupertino themes need.
/// </summary>
public static class CupertinoConverters
{
    /// <summary>
    /// Converts a weekday name to its first character.
    /// </summary>
    public static readonly IValueConverter DayInitial =
        new FuncValueConverter<string?, string>(s =>
            string.IsNullOrEmpty(s) ? string.Empty : s![..1].ToUpper(CultureInfo.CurrentCulture));

    /// <summary>
    /// Converts a day-title column to its uppercase abbreviated weekday name, assuming the culture's first day of week.
    /// </summary>
    public static readonly IValueConverter DayColumnAbbreviation =
        new FuncValueConverter<int, string>(column =>
        {
            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            var day = ((int)format.FirstDayOfWeek + column) % 7;
            return format.AbbreviatedDayNames[day].ToUpper(CultureInfo.CurrentCulture);
        });
}
