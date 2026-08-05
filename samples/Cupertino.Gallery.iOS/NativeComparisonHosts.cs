using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Platform;
using Foundation;
using UIKit;

namespace Cupertino.Gallery;

internal sealed class UiKitHost : NativeControlHost
{
    private readonly Func<UIView> _create;

    public UiKitHost(Func<UIView> create) => _create = create;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent) =>
        new UIViewControlHandle(_create());

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (control is UIViewControlHandle handle)
            handle.Destroy();
        else
            base.DestroyNativeControlCore(control);
    }
}

internal static class NativeComparisonHosts
{
    public static void Register()
    {
        NativeComparison.CreateNativeControl = kind => kind switch
        {
            "switch" => new UiKitHost(() =>
            {
                var view = new UISwitch();
                view.On = true;
                return view;
            })
            { Width = 51, Height = 31 },

            "slider" => new UiKitHost(() =>
            {
                var view = new UISlider();
                view.Value = 0.4f;
                return view;
            })
            { Width = 110, Height = 34 },

            "segmented" => new UiKitHost(() =>
            {
                var view = new UISegmentedControl();
                view.InsertSegment("One", 0, false);
                view.InsertSegment("Two", 1, false);
                view.SelectedSegment = 0;
                return view;
            })
            { Width = 110, Height = 32 },

            "progress" => new UiKitHost(() =>
            {
                var view = new UIProgressView(UIProgressViewStyle.Default);
                view.Progress = 0.6f;
                return view;
            })
            { Width = 110, Height = 4 },

            "spinner" => new UiKitHost(() =>
            {
                var view = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium);
                view.StartAnimating();
                return view;
            })
            { Width = 20, Height = 20 },

            "button-prominent" => new UiKitHost(() =>
            {
                var configuration = OperatingSystem.IsIOSVersionAtLeast(26)
                    ? UIButtonConfiguration.ProminentGlassButtonConfiguration
                    : UIButtonConfiguration.FilledButtonConfiguration;
                configuration.Title = "Button";
                return UIButton.GetButton(configuration, null);
            })
            { Width = 100, Height = 36 },

            "button-plain" => new UiKitHost(() =>
            {
                var configuration = UIButtonConfiguration.PlainButtonConfiguration;
                configuration.Title = "Button";
                return UIButton.GetButton(configuration, null);
            })
            { Width = 100, Height = 36 },

            "stepper" => new UiKitHost(() =>
            {
                var view = new UIStepper();
                view.Value = 5;
                return view;
            })
            { Width = 94, Height = 32 },

            "tabbar" => new UiKitHost(() =>
            {
                var view = new UITabBar();
                var home = new UITabBarItem(
                    "Home", UIImage.GetSystemImage("house"), UIImage.GetSystemImage("house.fill"));
                var settings = new UITabBarItem(
                    "Settings", UIImage.GetSystemImage("gearshape"), UIImage.GetSystemImage("gearshape.fill"));
                view.Items = [home, settings];
                view.SelectedItem = home;
                return view;
            })
            { Width = 160, Height = 83 },

            "navbar" => new UiKitHost(() =>
            {
                var view = new UINavigationBar();
                var item = new UINavigationItem { Title = "Library" };
                item.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Add);
                view.SetItems([item], false);
                return view;
            })
            { Width = 160, Height = 61 },

            "toolbar" => new UiKitHost(() =>
            {
                var view = new UIToolbar();
                view.Items =
                [
                    new UIBarButtonItem(UIImage.GetSystemImage("chevron.left")!, UIBarButtonItemStyle.Plain, (_, _) => { }),
                    new UIBarButtonItem(UIImage.GetSystemImage("chevron.right")!, UIBarButtonItemStyle.Plain, (_, _) => { }),
                    new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                    new UIBarButtonItem(UIBarButtonSystemItem.Search),
                ];
                return view;
            })
            { Width = 160, Height = 60 },

            "textfield" => new UiKitHost(() => new UITextField
            {
                BorderStyle = UITextBorderStyle.RoundedRect,
                Placeholder = "Placeholder",
            })
            { Width = 120, Height = 34 },

            "searchfield" => new UiKitHost(() => new UISearchTextField
            {
                Placeholder = "Search",
            })
            { Width = 120, Height = 47 },

            "date" => new UiKitHost(() => new UIDatePicker
            {
                PreferredDatePickerStyle = UIDatePickerStyle.Compact,
                Mode = UIDatePickerMode.Date,
                Locale = new NSLocale("en_US"),
            })
            { Width = 120, Height = 36 },

            "time" => new UiKitHost(() =>
            {
                var view = new UIDatePicker
                {
                    PreferredDatePickerStyle = UIDatePickerStyle.Compact,
                    Mode = UIDatePickerMode.Time,
                    Locale = new NSLocale("en_US"),
                };
                var when = new NSDateComponents { Hour = 9, Minute = 41 };
                var date = NSCalendar.CurrentCalendar.DateFromComponents(when);
                if (date is not null)
                    view.Date = date;
                return view;
            })
            { Width = 90, Height = 36 },

            _ => null,
        };
    }
}
