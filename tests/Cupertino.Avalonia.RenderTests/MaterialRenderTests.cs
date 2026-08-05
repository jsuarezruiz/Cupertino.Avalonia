using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Cupertino.Controls;
using Xunit;

namespace Cupertino.Avalonia.RenderTests;

public class MaterialRenderTests
{
    private static readonly Color Page = Color.Parse("#F2F2F7");

    [AvaloniaFact]
    public void Bar_button_frost_lifts_above_its_backdrop()
    {
        var capsule = new GlassSurface
        {
            Width = 120,
            Height = 36,
            CornerRadius = new global::Avalonia.CornerRadius(18),
            BlurRadius = 18,
            GlassThickness = 0,
            Saturation = 1,
            RefractionStrength = 0,
            ChromaticAberration = 0,
            DepthEffect = 0,
            LightIntensity = 0,
            FresnelStrength = 0,
            Magnification = 1,
            ShadowOpacity = 0.10,
            ShadowBlur = 12,
            ShadowOffset = 2,
            ShadowContactWeight = 0.2,
            Tint = Color.Parse("#C2FFFFFF"),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };

        var luma = Probe.Render("material-frost", capsule, 300, 200, Page);
        var inside = luma[150, 100];
        var outside = luma[20, 20];

        Assert.True(inside > outside, $"capsule {inside} should lift above page {outside}");
        Assert.InRange(inside - outside, 4, 16);
    }

    [AvaloniaFact]
    public void Glass_does_not_paint_its_corners()
    {
        var capsule = new GlassSurface
        {
            Width = 120,
            Height = 60,
            CornerRadius = new global::Avalonia.CornerRadius(30),
            BlurRadius = 18,
            GlassThickness = 0,
            Saturation = 1,
            RefractionStrength = 0,
            LightIntensity = 0,
            FresnelStrength = 0,
            ShadowOpacity = 0,
            Tint = Colors.White,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };

        var luma = Probe.Render("material-corners", capsule, 300, 200, Page);
        var corner = luma[92, 72];
        var centre = luma[150, 100];

        Assert.True(centre > corner + 3,
            $"corner {corner} should stay at page level while the centre reads {centre}");
    }
}
