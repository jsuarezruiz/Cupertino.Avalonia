using SkiaSharp;

namespace Cupertino.Rendering;

/// <summary>
/// Compiles and caches the liquid-glass SkSL effect.
/// </summary>
/// <remarks>Refraction adapted from KaranocaVe/LiquidGlassAvaloniaUI; lighting from whynotmake-it/flutter_liquid_glass. Both MIT.</remarks>
internal static class LiquidGlassShader
{
    private const string Source = """
        uniform shader uBlurred;

        uniform float2 uOrigin;       // device px: top-left of the surface rect on the canvas
        uniform float2 uSize;         // device px: surface size
        uniform float4 uCornerRadii;  // device px: TL, TR, BR, BL
        uniform float  uThickness;    // device px: refraction band depth
        uniform float  uRefraction;   // device px: max sample displacement
        uniform float  uChroma;       // 0..1
        uniform float  uDepth;        // 0..1
        uniform float4 uTint;         // straight RGBA
        uniform float2 uLightDir;     // normalized, y-down
        uniform float  uLightIntensity;
        uniform float  uFresnel;
        uniform float  uAdaptive;     // 1 = adapt tint/lighting to backdrop luminance
        uniform float  uMagnify;      // 1 = flat, >1 = convex lens over the whole interior
        uniform float  uPad;          // device px the pre-blurred capture extends past uSize

        float radiusAt(float2 c, float4 radii) {
            if (c.x >= 0.0) {
                return c.y < 0.0 ? radii.y : radii.z;
            }
            return c.y < 0.0 ? radii.x : radii.w;
        }

        float sdRoundedRect(float2 coord, float2 halfSize, float radius) {
            float2 q = abs(coord) - (halfSize - float2(radius));
            return length(max(q, 0.0)) - radius + min(max(q.x, q.y), 0.0);
        }

        // Avoid sign(0), which creates centre-line artifacts.
        float2 nonZeroSign(float2 v) {
            return float2(v.x >= 0.0 ? 1.0 : -1.0, v.y >= 0.0 ? 1.0 : -1.0);
        }

        float2 gradSdRoundedRect(float2 coord, float2 halfSize, float radius) {
            float2 q = abs(coord) - (halfSize - float2(radius));
            if (q.x >= 0.0 || q.y >= 0.0) {
                float2 m = max(q, 0.0);
                float len = length(m);
                float2 dir = len > 1e-5 ? m / len : float2(0.0, 1.0);
                return nonZeroSign(coord) * dir;
            }
            float gx = step(q.y, q.x);
            return nonZeroSign(coord) * float2(gx, 1.0 - gx);
        }

        float circleMap(float x) {
            x = clamp(x, 0.0, 1.0);
            return 1.0 - sqrt(1.0 - x * x);
        }

        // Clamp to the padded capture to avoid edge streaks.
        float2 clampCoord(float2 p) {
            return clamp(p, float2(1.0 - uPad), uSize - 1.0 + uPad);
        }

        float lumaAt(float2 p) {
            half4 c = uBlurred.eval(clampCoord(p));
            return dot(float3(c.rgb), float3(0.2126, 0.7152, 0.0722));
        }

        // Approximate backdrop luminance without a CPU readback.
        float backdropLuma() {
            float l = 0.0;
            l += lumaAt(uSize * float2(0.15, 0.15));
            l += lumaAt(uSize * float2(0.50, 0.15));
            l += lumaAt(uSize * float2(0.85, 0.15));
            l += lumaAt(uSize * float2(0.15, 0.50));
            l += lumaAt(uSize * float2(0.50, 0.50));
            l += lumaAt(uSize * float2(0.85, 0.50));
            l += lumaAt(uSize * float2(0.15, 0.85));
            l += lumaAt(uSize * float2(0.50, 0.85));
            l += lumaAt(uSize * float2(0.85, 0.85));
            return l / 9.0;
        }

        half4 main(float2 coord) {
            float2 local = coord - uOrigin;
            float2 halfSize = uSize * 0.5;
            float2 c = local - halfSize;

            float maxR = min(halfSize.x, halfSize.y);
            float radius = min(radiusAt(c, uCornerRadii), maxR);
            float sd = sdRoundedRect(c, halfSize, radius);

            float aa = clamp(0.5 - sd, 0.0, 1.0);
            if (aa <= 0.0) {
                return half4(0.0);
            }

            float h = max(uThickness, 0.001);
            float band = 1.0 - clamp(-min(sd, 0.0) / h, 0.0, 1.0); // 0 interior -> 1 edge

            float gradRadius = min(radius * 1.5, maxR);
            float2 grad = gradSdRoundedRect(c, halfSize, gradRadius);

            float d = circleMap(band) * uRefraction;
            float cLen = length(c);
            float2 radial = cLen > 1e-4 ? c / cLen : float2(0.0, 1.0);
            float2 lensDir = normalize(grad + uDepth * radial);

            // Magnify across the whole interior.
            float2 magnified = halfSize + c / max(uMagnify, 0.001);
            float2 refracted = magnified + d * lensDir;

            half4 bg;
            if (uChroma <= 0.001 || d <= 0.001) {
                bg = uBlurred.eval(clampCoord(refracted));
            } else {
                // Concentrate chromatic fringing at the rim.
                float2 spread = d * lensDir * uChroma * (0.35 + 0.9 * band * band);
                half4 cr = uBlurred.eval(clampCoord(refracted + spread));
                half4 cg = uBlurred.eval(clampCoord(refracted));
                half4 cb = uBlurred.eval(clampCoord(refracted - spread));
                bg = half4(cr.r, cg.g, cb.b, cg.a);
            }

            float3 color = float3(bg.rgb);

            // Adapt clear glass for contrast without altering tinted surfaces.
            float luma = uAdaptive > 0.5 ? backdropLuma() : 0.5;
            float clearness = 1.0 - smoothstep(0.35, 0.85, uTint.a);
            float adapt = (uAdaptive > 0.5) ? clearness : 0.0;
            float k = uTint.a * 1.8;
            float veilWhite = max(0.0, 0.100 - 0.110 * luma) * k;
            float veilBlack = max(0.0, 0.090 * (luma - 0.55)) * k;
            float rimScale = mix(1.7, 0.85, clamp(luma, 0.0, 1.0));
            float specScale = mix(0.85, 1.0, clamp(luma, 0.0, 1.0));

            float3 adapted = mix(color, uTint.rgb, clamp(veilWhite, 0.0, 0.96));
            adapted = mix(adapted, float3(0.0), clamp(veilBlack, 0.0, 0.96));
            float3 tinted = mix(color, uTint.rgb, uTint.a);
            color = mix(tinted, adapted, adapt);

            // Light both edges, favoring the key-light side.
            float ndl = max(0.0, dot(grad, uLightDir));
            float opp = max(0.0, dot(grad, -uLightDir));
            float total = ndl + opp * 0.55;
            float directional = total * sqrt(total) * uLightIntensity * specScale;
            float edgeFactor = smoothstep(0.0, 0.6, band);
            float brightness = directional * edgeFactor;
            brightness = brightness / (1.0 + brightness);
            color = mix(color, float3(1.0), brightness);

            // Add a shallow contact shade inside the rim.
            float away = max(0.0, dot(grad, -uLightDir));
            float shadeBand = smoothstep(0.30, 0.70, band) * (1.0 - smoothstep(0.80, 0.95, band));
            color *= 1.0 - shadeBand * away * 0.07 * clamp(uLightIntensity, 0.0, 1.0);

            float rim = smoothstep(0.72, 1.0, band) * uFresnel * rimScale;
            color += rim * 0.25 * (0.4 + 0.6 * total);

            color = clamp(color, 0.0, 1.0);
            return half4(half3(color) * half(aa), half(aa));
        }
        """;

    private static readonly Lazy<SKRuntimeEffect?> LazyEffect = new(() =>
    {
        try
        {
            var effect = SKRuntimeEffect.CreateShader(Source, out var errors);
            if (effect is null)
                CompileError = errors ?? "unknown error";
            return effect;
        }
        catch (Exception ex)
        {
            // Unsupported backends use the fallback renderer.
            CompileError = ex.Message;
            return null;
        }
    });

    /// <summary>
    /// Gets the effect, or null when unsupported.
    /// </summary>
    public static SKRuntimeEffect? Effect => LazyEffect.Value;

    /// <summary>
    /// Gets the compile error, if any.
    /// </summary>
    public static string? CompileError { get; private set; }

    /// <summary>
    /// Gets whether the full glass pipeline is available.
    /// </summary>
    public static bool IsSupported => LazyEffect.Value is not null;
}
