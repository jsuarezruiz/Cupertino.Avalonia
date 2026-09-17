using Avalonia.Animation.Easings;

namespace Cupertino.Animation;

/// <summary>
/// A normalized critically damped switch curve with no overshoot. The consuming transition controls
/// the duration; the switch theme pairs the curve with a 334 millisecond travel.
/// </summary>
public class SwitchTravelEasing : Easing
{
    // Settling rate and travel time the curve is normalized for.
    private const double SettleRate = 21.0;
    private const double TravelSeconds = 0.334;
    private const double OmegaT = SettleRate * TravelSeconds;
    private static readonly double Norm = MotionCurve.Normalization(OmegaT);

    /// <inheritdoc/>
    public override double Ease(double progress)
    {
        return MotionCurve.CriticallyDamped(progress, OmegaT, Norm);
    }
}
