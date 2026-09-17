using Avalonia;
using Avalonia.Browser;
using Cupertino.Gallery;

internal static partial class Program
{
    private static Task Main(string[] args) =>
        AppBuilder.Configure<BrowserApp>()
            .WithInterFont()
            .UseCupertinoSymbolFallbacks()
            .StartBrowserAppAsync("out");
}
