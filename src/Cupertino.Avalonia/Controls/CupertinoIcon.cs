using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Cupertino.Controls;

/// <summary>
/// Renders a glyph from the Cupertino icon set.
/// </summary>
public class CupertinoIcon : Control
{
    private readonly record struct Spec(string Key, double Box, bool Filled, double Stroke = 0,
                                        string? FillOverlayKey = null);

    private static readonly Dictionary<string, Spec> Specs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["magnifyingglass"] = new("CupertinoSearchGeometry", 22, false, 2.3),
        ["xmark"] = new("CupertinoXMarkGeometry", 16, false, 2),
        ["checkmark"] = new("CupertinoCheckmarkGeometry", 16, false, 2),
        ["chevron.left"] = new("CupertinoChevronLeftGeometry", 16, false, 2),
        ["chevron.right"] = new("CupertinoChevronRightGeometry", 16, false, 2),
        ["chevron.down"] = new("CupertinoChevronDownGeometry", 16, false, 2),
        ["chevron.up.chevron.down"] = new("CupertinoChevronUpDownGeometry", 16, false, 1.9),
        ["house"] = new("CupertinoHouseGeometry", 24, false, 2.0,
                        "CupertinoHouseDetailsGeometry"),
        ["house.fill"] = new("CupertinoHouseFillGeometry", 24, true),
        ["circle.half"] = new("CupertinoCircleHalfGeometry", 24, true),
        ["sparkle"] = new("CupertinoSparkleGeometry", 24, true),
        ["gear"] = new("CupertinoGearGeometry", 24, true),
        ["gearshape"] = new("CupertinoGearShapeGeometry", 24, false, 1.7),
        ["gearshape.fill"] = new("CupertinoGearShapeGeometry", 24, true),
        ["play"] = new("CupertinoPlayGeometry", 24, true),
        ["pause"] = new("CupertinoPauseGeometry", 24, true),
        ["forward"] = new("CupertinoForwardGeometry", 24, true),
        ["backward"] = new("CupertinoBackwardGeometry", 24, true),
        ["speaker"] = new("CupertinoSpeakerGeometry", 24, true),
        ["speaker.waves"] = new("CupertinoSpeakerWavesGeometry", 24, false, 1.6),
        ["flashlight"] = new("CupertinoFlashlightGeometry", 24, true),
        ["camera"] = new("CupertinoCameraGeometry", 24, true),
        ["location"] = new("CupertinoLocationArrowGeometry", 24, true),
        ["music.note"] = new("CupertinoMusicNoteGeometry", 24, true),
        ["photo"] = new("CupertinoPhotoGeometry", 24, true),
        ["square.stack"] = new("CupertinoStackGeometry", 24, true),
        ["line.3.horizontal.decrease"] = new("CupertinoFilterGeometry", 24, false, 2),
        ["chevron.left.forwardslash.chevron.right"] = new("CupertinoCodeGeometry", 24, false, 2),
        ["person"] = new("CupertinoPersonGeometry", 24, true),
        ["lock"] = new("CupertinoLockGeometry", 24, true),
        ["plus"] = new("CupertinoPlusGeometry", 24, false, 2.2),
        ["minus"] = new("CupertinoMinusGeometry", 24, false, 2.2),
        ["ellipsis"] = new("CupertinoEllipsisGeometry", 24, true),
        ["trash"] = new("CupertinoTrashGeometry", 24, false, 1.8),
        ["pencil"] = new("CupertinoPencilGeometry", 24, false, 1.9),
        ["square.and.arrow.up"] = new("CupertinoShareGeometry", 24, false, 1.8),
        ["heart"] = new("CupertinoHeartGeometry", 24, false, 1.8),
        ["heart.fill"] = new("CupertinoHeartFillGeometry", 24, true),
        ["star"] = new("CupertinoStarGeometry", 24, false, 1.8),
        ["star.fill"] = new("CupertinoStarGeometry", 24, true),
        ["bell"] = new("CupertinoBellGeometry", 24, false, 1.8),
        ["calendar"] = new("CupertinoCalendarGeometry", 24, false, 1.8),
        ["clock"] = new("CupertinoClockGeometry", 24, false, 1.8),
        ["bookmark"] = new("CupertinoBookmarkGeometry", 24, false, 1.8),
        ["bookmark.fill"] = new("CupertinoBookmarkGeometry", 24, true),
        ["envelope"] = new("CupertinoEnvelopeGeometry", 24, false, 1.8),
        ["folder"] = new("CupertinoFolderGeometry", 24, false, 1.8),
        ["doc"] = new("CupertinoDocumentGeometry", 24, false, 1.8),
        ["list.bullet"] = new("CupertinoListGeometry", 24, false, 1.8),
        ["info.circle"] = new("CupertinoInfoGeometry", 24, false, 1.8),
        ["exclamationmark.triangle"] = new("CupertinoWarningGeometry", 24, false, 1.8),
        ["arrow.clockwise"] = new("CupertinoRefreshGeometry", 24, false, 1.9),
    };

    /// <summary>
    /// Every glyph name the set ships, for catalogues and tests.
    /// </summary>
    public static IReadOnlyCollection<string> Glyphs => Specs.Keys;

    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<CupertinoIcon, string?>(nameof(Glyph));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<CupertinoIcon, double>(nameof(Size), 24);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<CupertinoIcon, IBrush?>(nameof(Foreground));

    /// <summary>
    /// Gets or sets the output-space stroke thickness, or NaN for the authored weight.
    /// </summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CupertinoIcon, double>(nameof(StrokeThickness), double.NaN);


    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    static CupertinoIcon()
    {
        AffectsRender<CupertinoIcon>(GlyphProperty, ForegroundProperty, StrokeThicknessProperty);
        AffectsMeasure<CupertinoIcon>(SizeProperty);
    }

    public CupertinoIcon()
    {
        // Use the label colour unless locally overridden.
        this.Bind(ForegroundProperty, this.GetResourceObservable("CupertinoLabelBrush"),
                  Avalonia.Data.BindingPriority.Style);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Glyph is null || !Specs.TryGetValue(Glyph, out var spec)
            || this.FindResource(spec.Key) is not Geometry geometry
            || Foreground is not { } brush)
            return;

        using (context.PushTransform(Matrix.CreateScale(Size / spec.Box, Size / spec.Box)))
        {
            if (spec.Filled)
                context.DrawGeometry(brush, null, geometry);
            else
            {
                // Convert an output stroke width to design units.
                var stroke = double.IsNaN(StrokeThickness)
                    ? spec.Stroke
                    : StrokeThickness * spec.Box / Size;
                context.DrawGeometry(null,
                    new Pen(brush, stroke) { LineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round },
                    geometry);
            }

            // Paint solid details after the outline.
            if (spec.FillOverlayKey is { } overlayKey
                && this.FindResource(overlayKey) is Geometry overlay)
                context.DrawGeometry(brush, null, overlay);
        }
    }
}
