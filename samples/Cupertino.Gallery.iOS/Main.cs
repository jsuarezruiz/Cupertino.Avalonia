using System.Reflection;
using System.Runtime.InteropServices;
using SkiaSharp;
using UIKit;

namespace Cupertino.Gallery;

public static class Program
{
    private static void Main(string[] args)
    {
        NativeLibrary.SetDllImportResolver(typeof(SKObject).Assembly, ResolveSkiaSharp);
        UIApplication.Main(args, null, typeof(AppDelegate));
    }

    private static IntPtr ResolveSkiaSharp(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (libraryName != "libSkiaSharp")
            return IntPtr.Zero;

        var framework = Path.Combine(
            Foundation.NSBundle.MainBundle.PrivateFrameworksPath!,
            "libSkiaSharp.framework",
            "libSkiaSharp");
        return NativeLibrary.Load(framework, assembly, searchPath);
    }
}
