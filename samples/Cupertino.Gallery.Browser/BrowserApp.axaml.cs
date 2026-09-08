using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Cupertino.Themes;

namespace Cupertino.Gallery;

public sealed class BrowserApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        var theme = new CupertinoTheme();
        theme.Resources["CupertinoFontFamily"] = new FontFamily("fonts:Inter#Inter");
        Styles.Add(theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = new ShellView();

        base.OnFrameworkInitializationCompleted();
    }
}
