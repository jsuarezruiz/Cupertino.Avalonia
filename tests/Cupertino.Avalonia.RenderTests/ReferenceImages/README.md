# Render references

The tests use Avalonia 12.1.1, SkiaSharp 3.119.4, the bundled Inter font, and en-US culture. Image comparisons allow a channel difference of 3 and a 0.2% pixel mismatch.

References live in `macos`, `linux`, and `windows`. The test process selects its own OS automatically, with no fallback to another OS. Skia's text rasterization differs across these platforms, so one set of images produced 45 failures on both Linux and Windows in [run 32751279020](https://github.com/jsuarezruiz/Cupertino.Avalonia/actions/runs/32751279020), although macOS passed. The comparison tolerances remain unchanged.

The existing macOS images were retained when splitting the references by OS. Linux and Windows images were captured on 2026-09-07 from the controls at `3fe8e1b`, using their respective GitHub runners in [run 34155670299](https://github.com/jsuarezruiz/Cupertino.Avalonia/actions/runs/34155670299). Each runner then executed the full render suite with reference updates disabled.

## Updating references

Review actual and difference images in `TestResults/RenderDiffs` before updating references. CI uploads these as `render-differences-ubuntu-latest` and `render-differences-windows-latest` artifacts. Copy only reviewed `.actual.png` files from the matching OS and source revision into its reference folder, removing `.actual` from each filename. A missing reference also saves the actual image and fails the test.

To generate images locally on macOS or Linux:

```sh
UPDATE_RENDER_REFERENCES=1 dotnet test tests/Cupertino.Avalonia.RenderTests -c Release --filter FullyQualifiedName~FieldRenderTests
dotnet test tests/Cupertino.Avalonia.RenderTests -c Release
```

On Windows, use PowerShell and clear the update flag before verification:

```powershell
$env:UPDATE_RENDER_REFERENCES = "1"
try {
    dotnet test tests/Cupertino.Avalonia.RenderTests -c Release --filter FullyQualifiedName~FieldRenderTests
} finally {
    Remove-Item Env:UPDATE_RENDER_REFERENCES
}
dotnet test tests/Cupertino.Avalonia.RenderTests -c Release
```

Choose a filter for the intended controls. An update writes only the current OS's folder. For a visual change that affects every platform, review and update all three sets, then run the normal CI checks. Do not enable reference updates in the normal CI test steps: those must compare against the committed images.

## Palette history

On 2026-09-05, ten references were updated for the light palette from commit `dca70d5738ca15966a448bb376eca37f3ad8f602`: BorderedButton, CheckBox, ComboBox, HyperlinkButton, ProminentButton, RadioButton, SearchView, Slider, ToggleButton, and the dedicated slider reference.

The accent changed from #0088FF (#0088FE for prominent tint) to #007AFF. Inspection confirmed that differences were in accent text, fills, and their antialiased edges. Geometry and tolerances stayed unchanged. Images were generated and checked on macOS ARM64.

On September 9, 2026, fresh iOS 26.2 UIKit captures measured light system blue as #0088FF and dark system blue as #0091FF. The theme now uses those measured values. The same audit corrected slider shadows, button rims and heights, stepper strokes, spinner colour and tab material. macOS and Linux references are generated on their corresponding platforms with Inter; the iOS comparison uses the system font.

On September 10, 2026, the Windows references were regenerated in Windows 11 from the same source revision and passed a second full comparison with updates disabled. No cross-platform images were copied and the comparison tolerance remains unchanged.
