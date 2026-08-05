using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace Cupertino.Gallery;

[Register(nameof(AppDelegate))]
public partial class AppDelegate : AvaloniaAppDelegate<IosApp>
{
    // Inter is incompatible with iOS AOT.
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Register before the shell builds its catalogue.
        NativeComparisonHosts.Register();
        return base.CustomizeAppBuilder(builder);
    }
}
