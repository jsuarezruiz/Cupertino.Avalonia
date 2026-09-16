using Avalonia;

namespace Cupertino.Controls;

internal static class DateRange
{
    // Keeps a minimum/maximum pair ordered by moving the bound that was not just changed.
    internal static void Normalize(
        AvaloniaObject owner,
        AvaloniaProperty changedProperty,
        StyledProperty<DateTimeOffset?> minimumProperty,
        StyledProperty<DateTimeOffset?> maximumProperty,
        ref bool normalizing)
    {
        if (owner.GetValue(minimumProperty) is not { } minimum
            || owner.GetValue(maximumProperty) is not { } maximum
            || minimum.Date <= maximum.Date)
            return;

        normalizing = true;
        try
        {
            if (changedProperty == minimumProperty)
                owner.SetCurrentValue(maximumProperty, minimum);
            else
                owner.SetCurrentValue(minimumProperty, maximum);
        }
        finally
        {
            normalizing = false;
        }
    }
}
