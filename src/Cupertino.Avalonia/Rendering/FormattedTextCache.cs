using System.Globalization;
using Avalonia.Media;

namespace Cupertino.Rendering;

// Each owner has a bounded cache; brush references retain live color updates.
internal sealed class FormattedTextCache
{
    private readonly Dictionary<Key, FormattedText> _entries = new();
    private readonly record struct Key(string Text, CultureInfo Culture, FlowDirection Direction,
        Typeface Typeface, double Size, IBrush? Brush);

    public FormattedText Get(string text, CultureInfo culture, FlowDirection direction,
        Typeface typeface, double size, IBrush? brush)
    {
        var key = new Key(text, culture, direction, typeface, size, brush);
        if (_entries.TryGetValue(key, out var formatted))
            return formatted;
        if (_entries.Count >= 512)
            _entries.Clear();
        formatted = new FormattedText(text, culture, direction, typeface, size, brush);
        _entries.Add(key, formatted);
        return formatted;
    }

    public void Clear() => _entries.Clear();
}
