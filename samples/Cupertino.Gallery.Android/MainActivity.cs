using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Cupertino.Gallery;

[Activity(
    Label = "Cupertino Gallery",
    Theme = "@style/CupertinoTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
