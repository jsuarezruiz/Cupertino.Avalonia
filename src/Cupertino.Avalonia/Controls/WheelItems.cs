using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace Cupertino.Controls;

// Picker columns share one immutable list per culture and remember its measured width.
internal sealed class WheelItems : ReadOnlyCollection<string>
{
    private const int MaxEntries = 32;
    private static readonly ConditionalWeakTable<CultureInfo, Dictionary<string, WheelItems>> Shared = new();
    private readonly Dictionary<(CultureInfo, FontFamily, double, FlowDirection), double> _widths = new();

    private WheelItems(string[] items) : base(items) { }

    public static WheelItems Get(CultureInfo culture, string key, Func<CultureInfo, string[]> create)
    {
        // A writable culture can change after the list is built.
        if (!culture.IsReadOnly)
            return new WheelItems(create(culture));
        var lists = Shared.GetOrCreateValue(culture);
        if (!lists.TryGetValue(key, out var items))
        {
            if (lists.Count >= MaxEntries)
                lists.Clear();
            lists[key] = items = new WheelItems(create(culture));
        }
        return items;
    }

    public bool TryGetWidth((CultureInfo, FontFamily, double, FlowDirection) style, out double width) =>
        _widths.TryGetValue(style, out width);

    public void SetWidth((CultureInfo, FontFamily, double, FlowDirection) style, double width)
    {
        if (_widths.Count >= MaxEntries)
            _widths.Clear();
        _widths[style] = width;
    }
}
