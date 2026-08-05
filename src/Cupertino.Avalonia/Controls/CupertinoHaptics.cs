using System.Diagnostics.CodeAnalysis;

namespace Cupertino.Controls;

/// <summary>
/// Haptic feedback types.
/// </summary>
public enum HapticFeedback
{
    /// <summary>
    /// Selection feedback.
    /// </summary>
    Selection,

    /// <summary>
    /// Light impact feedback.
    /// </summary>
    ImpactLight,

    /// <summary>
    /// Medium impact feedback.
    /// </summary>
    ImpactMedium,

    /// <summary>
    /// Heavy impact feedback.
    /// </summary>
    ImpactHeavy,

    /// <summary>
    /// Success feedback.
    /// </summary>
    Success,

    /// <summary>
    /// Warning feedback.
    /// </summary>
    Warning,

    /// <summary>
    /// Error feedback.
    /// </summary>
    Error,
}

/// <summary>
/// Dispatches optional platform haptic feedback.
/// </summary>
public static class CupertinoHaptics
{
    /// <summary>
    /// Gets or sets the platform feedback handler.
    /// </summary>
    public static Action<HapticFeedback>? Handler { get; set; }

    /// <summary>
    /// Gets or sets whether haptics are enabled.
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Requests feedback without propagating handler failures.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Application-provided haptics are an optional, best-effort platform boundary.")]
    public static void Play(HapticFeedback kind)
    {
        if (!IsEnabled)
            return;

        try { Handler?.Invoke(kind); }
        catch { }
    }
}
