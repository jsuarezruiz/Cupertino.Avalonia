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
        new FuncValueConverter<string?, string>(FirstLetter);

    // Take the first text element so surrogate pairs and combining marks stay whole.
    private static string FirstLetter(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var enumerator = StringInfo.GetTextElementEnumerator(value);
        return enumerator.MoveNext()
            ? ((string)enumerator.Current).ToUpper(CultureInfo.CurrentCulture)
            : string.Empty;
    }
}
