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
    // Use the system font on iOS; the theme resolves it at startup.
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // .NET drops the iOS region override (e.g. en_US@rg=es), so take
        // the first weekday from the platform calendar.
        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.FirstDayOfWeek =
            (DayOfWeek)(((int)NSCalendar.CurrentCalendar.FirstWeekDay - 1) % 7);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;

        CupertinoHaptics.Handler = kind =>
        {
            switch (kind)
            {
                case HapticFeedback.Selection:
                    using (var feedback = new UISelectionFeedbackGenerator())
                        feedback.SelectionChanged();
                    break;
                case HapticFeedback.ImpactLight:
                    PlayImpact(UIImpactFeedbackStyle.Light);
                    break;
                case HapticFeedback.ImpactMedium:
                    PlayImpact(UIImpactFeedbackStyle.Medium);
                    break;
                case HapticFeedback.ImpactHeavy:
                    PlayImpact(UIImpactFeedbackStyle.Heavy);
                    break;
                case HapticFeedback.Success:
                    using (var feedback = new UINotificationFeedbackGenerator())
                        feedback.NotificationOccurred(UINotificationFeedbackType.Success);
                    break;
                case HapticFeedback.Warning:
                    using (var feedback = new UINotificationFeedbackGenerator())
                        feedback.NotificationOccurred(UINotificationFeedbackType.Warning);
                    break;
                case HapticFeedback.Error:
                    using (var feedback = new UINotificationFeedbackGenerator())
                        feedback.NotificationOccurred(UINotificationFeedbackType.Error);
                    break;
            }
        };

        // Register before the shell builds its catalogue.
        NativeComparisonHosts.Register();
        return base.CustomizeAppBuilder(builder).UseCupertinoSystemFont();
    }

    private void PlayImpact(UIImpactFeedbackStyle style)
    {
        if (OperatingSystem.IsIOSVersionAtLeast(17, 5) || OperatingSystem.IsMacCatalystVersionAtLeast(17, 5))
        {
            var view = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>()
                .Where(scene => scene.ActivationState == UISceneActivationState.ForegroundActive)
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(window => window.IsKeyWindow)?.RootViewController?.View
                ?? Window?.RootViewController?.View;
            if (view is null)
                return;
            using var feedback = UIImpactFeedbackGenerator.GetFeedbackGenerator(style, view);
            feedback.ImpactOccurred();
        }
        else
        {
            using var feedback = new UIImpactFeedbackGenerator(style);
            feedback.ImpactOccurred();
        }
    }

}
