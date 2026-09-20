using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Cupertino.Gallery.Pages;

/// <summary>
/// Displays highlighted XAML source.
/// </summary>
public sealed class SourcePage : UserControl
{
    private TextMate.Installation? _textMate;
    private readonly TextEditor _editor;
    private readonly RegistryOptions _registry;

    public SourcePage(string xaml)
    {
        var editor = _editor = new TextEditor
        {
            Text = xaml,
            IsReadOnly = true,
            ShowLineNumbers = true,
            FontFamily = new FontFamily("Menlo,Consolas,monospace"),
            FontSize = 12,
            Background = Brushes.Transparent,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        editor.Options.AllowScrollBelowDocument = false;

        _registry = new RegistryOptions(ThemeName.LightPlus);

        var card = new Border
        {
            Margin = new Avalonia.Thickness(16, 84, 16, 24),
            Padding = new Avalonia.Thickness(6),
            CornerRadius = new Avalonia.CornerRadius(10),
            ClipToBounds = true,
            Child = editor,
        };
        card.Bind(Border.BackgroundProperty, this.GetResourceObservable("CupertinoCardBrush"));
        Content = card;

        ActualThemeVariantChanged += (_, _) => ApplyEditorTheme();
        ApplyEditorTheme();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _textMate = _editor.InstallTextMate(_registry);
        _textMate.SetGrammar(_registry.GetScopeByLanguageId(_registry.GetLanguageByExtension(".xml").Id));
        ApplyEditorTheme();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _textMate?.Dispose();
        _textMate = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ApplyEditorTheme() =>
        _textMate?.SetTheme(_registry.LoadTheme(
            ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus));
}
