using System.Globalization;
using Avalonia;
using Avalonia.iOS;
using Cupertino.Controls;
using Foundation;
using UIKit;

namespace Cupertino.Gallery;

[Register(nameof(AppDelegate))]
public partial class AppDelegate : AvaloniaAppDelegate<IosApp>
{
    // Inter is incompatible with iOS AOT.
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // .NET drops the iOS region override (e.g. en_US@rg=es), so take
        // the first weekday from the platform calendar.
        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.FirstDayOfWeek =
            (DayOfWeek)(((int)NSCalendar.CurrentCalendar.FirstWeekDay - 1) % 7);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;

        CupertinoHaptics.Handler = static kind =>
        {
            switch (kind)
            {
                case HapticFeedback.Selection:
                    new UISelectionFeedbackGenerator().SelectionChanged();
                    break;
                case HapticFeedback.ImpactLight:
                    new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light).ImpactOccurred();
                    break;
                case HapticFeedback.ImpactMedium:
                    new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium).ImpactOccurred();
                    break;
                case HapticFeedback.ImpactHeavy:
                    new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Heavy).ImpactOccurred();
                    break;
                case HapticFeedback.Success:
                    new UINotificationFeedbackGenerator().NotificationOccurred(UINotificationFeedbackType.Success);
                    break;
                case HapticFeedback.Warning:
                    new UINotificationFeedbackGenerator().NotificationOccurred(UINotificationFeedbackType.Warning);
                    break;
                case HapticFeedback.Error:
                    new UINotificationFeedbackGenerator().NotificationOccurred(UINotificationFeedbackType.Error);
                    break;
            }
        };

        // Register before the shell builds its catalogue.
        NativeComparisonHosts.Register();
        return base.CustomizeAppBuilder(builder);
    }
}
