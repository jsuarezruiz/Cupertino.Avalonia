using Avalonia;

namespace Cupertino.Controls;

/// <summary>
/// Geometry and timing shared by tab indicators.
/// </summary>
internal static class TabBarMotionModel
{
    internal const double TravelMilliseconds = 180.0;
    internal const double SegmentedTravelMilliseconds = 180.0;
    internal const double MaxStretch = 2.02;

    private const double ScrollTakeoverThreshold = 10.0;
    private const double LeadSpan = 0.67;
    private const double TrailDelay = 0.13;
    private static readonly double EdgeSeparation = ComputeEdgeSeparation();

    internal static bool ShouldYieldToScroll(double dx, double dy, bool dragging)
    {
        var ax = Math.Abs(dx);
        var ay = Math.Abs(dy);
        return ay >= ScrollTakeoverThreshold && (dragging || ay > ax);
    }

    internal static (double Progress, double WidthScale, double FillOpacity, double LensOpacity)
        SegmentedTravel(double elapsedMilliseconds, double hops)
    {
        const double referenceDuration = 600;
        var t = elapsedMilliseconds * referenceDuration / SegmentedTravelMilliseconds;
        const double tau = 60;
        var progress = 1 - (1 + t / tau) * Math.Exp(-t / tau);
        var swell = 1 + 0.095 * Math.Clamp(hops, 1, 2.3);

        double widthScale;
        if (t < 60)
            widthScale = 1 + (swell - 1) * Smooth(t / 60);
        else if (t < 250)
            widthScale = swell;
        else if (t < 360)
            widthScale = swell + (0.93 - swell) * Smooth((t - 250) / 110);
        else
            widthScale = 0.93 + 0.07 * Smooth((t - 360) / (referenceDuration - 360));

        var fillOpacity = t < 10 ? 1
            : t < 90 ? 1 - Smooth((t - 10) / 80)
            : t < 280 ? 0
            : t < 450 ? Smooth((t - 280) / 170)
            : 1;
        var lensOpacity = t < 70 ? Smooth(t / 70)
            : t < 380 ? 1
            : t < 500 ? 1 - Smooth((t - 380) / 120)
            : 0;

        return (progress, widthScale, fillOpacity, lensOpacity);
    }

    internal static (double Thickness, double Refraction, double Chroma, double Magnification)
        SegmentedLensOptics(double heightScale)
    {
        const double heldHeightScale = 1.5;
        var activity = Math.Clamp((heightScale - 1) / (heldHeightScale - 1), 0, 1);
        activity = Smooth(activity);
        return (
            5.0 + 4.0 * activity,
            8.0 + 5.0 * activity,
            0.25 + 0.17 * activity,
            1.025 + 0.020 * activity);
    }

    internal static double DragWidthScale(double velocity, bool segmented) => segmented
        ? Math.Clamp(1 + Math.Abs(velocity) / 160.0, 1, 1.25)
        : Math.Clamp(1 + Math.Abs(velocity) / 14.0, 1, 2.0);

    internal static (double Left, double Right) TravelEdges(double t, Rect from, Rect to)
    {
        var lead = Curve(Math.Min(1.0, t / LeadSpan));
        var trail = Curve(Math.Max(0.0, (t - TrailDelay) / (1 - TrailDelay)));
        var distance = Math.Abs(to.Center.X - from.Center.X);
        var width = to.Width > 0 ? to.Width : 1;

        if (distance > width * 0.01)
        {
            var targetSeparation = (MaxStretch - 1) * width / distance;
            var scale = Math.Clamp(targetSeparation / EdgeSeparation, 0, 1);
            trail = lead + (trail - lead) * scale;
        }

        var goingRight = to.X >= from.X;
        var leftProgress = goingRight ? trail : lead;
        var rightProgress = goingRight ? lead : trail;
        return (from.X + (to.X - from.X) * leftProgress,
                from.Right + (to.Right - from.Right) * rightProgress);
    }

    private static double ComputeEdgeSeparation()
    {
        var peak = 0.0;
        for (var t = 0.0; t <= 1.0; t += 16 / TravelMilliseconds)
            peak = Math.Max(peak, Curve(Math.Min(1.0, t / LeadSpan))
                                - Curve(Math.Max(0.0, (t - TrailDelay) / (1 - TrailDelay))));
        return peak > 0 ? peak : 0.5;
    }

    private static double Curve(double t)
    {
        if (t <= 0)
            return 0;
        if (t >= 1)
            return 1;
        const double omega = 7.0;
        var normalization = 1 - (1 + omega) * Math.Exp(-omega);
        var x = omega * t;
        return (1 - (1 + x) * Math.Exp(-x)) / normalization;
    }

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }
}
