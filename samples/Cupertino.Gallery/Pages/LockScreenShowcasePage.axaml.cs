using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Cupertino.Gallery.Pages;

public partial class LockScreenShowcasePage : UserControl
{
    private readonly DispatcherTimer _clock;

    public LockScreenShowcasePage()
    {
        InitializeComponent();
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();
        UpdateClock();
        AttachedToVisualTree += (_, _) => _clock.Start();
        DetachedFromVisualTree += (_, _) => _clock.Stop();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        this.FindControl<TextBlock>("ClockLine")!.Text = now.ToString("H:mm", CultureInfo.CurrentCulture);
        this.FindControl<TextBlock>("DateLine")!.Text = now.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);
    }
}
