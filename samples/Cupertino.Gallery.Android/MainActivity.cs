using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Cupertino.Gallery;

[Activity(
    Theme = "@style/CupertinoTheme.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
