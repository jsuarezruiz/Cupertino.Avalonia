using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Builds the hard color boundary produced as a tab indicator crosses label content.
/// </summary>
internal static class TabLabelWipe
{
    // ApplyWipe runs per travel tick; one mutable gradient per labelled item
    // replaces a brush and six stops allocated per item per frame. Mutating the
    // stops re-serializes the brush, so rendering updates exactly as before.
    private static readonly ConditionalWeakTable<object, Wipe> Wipes = new();

    internal static IBrush Apply(object item, double contentX, double contentWidth,
                                 double indicatorLeft, double indicatorRight,
                                 IBrush accent, IBrush label)
    {
        var wipe = Wipes.GetValue(item, static _ => new Wipe());
        return wipe.Update(contentX, contentWidth, indicatorLeft, indicatorRight, accent, label);
    }

    internal sealed class Wipe
    {
        private readonly LinearGradientBrush _gradient = new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };
        private readonly GradientStop[] _stops = new GradientStop[6];

        public Wipe()
        {
            for (var i = 0; i < _stops.Length; i++)
            {
                _stops[i] = new GradientStop(Colors.Black, 0);
                _gradient.GradientStops.Add(_stops[i]);
            }
        }

        public IBrush Update(double contentX, double contentWidth,
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
            var labelColor = ColorOf(label);
            var accentColor = ColorOf(accent);
            SetStop(0, labelColor, 0);
            SetStop(1, labelColor, low);
            SetStop(2, accentColor, low);
            SetStop(3, accentColor, high);
            SetStop(4, labelColor, high);
            SetStop(5, labelColor, 1);
            return _gradient;

            void SetStop(int index, Color color, double offset)
            {
                _stops[index].Color = color;
                _stops[index].Offset = offset;
            }
        }
    }

    private static Color ColorOf(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color : Colors.Black;
}
