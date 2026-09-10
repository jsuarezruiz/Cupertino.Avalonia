using Avalonia;
using Avalonia.Platform;

namespace Cupertino;

/// <summary>
/// Adds Cupertino platform integrations to an Avalonia application.
/// </summary>
public static class CupertinoAppBuilderExtensions
{
    /// <summary>
    /// Uses CoreText positioning for Apple's system font on macOS, iOS and
    /// Mac Catalyst. Other platforms and custom font families retain their
    /// configured text shaper.
    /// </summary>
    /// <remarks>
    /// Call this after selecting the Avalonia platform and before application
    /// initialization. Unsupported runs automatically fall back to Avalonia's
    /// original shaping result.
    /// </remarks>
    public static AppBuilder UseCupertinoSystemFont(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
            builder.AfterPlatformServicesSetup(_ =>
            {
                var current = AvaloniaLocator.Current.GetRequiredService<ITextShaperImpl>();
                if (current is not AppleSystemTextShaper)
                    AvaloniaLocator.CurrentMutable.Bind<ITextShaperImpl>()
                        .ToConstant(new AppleSystemTextShaper(current));
            });
        return builder;
    }
}
