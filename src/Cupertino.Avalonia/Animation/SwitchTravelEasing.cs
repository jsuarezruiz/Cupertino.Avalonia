using Avalonia.Animation.Easings;

namespace Cupertino.Animation;

/// <summary>
/// A 334ms critically damped switch curve with no overshoot.
/// </summary>
public class SwitchTravelEasing : Easing
{
    private const double OmegaT = 21.0 * 0.334;
    private static readonly double Norm = 1 - (1 + OmegaT) * Math.Exp(-OmegaT);

    public override double Ease(double progress)
    {
        var x = OmegaT * progress;
        return (1 - (1 + x) * Math.Exp(-x)) / Norm;
    }
}
