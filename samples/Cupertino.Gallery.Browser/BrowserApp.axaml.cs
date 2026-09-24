using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Rendering;
using Cupertino.Themes;

namespace Cupertino.Gallery;

public sealed class BrowserApp : Application
{
    internal static bool ShowRendererDiagnostics { get; set; }

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
        {
            var shell = new ShellView();
            if (ShowRendererDiagnostics)
                shell.AttachedToVisualTree += (_, _) =>
                {
                    if (TopLevel.GetTopLevel(shell) is { } top)
                        top.RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps
                            | RendererDebugOverlays.RenderTimeGraph
                            | RendererDebugOverlays.LayoutTimeGraph;
                };
            single.MainView = shell;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
