using System.Globalization;
using Avalonia.Media;

namespace Cupertino.Rendering;

// Each owner has a bounded cache; brush references retain live color updates.
internal sealed class FormattedTextCache
{
    private readonly Dictionary<Key, FormattedText> _entries = new();
    private readonly Queue<Key> _order = new();
    private readonly record struct Key(string Text, CultureInfo Culture, FlowDirection Direction,
        Typeface Typeface, double Size, IBrush? Brush);

    public FormattedText Get(string text, CultureInfo culture, FlowDirection direction,
        Typeface typeface, double size, IBrush? brush)
    {
        var key = new Key(text, culture, direction, typeface, size, brush);
        if (_entries.TryGetValue(key, out var formatted))
            return formatted;
        // Evict a single oldest entry instead of clearing the cache, so a burst
        // of distinct text cannot make every subsequent lookup miss at once.
        if (_entries.Count >= 512)
            while (_order.Count > 0)
                if (_entries.Remove(_order.Dequeue()))
                    break;
        formatted = new FormattedText(text, culture, direction, typeface, size, brush);
        _entries.Add(key, formatted);
        _order.Enqueue(key);
        return formatted;
    }

    public void Clear()
    {
        _entries.Clear();
        _order.Clear();
    }
}
