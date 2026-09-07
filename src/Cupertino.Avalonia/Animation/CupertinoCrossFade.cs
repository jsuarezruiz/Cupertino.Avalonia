using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Cupertino.Controls;

namespace Cupertino.Animation;

/// <summary>
/// A page crossfade that skips animation when Reduce Motion is enabled.
/// </summary>
public sealed class CupertinoCrossFade : IPageTransition
{
    /// <summary>
    /// The duration used when motion is enabled; defaults to 250 milliseconds.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <inheritdoc/>
    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (CupertinoAccessibility.ReduceMotion)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (from is not null)
                from.IsVisible = false;
            if (to is not null)
                to.IsVisible = true;
            return Task.CompletedTask;
        }
        return new CrossFade(Duration).Start(from, to, cancellationToken);
    }
}
