namespace Cupertino.Controls;

// Monotonic, so system clock adjustments cannot stall or reverse animations.
internal static class MotionClock
{
    public static long Now => Environment.TickCount64;

    public static double MillisecondsSince(long start) => Math.Max(0, Now - start);

    public static double SecondsSince(long start) => MillisecondsSince(start) / 1000.0;
}
