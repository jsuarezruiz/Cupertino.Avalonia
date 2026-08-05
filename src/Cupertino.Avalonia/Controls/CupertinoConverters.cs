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
}
