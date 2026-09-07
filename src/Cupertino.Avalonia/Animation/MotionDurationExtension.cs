using Avalonia;
using Avalonia.Data;
using Cupertino.Controls;

namespace Cupertino.Animation;

/// <summary>
/// Binds a transition duration directly to the reduced-motion preference.
/// </summary>
public sealed class MotionDurationExtension
{
    private static readonly List<WeakReference<Subscription>> Subscribers = new();
    private static int _registrations;
    private readonly TimeSpan _duration;

    static MotionDurationExtension() => CupertinoAccessibility.Changed += OnSettingsChanged;

    /// <summary>
    /// Creates a duration binding with the supplied normal duration in milliseconds.
    /// </summary>
    public MotionDurationExtension(int milliseconds) =>
        _duration = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));

    /// <summary>
    /// Returns a binding that emits zero while Reduce Motion is enabled.
    /// </summary>
    public BindingBase ProvideValue(IServiceProvider serviceProvider) => new DurationObservable(_duration).ToBinding();

    private static void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Publishing can attach or detach bindings; iterate a snapshot.
        foreach (var weak in Subscribers.ToArray())
            if (weak.TryGetTarget(out var subscription))
                subscription.Publish();
        Prune();
    }

    private static void Prune() => Subscribers.RemoveAll(weak =>
        !weak.TryGetTarget(out var subscription) || subscription.IsDisposed);

    private sealed class DurationObservable(TimeSpan duration) : IObservable<TimeSpan>
    {
        public IDisposable Subscribe(IObserver<TimeSpan> observer)
        {
            // The binding owns the subscription. The global preference must not own controls.
            var subscription = new Subscription(observer, duration);
            if (++_registrations % 64 == 0)
                Prune();
            Subscribers.Add(new WeakReference<Subscription>(subscription));
            subscription.Publish();
            return subscription;
        }
    }

    private sealed class Subscription(IObserver<TimeSpan> observer, TimeSpan duration) : IDisposable
    {
        private IObserver<TimeSpan>? _observer = observer;
        public bool IsDisposed => _observer is null;
        public void Publish() => _observer?.OnNext(CupertinoAccessibility.ReduceMotion ? TimeSpan.Zero : duration);
        public void Dispose() => _observer = null;
    }
}
