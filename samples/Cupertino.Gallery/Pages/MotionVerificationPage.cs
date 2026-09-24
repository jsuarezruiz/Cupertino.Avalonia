using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Cupertino.Controls;

namespace Cupertino.Gallery.Pages;

/// <summary>
/// Opt-in capture surface used to compare native UIKit and Avalonia motion at
/// identical coordinates. It is only added to the catalogue when
/// GALLERY_MOTION_CAPTURE=1.
/// </summary>
internal sealed class MotionVerificationPage : UserControl, IGalleryCaptureState
{
    private readonly MenuItem? _menu;
    private readonly CupertinoDatePicker? _datePicker;

    internal MotionVerificationPage()
    {
        var target = Environment.GetEnvironmentVariable("GALLERY_MOTION_TARGET") ?? "menu";
        var renderer = Environment.GetEnvironmentVariable("GALLERY_MOTION_RENDERER") ?? "avalonia";
        var native = string.Equals(renderer, "native", StringComparison.OrdinalIgnoreCase);

        var stage = new Grid
        {
            RowDefinitions = new RowDefinitions("230,Auto,*"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        Control anchor;
        if (native)
        {
            var host = new ContentControl
            {
                Width = 140,
                Height = 44,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            AttachedToVisualTree += (_, _) => DispatcherTimer.RunOnce(() =>
            {
                host.Content = NativeComparison.CreateNativeControl?.Invoke($"motion-{target}")
                    ?? new TextBlock { Text = "Native target unavailable" };
            }, TimeSpan.FromMilliseconds(600));
            anchor = host;
        }
        else if (string.Equals(target, "date", StringComparison.OrdinalIgnoreCase))
        {
            _datePicker = new CupertinoDatePicker
            {
                Width = 140,
                DateFormat = "MMM d, yyyy",
                SelectedDate = new DateTimeOffset(2026, 9, 10, 10, 10, 0, TimeSpan.Zero),
            };
            anchor = _datePicker;
        }
        else
        {
            _menu = new MenuItem
            {
                Header = "File",
                Width = 72,
            };
            _menu.Items.Add(new MenuItem { Header = "New" });
            _menu.Items.Add(new MenuItem { Header = "Open…" });
            _menu.Items.Add(new Separator());
            _menu.Items.Add(new MenuItem { Header = "Close" });
            anchor = new Menu { Items = { _menu } };
        }

        anchor.Name = "MotionTarget";
        anchor.HorizontalAlignment = HorizontalAlignment.Center;
        anchor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(anchor, 1);
        stage.Children.Add(anchor);
        Content = stage;

        if (!native
            && string.Equals(Environment.GetEnvironmentVariable("GALLERY_MOTION_SEQUENCE"), "1",
                StringComparison.Ordinal))
        {
            AttachedToVisualTree += (_, _) =>
            {
                DispatcherTimer.RunOnce(OpenAvaloniaTarget, TimeSpan.FromMilliseconds(1_000));
                DispatcherTimer.RunOnce(CloseAvaloniaTarget, TimeSpan.FromMilliseconds(2_200));
            };
        }
    }

    public void ApplyGalleryCaptureState()
        => OpenAvaloniaTarget();

    private void OpenAvaloniaTarget()
    {
        if (_menu is not null)
            _menu.IsSubMenuOpen = true;
        if (_datePicker is not null)
            _datePicker.IsDropDownOpen = true;
    }

    private void CloseAvaloniaTarget()
    {
        if (_menu is not null)
            _menu.IsSubMenuOpen = false;
        if (_datePicker is not null)
            _datePicker.IsDropDownOpen = false;
    }
}
