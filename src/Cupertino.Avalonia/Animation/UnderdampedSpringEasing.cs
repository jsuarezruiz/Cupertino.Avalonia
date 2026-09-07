using Avalonia.Animation.Easings;

namespace Cupertino.Animation;

/// <summary>
/// An underdamped spring with a slight overshoot, like UIKit presentation springs.
/// </summary>
public class UnderdampedSpringEasing : Easing
{
    /// <summary>
    /// Gets or sets the damping ratio; below 1 overshoots.
    /// </summary>
    public double DampingRatio { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets how early the spring settles.
    /// </summary>
    public double OmegaDuration { get; set; } = 12.0;

    /// <inheritdoc/>
    public override double Ease(double progress)
    {
        var zeta = Math.Clamp(DampingRatio, 0.05, 0.999);
        var w = OmegaDuration <= 0 ? 12.0 : OmegaDuration;
        var wd = w * Math.Sqrt(1 - zeta * zeta);
        var x = progress;
        var value = 1 - Math.Exp(-zeta * w * x) *
            (Math.Cos(wd * x) + zeta * w / wd * Math.Sin(wd * x));
        var end = 1 - Math.Exp(-zeta * w) *
            (Math.Cos(wd) + zeta * w / wd * Math.Sin(wd));
        return progress >= 1 ? 1 : value / end;
    }
}
