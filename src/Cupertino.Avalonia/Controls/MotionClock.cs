namespace Cupertino.Controls;

// Monotonic, so system clock adjustments cannot stall or reverse animations.
internal static class MotionClock
{
    public static long Now => Environment.TickCount64;

    public static double MillisecondsSince(long start) => Math.Max(0, Now - start);

    // Frame delta since the previous tick, clamped so a stall cannot teleport a spring or a drag.
    internal static double TakeElapsedMilliseconds(ref long lastTick)
    {
        var now = Now;
        var elapsed = Math.Clamp(now - lastTick, 1, 50);
        lastTick = now;
        return elapsed;
    }

    internal static double TakeElapsedSeconds(ref long lastTick) =>
        TakeElapsedMilliseconds(ref lastTick) / 1000.0;
}
