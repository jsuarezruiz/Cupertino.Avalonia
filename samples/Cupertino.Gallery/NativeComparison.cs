using Avalonia.Controls;

namespace Cupertino.Gallery;

/// <summary>
/// Provides native controls for the comparison gallery.
/// </summary>
public static class NativeComparison
{
    /// <summary>
    /// Creates a native control for a known gallery identifier.
    /// </summary>
    public static Func<string, Control?>? CreateNativeControl { get; set; }

    /// <summary>
    /// Gets whether native comparisons are available.
    /// </summary>
    public static bool IsAvailable => CreateNativeControl is not null;
}
