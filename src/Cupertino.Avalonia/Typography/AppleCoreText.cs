using System.Runtime.InteropServices;
using Avalonia.Media;

namespace Cupertino;

internal static class AppleCoreText
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreText = "/System/Library/Frameworks/CoreText.framework/CoreText";
    private const string MacObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string MobileObjectiveC = "/usr/lib/libobjc.dylib";
    private const uint Utf8Encoding = 0x08000100;

    private static readonly bool IsApplePlatform =
        OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();
    private static readonly bool IsMobile =
        OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst();
    private static readonly IntPtr FontClass =
        IsApplePlatform ? GetClass(IsMobile ? "UIFont" : "NSFont") : IntPtr.Zero;
    private static readonly IntPtr SystemFontSelector =
        IsApplePlatform ? GetSelector("systemFontOfSize:weight:") : IntPtr.Zero;
    private static readonly IntPtr MonospacedDigitFontSelector =
        IsApplePlatform ? GetSelector("monospacedDigitSystemFontOfSize:weight:") : IntPtr.Zero;
    private static readonly IntPtr FontManagerClass =
        !IsMobile && IsApplePlatform ? GetClass("NSFontManager") : IntPtr.Zero;
    private static readonly IntPtr SharedFontManagerSelector =
        !IsMobile && IsApplePlatform ? GetSelector("sharedFontManager") : IntPtr.Zero;
    private static readonly IntPtr ConvertFontSelector =
        !IsMobile && IsApplePlatform ? GetSelector("convertFont:toHaveTrait:") : IntPtr.Zero;
    private static readonly IntPtr FontAttributeName = GetCoreTextConstant("kCTFontAttributeName");

    internal readonly record struct NativeGlyph(
        ushort Glyph,
        int Cluster,
        double Advance,
        double X,
        double Y);

    private readonly record struct ShapeKey(string Text, double Size, FontWeight Weight, bool Italic, bool TabularNumbers);

    private const int CacheCapacity = 1024;
    private static readonly Dictionary<ShapeKey, NativeGlyph[]?> Cache = new();
    private static readonly Queue<ShapeKey> CacheOrder = new();
    private static readonly object CacheLock = new();

    // Fast path for re-shaping the last run without allocating a key string.
    private static ShapeKey _recentKey;
    private static NativeGlyph[]? _recentValue;
    private static bool _hasRecent;

    internal static IReadOnlyList<NativeGlyph>? Shape(
        ReadOnlySpan<char> text,
        double size,
        FontWeight weight,
        bool italic,
        bool tabularNumbers)
    {
        ShapeKey key;
        lock (CacheLock)
        {
            if (_hasRecent
                && _recentKey.Size == size && _recentKey.Weight == weight
                && _recentKey.Italic == italic && _recentKey.TabularNumbers == tabularNumbers
                && text.SequenceEqual(_recentKey.Text))
                return _recentValue;

            key = new ShapeKey(text.ToString(), size, weight, italic, tabularNumbers);
            if (Cache.TryGetValue(key, out var cached))
            {
                Remember(key, cached);
                return cached;
            }
        }

        var shaped = ShapeUncached(key.Text, size, weight, italic, tabularNumbers)?.ToArray();
        lock (CacheLock)
        {
            // FIFO eviction.
            while (CacheOrder.Count >= CacheCapacity)
                Cache.Remove(CacheOrder.Dequeue());
            if (Cache.TryAdd(key, shaped))
                CacheOrder.Enqueue(key);
            Remember(key, shaped);
        }
        return shaped;
    }

    // Keys include the requested size, so a typography scale change makes every entry stale.
    internal static void ClearCache()
    {
        lock (CacheLock)
        {
            Cache.Clear();
            CacheOrder.Clear();
            _hasRecent = false;
            _recentValue = null;
        }
    }

    private static void Remember(ShapeKey key, NativeGlyph[]? value)
    {
        _recentKey = key;
        _recentValue = value;
        _hasRecent = true;
    }

    private static List<NativeGlyph>? ShapeUncached(
        string text,
        double size,
        FontWeight weight,
        bool italic,
        bool tabularNumbers)
    {
        if (!IsApplePlatform || FontClass == IntPtr.Zero || FontAttributeName == IntPtr.Zero)
            return null;

        // UIFont does not expose NSFontManager's weighted italic conversion.
        // Preserve Avalonia's complete run instead of substituting a different face.
        if (IsMobile && italic)
            return null;

        // .NET threads have no autorelease pool for the font calls.
        var pool = AutoreleasePoolPush();
        IntPtr font;
        try
        {
            var selector = tabularNumbers ? MonospacedDigitFontSelector : SystemFontSelector;
            font = SendFont(FontClass, selector, size, ToNativeWeight(weight));
            if (font == IntPtr.Zero)
                return null;

            if (italic)
            {
                var manager = Send(FontManagerClass, SharedFontManagerSelector);
                var converted = SendConvertFont(manager, ConvertFontSelector, font, 1);
                if (converted != IntPtr.Zero)
                    font = converted;
            }

            CFRetain(font);
        }
        finally
        {
            AutoreleasePoolPop(pool);
        }

        IntPtr value = IntPtr.Zero;
        IntPtr attributes = IntPtr.Zero;
        IntPtr attributed = IntPtr.Zero;
        IntPtr line = IntPtr.Zero;
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            value = CFStringCreateWithBytes(
                IntPtr.Zero, bytes, bytes.Length, Utf8Encoding, false);
            if (value == IntPtr.Zero)
                return null;

            attributes = CFDictionaryCreate(
                IntPtr.Zero, [FontAttributeName], [font], 1, IntPtr.Zero, IntPtr.Zero);
            if (attributes == IntPtr.Zero)
                return null;

            attributed = CFAttributedStringCreate(IntPtr.Zero, value, attributes);
            if (attributed == IntPtr.Zero)
                return null;

            line = CTLineCreateWithAttributedString(attributed);
            if (line == IntPtr.Zero)
                return null;

            var runs = CTLineGetGlyphRuns(line);
            var result = new List<NativeGlyph>();
            var runCount = checked((int)CFArrayGetCount(runs));
            for (var runIndex = 0; runIndex < runCount; runIndex++)
            {
                var run = CFArrayGetValueAtIndex(runs, runIndex);
                var count = checked((int)CTRunGetGlyphCount(run));
                if (count == 0)
                    continue;

                var glyphs = new ushort[count];
                var clusters = new nint[count];
                var advances = new NativeSize[count];
                var positions = new NativePoint[count];
                var fullRange = new NativeRange(0, 0);
                CTRunGetGlyphs(run, fullRange, glyphs);
                CTRunGetStringIndices(run, fullRange, clusters);
                CTRunGetAdvances(run, fullRange, advances);
                CTRunGetPositions(run, fullRange, positions);
                for (var i = 0; i < count; i++)
                    result.Add(new NativeGlyph(
                        glyphs[i], checked((int)clusters[i]), advances[i].Width,
                        positions[i].X, positions[i].Y));
            }
            return result;
        }
        finally
        {
            if (line != IntPtr.Zero)
                CFRelease(line);
            if (attributed != IntPtr.Zero)
                CFRelease(attributed);
            if (attributes != IntPtr.Zero)
                CFRelease(attributes);
            if (value != IntPtr.Zero)
                CFRelease(value);
            CFRelease(font);
        }
    }

    private static double ToNativeWeight(FontWeight weight) => (int)weight switch
    {
        <= 100 => -0.800000011920929,
        <= 200 => -0.6000000238418579,
        <= 300 => -0.4000000059604645,
        <= 400 => 0,
        <= 500 => 0.23000000417232513,
        <= 600 => 0.30000001192092896,
        <= 700 => 0.4000000059604645,
        <= 800 => 0.5600000023841858,
        _ => 0.6200000047683716,
    };

    private static IntPtr GetCoreTextConstant(string name)
    {
        if (!IsApplePlatform)
            return IntPtr.Zero;
        var framework = NativeLibrary.Load(CoreText);
        return Marshal.ReadIntPtr(NativeLibrary.GetExport(framework, name));
    }

    private static IntPtr GetClass(string name) => IsMobile
        ? MobileGetClass(name)
        : MacGetClass(name);

    private static IntPtr GetSelector(string name) => IsMobile
        ? MobileGetSelector(name)
        : MacGetSelector(name);

    private static IntPtr Send(IntPtr receiver, IntPtr selector) => IsMobile
        ? MobileSend(receiver, selector)
        : MacSend(receiver, selector);

    private static IntPtr AutoreleasePoolPush() => IsMobile
        ? MobilePoolPush()
        : MacPoolPush();

    private static void AutoreleasePoolPop(IntPtr pool)
    {
        if (IsMobile)
            MobilePoolPop(pool);
        else
            MacPoolPop(pool);
    }

    private static IntPtr SendFont(
        IntPtr receiver,
        IntPtr selector,
        double size,
        double weight) => IsMobile
            ? MobileSendFont(receiver, selector, size, weight)
            : MacSendFont(receiver, selector, size, weight);

    private static IntPtr SendConvertFont(
        IntPtr receiver,
        IntPtr selector,
        IntPtr font,
        nuint trait) => IsMobile
            ? MobileSendConvertFont(receiver, selector, font, trait)
            : MacSendConvertFont(receiver, selector, font, trait);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeRange(nint Location, nint Length);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(double X, double Y);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeSize(double Width, double Height);

    [DllImport(MacObjectiveC, EntryPoint = "objc_getClass", CharSet = CharSet.Ansi,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr MacGetClass(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(MobileObjectiveC, EntryPoint = "objc_getClass", CharSet = CharSet.Ansi,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr MobileGetClass(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(MacObjectiveC, EntryPoint = "sel_registerName", CharSet = CharSet.Ansi,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr MacGetSelector(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(MobileObjectiveC, EntryPoint = "sel_registerName", CharSet = CharSet.Ansi,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr MobileGetSelector(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(MacObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    private static extern IntPtr MacPoolPush();

    [DllImport(MobileObjectiveC, EntryPoint = "objc_autoreleasePoolPush")]
    private static extern IntPtr MobilePoolPush();

    [DllImport(MacObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    private static extern void MacPoolPop(IntPtr pool);

    [DllImport(MobileObjectiveC, EntryPoint = "objc_autoreleasePoolPop")]
    private static extern void MobilePoolPop(IntPtr pool);

    [DllImport(MacObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MacSend(IntPtr receiver, IntPtr selector);

    [DllImport(MobileObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MobileSend(IntPtr receiver, IntPtr selector);

    [DllImport(MacObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MacSendFont(
        IntPtr receiver, IntPtr selector, double size, double weight);

    [DllImport(MobileObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MobileSendFont(
        IntPtr receiver, IntPtr selector, double size, double weight);

    [DllImport(MacObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MacSendConvertFont(
        IntPtr receiver, IntPtr selector, IntPtr font, nuint trait);

    [DllImport(MobileObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr MobileSendConvertFont(
        IntPtr receiver, IntPtr selector, IntPtr font, nuint trait);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFRetain(IntPtr value);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr value);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithBytes(
        IntPtr allocator,
        byte[] bytes,
        nint byteCount,
        uint encoding,
        [MarshalAs(UnmanagedType.I1)] bool isExternalRepresentation);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryCreate(
        IntPtr allocator,
        IntPtr[] keys,
        IntPtr[] values,
        nint count,
        IntPtr keyCallbacks,
        IntPtr valueCallbacks);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFAttributedStringCreate(
        IntPtr allocator, IntPtr value, IntPtr attributes);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport(CoreText)]
    private static extern IntPtr CTLineCreateWithAttributedString(IntPtr attributedString);

    [DllImport(CoreText)]
    private static extern IntPtr CTLineGetGlyphRuns(IntPtr line);

    [DllImport(CoreText)]
    private static extern nint CTRunGetGlyphCount(IntPtr run);

    [DllImport(CoreText)]
    private static extern void CTRunGetGlyphs(
        IntPtr run, NativeRange range, [Out] ushort[] glyphs);

    [DllImport(CoreText)]
    private static extern void CTRunGetStringIndices(
        IntPtr run, NativeRange range, [Out] nint[] indices);

    [DllImport(CoreText)]
    private static extern void CTRunGetAdvances(
        IntPtr run, NativeRange range, [Out] NativeSize[] advances);

    [DllImport(CoreText)]
    private static extern void CTRunGetPositions(
        IntPtr run, NativeRange range, [Out] NativePoint[] positions);
}
