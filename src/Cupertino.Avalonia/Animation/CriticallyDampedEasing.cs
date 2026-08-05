using Avalonia.Animation.Easings;

namespace Cupertino.Animation;

/// <summary>
/// A critically damped spring with no overshoot.
/// </summary>
public class CriticallyDampedEasing : Easing
{
    /// <summary>
    /// Gets or sets how early the spring settles.
    /// </summary>
    public double OmegaDuration { get; set; } = 7.0;

    public override double Ease(double progress)
    {
        var wd = OmegaDuration <= 0 ? 7.0 : OmegaDuration;
        var norm = 1 - (1 + wd) * Math.Exp(-wd);
        var x = wd * progress;
        return (1 - (1 + x) * Math.Exp(-x)) / norm;
    }
}
