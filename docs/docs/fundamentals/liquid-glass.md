---
title: Liquid Glass
description: Configure live refractive GlassSurface materials and their fallbacks.
ms.date: 2026-08-27
---

# Liquid Glass

`GlassSurface` samples the pixels behind its child and combines blur, refraction, saturation, edge light, tint, and shadow into a live material.

<img src="../../images/liquid-glass.png"
     alt="Liquid Glass examples and interactive material controls"
     width="420" />

```xml
<Panel xmlns:cupertino="https://cupertino.avaloniaui.net">
  <Image Source="avares://MyApp/Assets/Backdrop.jpg"
         Stretch="UniformToFill" />

  <cupertino:GlassSurface Width="220"
                          Height="110"
                          CornerRadius="24"
                          IsLive="True"
                          BlurRadius="24"
                          GlassThickness="11"
                          RefractionStrength="18"
                          ChromaticAberration="0.5"
                          DepthEffect="0.28"
                          Saturation="1.3"
                          LightIntensity="0.9"
                          FresnelStrength="1.2">
    <TextBlock HorizontalAlignment="Center"
               VerticalAlignment="Center"
               Text="Liquid Glass" />
  </cupertino:GlassSurface>
</Panel>
```

## Important properties

| Property | Effect |
| --- | --- |
| `BlurRadius` | Frost blur sigma in logical pixels |
| `GlassThickness` | Width of the refracting edge band |
| `RefractionStrength` | Backdrop displacement at the edge |
| `ChromaticAberration` | Color separation in the refraction band |
| `DepthEffect` | Convex lens contribution |
| `Saturation` | Backdrop saturation multiplier |
| `Tint` | Material tint; alpha controls its strength |
| `LightIntensity` and `LightAngle` | Directional highlight |
| `FresnelStrength` | Brightness at the outer rim |
| `Magnification` | Convex magnification; `1` is flat |
| `IsAdaptive` | Adapts the material to backdrop luminance |

Start from the values used by an existing themed control and tune only the properties needed for the intended material. Strong refraction and chromatic aberration are best reserved for compact lenses; large reading surfaces should use a thick, calmer material.

## Rendering and accessibility

Call `UseCupertino()` during application setup to support full-frame live sampling. When Reduce Transparency is enabled or a live GPU surface is unavailable, `GlassSurface` degrades to a stable tinted material. Do not place meaning exclusively in the refraction effect.
