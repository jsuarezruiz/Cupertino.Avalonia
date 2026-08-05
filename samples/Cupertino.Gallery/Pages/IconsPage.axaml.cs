using Avalonia.Controls;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

public partial class IconsPage : UserControl
{
    public IconsPage()
    {
        InitializeComponent();

        var grid = this.FindControl<WrapPanel>("IconGrid")!;
        foreach (var glyph in CupertinoIcon.Glyphs.OrderBy(g => g, StringComparer.Ordinal))
        {
            var cell = new StackPanel
            {
                Width = 82,
                Spacing = 6,
                Children =
                {
                    new CupertinoIcon
                    {
                        Glyph = glyph, Size = 26,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    },
                    new TextBlock
                    {
                        // Add wrap points after dots.
                        Text = glyph.Replace(".", ".\u200B"), FontSize = 10,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        TextAlignment = Avalonia.Media.TextAlignment.Center,
                    },
                },
            };
            ToolTip.SetTip(cell, glyph);
            grid.Children.Add(cell);
        }
    }
}
