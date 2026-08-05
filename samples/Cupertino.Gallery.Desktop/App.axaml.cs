using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Cupertino.Gallery;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Program.Static)
        {
            var style = new Style(x => x.Is<Cupertino.Controls.GlassSurface>());
            style.Setters.Add(new Setter(
                Cupertino.Controls.GlassSurface.IsLiveProperty, false));
            Styles.Add(style);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.AlertPreview ? new AlertPreviewWindow() : Program.Matte
                ? (Window)new MatteWindow()
                : Program.Nav ? new NavLabWindow()
                : Program.Cal ? new CalLabWindow()
                : Program.Wheel ? new WheelLabWindow()
                : Program.Lab ? new MainWindow() : (Window)new GalleryWindow();

            // Apply the theme after the visual tree exists.
            if (Program.Dark)
                RequestedThemeVariant = ThemeVariant.Dark;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
