using System.Globalization;
using Avalonia;
using Cupertino;

namespace Cupertino.Gallery;

internal static class Program
{
    public static string? ScreenshotPath;
    public static string? RecordDir;
    public static bool Matte;
    public static bool Lab;
    public static bool Wheel;
    public static bool Cal;
    public static bool Nav;
    public static double ScrollTo;
    public static bool Dark;
    public static bool ShowAlert;
    public static bool AlertPreview;
    public static bool NoActions;
    public static bool HoverAction;
    public static bool Static;
    public static bool ShowSheet;

    [STAThread]
    public static void Main(string[] args)
    {
        var i = Array.IndexOf(args, "--screenshot");
        if (i >= 0 && i + 1 < args.Length)
            ScreenshotPath = args[i + 1];
        Matte = Array.IndexOf(args, "--matte") >= 0;
        Lab = Array.IndexOf(args, "--lab") >= 0;
        Wheel = Array.IndexOf(args, "--wheel") >= 0;
        Cal = Array.IndexOf(args, "--cal") >= 0;
        Nav = Array.IndexOf(args, "--nav") >= 0;
        Dark = Array.IndexOf(args, "--dark") >= 0;
        ShowAlert = Array.IndexOf(args, "--alert") >= 0 || Array.IndexOf(args, "--sheet") >= 0;
        AlertPreview = Array.IndexOf(args, "--alertshot") >= 0;
        NoActions = Array.IndexOf(args, "--noactions") >= 0;
        HoverAction = Array.IndexOf(args, "--hover") >= 0;
        Static = Array.IndexOf(args, "--static") >= 0;
        ShowSheet = Array.IndexOf(args, "--sheet") >= 0;
        var sc = Array.IndexOf(args, "--scroll");
        if (sc >= 0 && sc + 1 < args.Length
            && double.TryParse(args[sc + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var scrollTo))
            ScrollTo = scrollTo;
        var r = Array.IndexOf(args, "--record");
        if (r >= 0 && r + 1 < args.Length)
            RecordDir = args[r + 1];

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseCupertino()
            .WithInterFont()
            .LogToTrace();
}
