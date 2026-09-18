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

    // Everything except the progress term depends only on the properties above;
    // recompute on change rather than paying the extra Exp/Cos/Sin per frame.
    private double _cachedZeta;
    private double _cachedOmega;
    private double _cachedDamped;
    private double _cachedDecay;
    private double _cachedMix;
    private double _cachedEnd = 1.0;

    /// <inheritdoc/>
    public override double Ease(double progress)
    {
        if (progress >= 1)
            return 1;

        var zeta = Math.Clamp(DampingRatio, 0.05, 0.999);
        var w = OmegaDuration <= 0 ? 12.0 : OmegaDuration;
        if (zeta != _cachedZeta || w != _cachedOmega)
        {
            _cachedZeta = zeta;
            _cachedOmega = w;
            var zw = -zeta * w;
            _cachedDamped = w * Math.Sqrt(1 - zeta * zeta);
            _cachedDecay = zw;
            _cachedMix = zeta * w / _cachedDamped;
            _cachedEnd = 1 - Math.Exp(zw) *
                (Math.Cos(_cachedDamped) + _cachedMix * Math.Sin(_cachedDamped));
        }

        var value = 1 - Math.Exp(_cachedDecay * progress) *
            (Math.Cos(_cachedDamped * progress) + _cachedMix * Math.Sin(_cachedDamped * progress));
        return value / _cachedEnd;
    }
}
