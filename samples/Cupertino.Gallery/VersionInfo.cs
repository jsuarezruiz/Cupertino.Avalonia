using System.Reflection;
using Avalonia;
using Cupertino.Themes;

namespace Cupertino.Gallery;

internal static class VersionInfo
{
    public static string? Library { get; } = ReadLibrary(typeof(CupertinoTheme).Assembly);

    public static string Avalonia { get; } = Read(typeof(AvaloniaObject).Assembly);

    private static string? ReadLibrary(Assembly assembly)
    {
        var version = Read(assembly);
        return version == "unknown" || version.EndsWith("-local", StringComparison.Ordinal)
            ? null
            : version;
    }

    private static string Read(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        var suffix = version.IndexOf('+');
        version = suffix > 0 ? version[..suffix] : version;
        return version;
    }
}
