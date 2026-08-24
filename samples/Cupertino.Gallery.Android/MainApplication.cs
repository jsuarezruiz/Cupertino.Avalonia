using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Cupertino.Gallery;

[Application]
public class MainApplication : AvaloniaAndroidApplication<AndroidApp>
{
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
