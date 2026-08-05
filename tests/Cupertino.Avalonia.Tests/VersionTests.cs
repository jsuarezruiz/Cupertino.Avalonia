using System.Reflection;
using Cupertino.Themes;
using Xunit;

namespace Cupertino.Avalonia.Tests;

public class VersionTests
{
    [Fact]
    public void Version_const_matches_the_assembly()
    {
        var info = typeof(CupertinoTheme).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        Assert.Equal(CupertinoTheme.Version, info.Split('+')[0]);
    }
}
