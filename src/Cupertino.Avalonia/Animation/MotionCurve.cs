namespace Cupertino.Animation;

// Critically damped spring shapes and smoothing shared by the easings and the interaction drivers.
internal static class MotionCurve
{
    // Settling rate used by navigation, dialog and tab motion.
    internal const double StandardOmega = 8.4;

    // Iteration step of a critically damped curve, normalized so it reaches one at progress one.
    internal static double CriticallyDamped(double progress, double omega) =>
        CriticallyDamped(progress, omega, Normalization(omega));

    // Use this overload when the rate is fixed and the denominator can be cached.
    internal static double CriticallyDamped(double progress, double omega, double normalization)
    {
        var x = omega * progress;
        return (1 - (1 + x) * Math.Exp(-x)) / normalization;
    }

    internal static double Normalization(double omega) => 1 - (1 + omega) * Math.Exp(-omega);

    // One integration step of a critically damped spring carrying its own velocity.
    internal static void Step(
        ref double position, ref double velocity, double target, double omega, double dt)
    {
        var distance = position - target;
        var acceleration = -omega * omega * distance - 2 * omega * velocity;
        velocity += acceleration * dt;
        position += velocity * dt;
    }

    internal static double Smooth01(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    internal static double Smoothstep(double from, double to, double x) =>
        Smooth01((x - from) / (to - from));
}
