using System.Reflection;
using Avalonia;
using Cupertino.Themes;

namespace Cupertino.Gallery;

internal static class VersionInfo
{
    public static string Library { get; } = Read(typeof(CupertinoTheme).Assembly);

    public static string Avalonia { get; } = Read(typeof(AvaloniaObject).Assembly);

    private static string Read(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        var suffix = version.IndexOf('+');
        return suffix > 0 ? version[..suffix] : version;
    }
}
