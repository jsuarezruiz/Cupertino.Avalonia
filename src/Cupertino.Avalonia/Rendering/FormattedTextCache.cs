using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Cupertino.Rendering;

// Each owner has a bounded cache; brush references retain live color updates.
internal sealed class FormattedTextCache
{
    private readonly Dictionary<Key, CachedText> _entries = new();
    private readonly Queue<Key> _order = new();
    private readonly record struct Key(string Text, CultureInfo Culture, FlowDirection Direction,
        Typeface Typeface, double Size, IBrush? Brush);

    public CachedText Get(string text, CultureInfo culture, FlowDirection direction,
        Typeface typeface, double size, IBrush? brush)
    {
        var key = new Key(text, culture, direction, typeface, size, brush);
        if (_entries.TryGetValue(key, out var cached))
            return cached;
        // Evict a single oldest entry instead of clearing the cache, so a burst
        // of distinct text cannot make every subsequent lookup miss at once.
        if (_entries.Count >= 512)
            while (_order.Count > 0)
                if (_entries.Remove(_order.Dequeue(), out var evicted))
                {
                    evicted.Dispose();
                    break;
                }
        cached = new CachedText(text, culture, direction, typeface, size, brush);
        _entries.Add(key, cached);
        _order.Enqueue(key);
        return cached;
    }

    public void Clear()
    {
        foreach (var entry in _entries.Values)
            entry.Dispose();
        _entries.Clear();
        _order.Clear();
    }
}

// FormattedText reformats its lines on every draw; keep the single line it would produce.
internal sealed class CachedText : IDisposable
{
    private readonly FormattedText _formatted;
    private readonly string _text;
    private readonly CultureInfo _culture;
    private readonly FlowDirection _direction;
    private readonly Typeface _typeface;
    private readonly double _size;
    private readonly IBrush? _brush;
    private TextLine? _line;
    private bool _lineResolved;

    public CachedText(string text, CultureInfo culture, FlowDirection direction,
        Typeface typeface, double size, IBrush? brush)
    {
        _formatted = new FormattedText(text, culture, direction, typeface, size, brush);
        (_text, _culture, _direction, _typeface, _size, _brush) = (text, culture, direction, typeface, size, brush);
    }

    public double Width => _formatted.Width;

    public double Height => _formatted.Height;

    public void Draw(DrawingContext context, Point origin)
    {
        if (!_lineResolved)
        {
            _line = CreateLine();
            _lineResolved = true;
        }
        if (_line is { } line)
            line.Draw(context, origin);
        else
            context.DrawText(_formatted, origin);
    }

    // Same run and paragraph properties that FormattedText uses internally.
    private TextLine? CreateLine()
    {
        // Any mandatory break makes FormattedText lay out more than one line.
        if (_text.Length == 0 || _text.AsSpan().IndexOfAny(LineBreaks) >= 0)
            return null;
        var run = new GenericTextRunProperties(_typeface, _size, null, _brush, null,
            BaselineAlignment.Baseline, _culture);
        var paragraph = new GenericTextParagraphProperties(_direction, TextAlignment.Left,
            false, false, run, TextWrapping.WrapWithOverflow, 0, 0, 0);
        return TextFormatter.Current.FormatLine(new SingleRunSource(_text, run),
            0, double.PositiveInfinity, paragraph);
    }

    private static readonly System.Buffers.SearchValues<char> LineBreaks =
        System.Buffers.SearchValues.Create("\r\n\u000B\u000C\u0085\u2028\u2029");

    public void Dispose() => _line?.Dispose();

    private sealed class SingleRunSource(string text, TextRunProperties properties) : ITextSource
    {
        public TextRun? GetTextRun(int textSourceIndex) =>
            textSourceIndex < text.Length
                ? new TextCharacters(text.AsMemory(textSourceIndex), properties)
                : null;
    }
}
