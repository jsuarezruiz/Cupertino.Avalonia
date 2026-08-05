using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Cupertino.Themes;

[assembly: AvaloniaTestApplication(typeof(Cupertino.Avalonia.RenderTests.RenderTestApp))]

namespace Cupertino.Avalonia.RenderTests;

/// <summary>
/// Renders through Skia for pixel-level tests.
/// </summary>
public class RenderTestApp : Application
{
    static RenderTestApp()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var theme = new CupertinoTheme();
        theme.Resources["CupertinoFontFamily"] = new FontFamily("fonts:Inter#Inter");
        Styles.Add(theme);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<RenderTestApp>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
