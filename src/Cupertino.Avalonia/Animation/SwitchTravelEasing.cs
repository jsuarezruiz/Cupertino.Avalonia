using Avalonia.Animation.Easings;

namespace Cupertino.Animation;

/// <summary>
/// A normalized critically damped switch curve with no overshoot. The consuming transition controls its duration; the theme uses 334 milliseconds.
/// </summary>
public class SwitchTravelEasing : Easing
{
    private const double OmegaT = 21.0 * 0.334;
    private static readonly double Norm = 1 - (1 + OmegaT) * Math.Exp(-OmegaT);

    /// <inheritdoc/>
    public override double Ease(double progress)
    {
        var x = OmegaT * progress;
        return (1 - (1 + x) * Math.Exp(-x)) / Norm;
    }
}
