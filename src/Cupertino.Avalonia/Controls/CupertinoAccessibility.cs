using Avalonia.Threading;

namespace Cupertino.Controls;

/// <summary>
/// One snapshot of the operating system's accessibility preferences.
/// </summary>
/// <param name="ReduceTransparency">Use flat opaque glass fills.</param>
/// <param name="ReduceMotion">Skip decorative transitions.</param>
/// <param name="TextScaleFactor">Scale typography tokens; values are clamped to 0.8–2.35 when applied.</param>
public readonly record struct CupertinoAccessibilitySettings(
    bool ReduceTransparency,
    bool ReduceMotion,
    double TextScaleFactor = 1.0);

/// <summary>
/// Supplies platform accessibility preferences.
/// </summary>
public interface ICupertinoAccessibilityProvider
{
    /// <summary>
    /// The provider's latest platform accessibility snapshot.
    /// </summary>
    CupertinoAccessibilitySettings Current { get; }

    /// <summary>
    /// Raised when <see cref="Current"/> changes.
    /// </summary>
    event EventHandler? Changed;
}

/// <summary>
/// Controls accessibility behavior for the theme.
/// </summary>
/// <remarks>
/// Every setter applies on the UI thread. Called from any other thread it posts the change
/// and returns immediately, so a getter read straight afterwards still returns the old value.
/// </remarks>
public static class CupertinoAccessibility
{
    private static bool _reduceTransparency;
    private static bool _reduceMotion;
    private static double _textScaleFactor = 1.0;
    private static ICupertinoAccessibilityProvider? _provider;

    /// <summary>
    /// Gets or sets whether glass uses a flat fill.
    /// </summary>
    public static bool ReduceTransparency
    {
        get => _reduceTransparency;
        set => RunOnUiThread(() =>
        {
            if (_reduceTransparency == value)
                return;
            _reduceTransparency = value;
            Changed?.Invoke(null, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Gets or sets whether motion is reduced.
    /// </summary>
    public static bool ReduceMotion
    {
        get => _reduceMotion;
        set => RunOnUiThread(() =>
        {
            if (_reduceMotion == value)
                return;
            _reduceMotion = value;
            Changed?.Invoke(null, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Gets or sets the Dynamic Type scale.
    /// </summary>
    public static double TextScaleFactor
    {
        get => _textScaleFactor;
        set => RunOnUiThread(() =>
        {
            var next = Math.Clamp(double.IsFinite(value) ? value : 1.0, 0.8, 2.35);
            if (Math.Abs(_textScaleFactor - next) < 0.0001)
                return;
            _textScaleFactor = next;
            Changed?.Invoke(null, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Gets or sets the host-supplied platform accessibility provider. No provider is installed automatically. Replacing it removes the old event subscription; the host owns disposal of native observers.
    /// </summary>
    public static ICupertinoAccessibilityProvider? Provider
    {
        get => _provider;
        set => RunOnUiThread(() =>
        {
            if (ReferenceEquals(_provider, value))
                return;
            if (_provider is not null)
                _provider.Changed -= OnProviderChanged;
            _provider = value;
            if (_provider is not null)
            {
                _provider.Changed += OnProviderChanged;
                Apply(_provider.Current);
            }
        });
    }

    /// <summary>
    /// Reloads the current provider.
    /// </summary>
    public static void Refresh() => OnProviderChanged(null, EventArgs.Empty);

    private static void OnProviderChanged(object? sender, EventArgs e)
    {
        var provider = _provider;
        if (provider is null)
            return;
        RunOnUiThread(() =>
        {
            if (ReferenceEquals(_provider, provider))
                Apply(provider.Current);
        });
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private static void Apply(CupertinoAccessibilitySettings settings)
    {
        var clampedScale = Math.Clamp(
            double.IsFinite(settings.TextScaleFactor) ? settings.TextScaleFactor : 1.0,
            0.8, 2.35);
        var changed = _reduceTransparency != settings.ReduceTransparency
                      || _reduceMotion != settings.ReduceMotion
                      || Math.Abs(_textScaleFactor - clampedScale) >= 0.0001;
        _reduceTransparency = settings.ReduceTransparency;
        _reduceMotion = settings.ReduceMotion;
        _textScaleFactor = clampedScale;
        if (changed)
            Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Raised when an accessibility setting changes.
    /// </summary>
    public static event EventHandler? Changed;
}
