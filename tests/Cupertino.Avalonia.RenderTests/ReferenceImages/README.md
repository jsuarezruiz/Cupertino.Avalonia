# Render references

The tests use Avalonia 12.1.1, SkiaSharp 3.119.4, the bundled Inter font, and en-US culture. Image comparisons allow a channel difference of 3 and a 0.2% pixel mismatch. The audit fixes kept these tolerances unchanged.

On 2026-09-05, ten references were updated for the light palette from commit `dca70d5738ca15966a448bb376eca37f3ad8f602`: BorderedButton, CheckBox, ComboBox, HyperlinkButton, ProminentButton, RadioButton, SearchView, Slider, ToggleButton, and the dedicated slider reference.

The accent changed from #0088FF (#0088FE for prominent tint) to #007AFF. Inspection confirmed that differences were in accent text, fills, and their antialiased edges. Geometry and tolerances stayed unchanged. Images were generated and checked on macOS ARM64.

Review actual and difference images in `TestResults/RenderDiffs` before updating references. Run `UPDATE_RENDER_REFERENCES=1 dotnet test tests/Cupertino.Avalonia.RenderTests -c Release`, using a test filter to update only the intended images. Then rerun with the environment variable unset. Linux and Windows CI checks and device profiling are still needed.
