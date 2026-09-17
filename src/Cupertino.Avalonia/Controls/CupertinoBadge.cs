using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Cupertino.Controls;

/// <summary>
/// A count badge that can also render as a dot.
/// </summary>
public class CupertinoBadge : TemplatedControl
{
    /// <summary>
    /// Identifies the <see cref="Value"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<CupertinoBadge, int>(nameof(Value), -1);

    /// <summary>
    /// Identifies the <see cref="Text"/> property.
    /// </summary>
    public static readonly DirectProperty<CupertinoBadge, string> TextProperty =
        AvaloniaProperty.RegisterDirect<CupertinoBadge, string>(nameof(Text), b => b.Text);

    private string _text = "";

    /// <summary>
    /// Gets or sets the count; negative values show a dot and values above 99 show <c>99+</c>.
    /// </summary>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets the formatted text displayed by the badge.
    /// </summary>
    public string Text
    {
        get => _text;
        private set => SetAndRaise(TextProperty, ref _text, value);
    }

    /// <summary>
    /// Creates a CupertinoBadge with its default settings.
    /// </summary>
    public CupertinoBadge()
    {
        UpdateFromValue(Value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            UpdateFromValue(change.GetNewValue<int>());
    }

    private void UpdateFromValue(int value)
    {
        PseudoClasses.Set(":dot", value < 0);
        Text = value < 0 ? "" : FormatCount(Math.Min(value, 99)) + (value > 99 ? "+" : "");
    }

    private static string FormatCount(int value)
    {
        var culture = CultureInfo.CurrentCulture;
        var formatted = value.ToString(culture);
        var digits = culture.NumberFormat.NativeDigits;
        if (digits.Length != 10 || UsesAsciiDigits(digits))
            return formatted;

        var localized = new StringBuilder(formatted.Length);
        foreach (var character in formatted)
        {
            if (character is >= '0' and <= '9')
                localized.Append(digits[character - '0']);
            else
                localized.Append(character);
        }
        return localized.ToString();
    }

    // The invariant ASCII digits need no translation.
    private static bool UsesAsciiDigits(string[] digits)
    {
        for (var index = 0; index < digits.Length; index++)
            if (digits[index].Length != 1 || digits[index][0] != (char)('0' + index))
                return false;
        return true;
    }
}
