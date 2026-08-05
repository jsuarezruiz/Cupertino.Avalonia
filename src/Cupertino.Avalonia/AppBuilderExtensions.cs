using Avalonia;
using Avalonia.Rendering.Composition;

namespace Cupertino;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Configures full-frame rendering for live glass.
    /// </summary>
    public static AppBuilder UseCupertino(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.With(new CompositionOptions
        {
            UseRegionDirtyRectClipping = false,
            MaxDirtyRects = 0,
        });
    }
}
