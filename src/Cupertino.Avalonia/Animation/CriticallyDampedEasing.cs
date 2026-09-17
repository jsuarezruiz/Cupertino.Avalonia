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

    /// <inheritdoc/>
    public override double Ease(double progress)
    {
        return MotionCurve.CriticallyDamped(progress, OmegaDuration <= 0 ? 7.0 : OmegaDuration);
    }
}
