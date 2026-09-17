# Render references

The tests use Avalonia 12.1.1, SkiaSharp 3.119.4, the bundled Inter font, and `en-US` culture. Image comparisons allow a channel difference of 3 and a 0.2% pixel mismatch.

References live in `macos`, `linux`, and `windows`. The test suite automatically selects the folder matching the current operating system, as Skia's font rasterization produces minor rendering differences across platforms.

## Updating references

Review actual and difference images in `TestResults/RenderDiffs` before updating references. CI uploads these as `render-differences-ubuntu-latest` and `render-differences-windows-latest` artifacts. Copy only reviewed `.actual.png` files from the matching OS into its reference folder, removing `.actual` from each filename. A missing reference also saves the actual image and fails the test.

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

Choose a filter for the intended controls. An update writes only to the current OS folder. For a visual change that affects every platform, review and update all three sets, then run the normal CI checks. Never enable reference updates in the normal CI test steps: those must compare against the committed baseline images.

