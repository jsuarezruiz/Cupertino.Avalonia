using System.Globalization;
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
        Text = value < 0 ? "" : value > 99 ? "99+" : value.ToString(CultureInfo.InvariantCulture);
    }
}
