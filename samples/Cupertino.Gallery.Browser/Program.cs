using Avalonia;
using Avalonia.Browser;
using Cupertino.Gallery;

internal static partial class Program
{
    private static Task Main(string[] args)
    {
        var url = args.FirstOrDefault() ?? string.Empty;
        BrowserApp.ShowRendererDiagnostics = HasQueryFlag(url, "fps");
        var options = new BrowserPlatformOptions();
        if (HasQueryFlag(url, "software"))
            options.RenderingMode = [BrowserRenderingMode.Software2D];

        return AppBuilder.Configure<BrowserApp>()
            .WithInterFont()
            .UseCupertinoSymbolFallbacks()
            .StartBrowserAppAsync("out", options);
    }

    private static bool HasQueryFlag(string url, string name) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Query.TrimStart('?').Split('&')
            .Any(part => part.Split('=')[0].Equals(name, StringComparison.OrdinalIgnoreCase));
}
