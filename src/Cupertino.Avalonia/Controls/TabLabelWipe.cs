using Avalonia;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Builds the hard color boundary produced as a tab indicator crosses label content.
/// </summary>
internal static class TabLabelWipe
{
    internal static IBrush Create(double contentX, double contentWidth,
                                  double indicatorLeft, double indicatorRight,
                                  IBrush accent, IBrush label)
    {
        var left = (indicatorLeft - contentX) / contentWidth;
        var right = (indicatorRight - contentX) / contentWidth;

        if (right <= 0 || left >= 1)
            return label;
        if (left <= 0 && right >= 1)
            return accent;

        var low = Math.Clamp(left, 0, 1);
        var high = Math.Clamp(right, 0, 1);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        gradient.GradientStops.Add(new GradientStop(ColorOf(label), 0));
        gradient.GradientStops.Add(new GradientStop(ColorOf(label), low));
        gradient.GradientStops.Add(new GradientStop(ColorOf(accent), low));
        gradient.GradientStops.Add(new GradientStop(ColorOf(accent), high));
        gradient.GradientStops.Add(new GradientStop(ColorOf(label), high));
        gradient.GradientStops.Add(new GradientStop(ColorOf(label), 1));
        return gradient;
    }

    private static Color ColorOf(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color : Colors.Black;
}
