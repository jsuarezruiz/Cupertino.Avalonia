# Rendering and gallery verification

Run all checks from the repository root:

```sh
dotnet test Cupertino.Avalonia.sln -c Release
```

These tests use Avalonia Headless with Skia and Inter fonts. They compare control images, check glass repainting, and verify text orientation in right-to-left layouts.

Pixel comparisons use references for the current operating system under `ReferenceImages/macos`, `ReferenceImages/linux`, or `ReferenceImages/windows`. Skia rasterizes text differently on each OS even with the same font. See [the reference-image guide](ReferenceImages/README.md) for reviewing and updating these images.

Gallery tests open each catalog page, render it, scroll where possible, and navigate back. They also check for binding warnings and missing catalog entries.

The gallery matrix uses these configurations:

| Theme | Width | Direction | Text scale |
| --- | ---: | --- | ---: |
| Light | 402 | LTR | 1 |
| Dark | 402 | LTR | 1 |
| Light | 900 | LTR | 1 |
| Dark | 402 | RTL | 1.5 |

To save gallery frames for visual review on macOS or Linux:

```sh
CUPERTINO_GALLERY_CAPTURES="$PWD/artifacts/gallery-captures" \
  dotnet test tests/Cupertino.Avalonia.RenderTests/Cupertino.Avalonia.RenderTests.csproj \
  -c Release --filter FullyQualifiedName~GalleryIntegrationTests
```

Review the saved gallery images for layout problems. They contain changing sample dates and times, so they are not used as reference images for pixel comparisons. A successful render does not prove every label fits or every interaction works.

Gallery navigation tests disable motion for consistent results. Separate tests cover animations.

The native comparison page requires iOS and is excluded. Check GPU performance, touch, haptics and screen-reader behavior on devices. For manual desktop checks, use the gallery's Settings page to change appearance, layout direction, reduced motion and reduced transparency.
