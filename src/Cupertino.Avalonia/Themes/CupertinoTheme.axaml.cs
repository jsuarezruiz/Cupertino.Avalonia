using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Cupertino.Controls;

namespace Cupertino.Themes;

/// <summary>
/// The Cupertino theme for Avalonia. Add it after the base theme.
/// </summary>
public class CupertinoTheme : Styles
{
    public static readonly StyledProperty<Color?> AccentProperty =
        AvaloniaProperty.Register<CupertinoTheme, Color?>(nameof(Accent));

    public Color? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    // Preserve each role's alpha when changing the accent.
    private static readonly (string Key, byte LightAlpha, byte DarkAlpha, bool IsColor)[] AccentRoles =
    [
        ("CupertinoAccentBrush", 0xFF, 0xFF, false),
        ("CupertinoFocusRingBrush", 0x99, 0x99, false),
        ("CupertinoSelectionBrush", 0x4D, 0x4D, false),
        ("CupertinoAccentSubtleBrush", 0x1F, 0x2E, false),
        ("CupertinoProminentTint", 0xFF, 0xFF, true),
    ];

    private static readonly Color LightBlue = Color.FromRgb(0x00, 0x88, 0xFF);
    private static readonly Color DarkBlue = Color.FromRgb(0x0A, 0x84, 0xFF);

    private static readonly Color LightTabBlue = Color.FromRgb(0x00, 0x53, 0xDB);

    public CupertinoTheme(IServiceProvider? sp = null)
    {
        CupertinoFlyoutTransition.Initialize();
        AvaloniaXamlLoader.Load(sp, this);
        Resources["CupertinoFontFamily"] = ResolveSystemFont();
        ApplyTextScale();
        var weakTheme = new WeakReference<CupertinoTheme>(this);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (weakTheme.TryGetTarget(out var theme))
                theme.ApplyTextScale();
            else
                CupertinoAccessibility.Changed -= handler;
        };
        CupertinoAccessibility.Changed += handler;
    }

    private void ApplyTextScale()
    {
        var scale = CupertinoAccessibility.TextScaleFactor;
        foreach (var baseline in new[] { 10d, 11d, 12d, 13d, 14d, 15d, 16d, 17d, 18d,
                                          20d, 22d, 24d, 28d, 34d, 35d })
            Resources[$"CupertinoFontSize{baseline:0}"] = baseline * scale;

        foreach (var (key, baseline) in new[]
                 {
                     ("LargeTitle", 41d), ("Title1", 34d), ("Title2", 28d),
                     ("Title3", 25d), ("Headline", 22d), ("Body", 22d),
                     ("Callout", 21d), ("Subheadline", 20d), ("Footnote", 18d),
                     ("Caption1", 16d), ("Caption2", 13d),
                 })
            Resources[$"CupertinoLineHeight{key}"] = baseline * scale;
    }

    private static FontFamily ResolveSystemFont()
    {
        string[] candidates =
        [
            "SF Pro Text", "SF Pro Display", "SF Pro",
            ".AppleSystemUIFont",
            "Helvetica Neue", "Segoe UI", "Roboto", "Inter",
        ];

        // Reject candidates that resolve to the default fallback.
        FontManager.Current.TryGetGlyphTypeface(
            new Typeface(new FontFamily("cupertino-no-such-font")), out var fallback);

        foreach (var name in candidates)
        {
            if (FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(name)), out var glyph)
                && glyph.FamilyName != fallback?.FamilyName)
                return new FontFamily(name);
        }

        return FontFamily.Default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentProperty)
            ApplyAccent(change.GetNewValue<Color?>());
    }

    private void ApplyAccent(Color? accent)
    {
        if (Resources is not ResourceDictionary resources)
            return;

        foreach (var (variantKey, fallback) in new[]
                 { (ThemeVariant.Light, LightBlue), (ThemeVariant.Dark, DarkBlue) })
        {
            if (!resources.ThemeDictionaries.TryGetValue(variantKey, out var provider)
                || provider is not ResourceDictionary variant)
                continue;

            var hue = accent ?? fallback;
            foreach (var role in AccentRoles)
            {
                var alpha = variantKey == ThemeVariant.Light ? role.LightAlpha : role.DarkAlpha;
                var color = Color.FromArgb(alpha, hue.R, hue.G, hue.B);
                variant[role.Key] = role.IsColor ? color : new SolidColorBrush(color);
            }

            // Light glass deepens the accent; dark glass preserves it.
            variant["CupertinoTabAccentBrush"] = new SolidColorBrush(
                variantKey != ThemeVariant.Light ? hue
                : accent is { } a ? Color.FromRgb((byte)(a.R * 0.85), (byte)(a.G * 0.85), (byte)(a.B * 0.85))
                : LightTabBlue);
        }
    }
}
