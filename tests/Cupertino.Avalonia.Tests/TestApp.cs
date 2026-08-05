using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Cupertino.Themes;

[assembly: AvaloniaTestApplication(typeof(Cupertino.Avalonia.Tests.TestApp))]

namespace Cupertino.Avalonia.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new CupertinoTheme());
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
