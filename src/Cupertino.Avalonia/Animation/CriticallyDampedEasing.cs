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

    // Ease runs every frame; the normalization only depends on the rate.
    private double _cachedOmega;
    private double _cachedNormalization = 1.0;

    /// <inheritdoc/>
    public override double Ease(double progress)
    {
        var omega = OmegaDuration <= 0 ? 7.0 : OmegaDuration;
        if (omega != _cachedOmega)
        {
            _cachedOmega = omega;
            _cachedNormalization = MotionCurve.Normalization(omega);
        }
        return MotionCurve.CriticallyDamped(progress, omega, _cachedNormalization);
    }
}
