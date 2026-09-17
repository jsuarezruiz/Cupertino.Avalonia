using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Cupertino.Controls;

/// <summary>
/// Attached properties for bottom and segmented tab layouts.
/// </summary>
public static class Tabs
{
    /// <summary>
    /// Identifies the <see cref="GetAccessory"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<object?> AccessoryProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>("Accessory", typeof(Tabs));

    /// <summary>
    /// Identifies the <see cref="GetIsDetached"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsDetachedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsDetached", typeof(Tabs));

    private static readonly AttachedProperty<DetachedState?> DetachedStateProperty =
        AvaloniaProperty.RegisterAttached<Control, DetachedState?>("DetachedState", typeof(Tabs));

    private static readonly AttachedProperty<DetachedState?> OwnerDetachedStateProperty =
        AvaloniaProperty.RegisterAttached<Control, DetachedState?>("OwnerDetachedState", typeof(Tabs));

    /// <summary>
    /// Identifies the <see cref="GetBadgeValue"/> attached setting; negative shows a dot and null hides it.
    /// </summary>
    public static readonly AttachedProperty<int?> BadgeValueProperty =
        AvaloniaProperty.RegisterAttached<Control, int?>("BadgeValue", typeof(Tabs));

    static Tabs()
    {
        AccessoryProperty.Changed.AddClassHandler<Control>((tabs, e) =>
        {
            var state = tabs.GetValue(OwnerDetachedStateProperty);
            if (state is not null && !state.Owns(e.NewValue))
                state.Dispose();

            tabs.Classes.Set("cupertino-has-accessory", e.NewValue is not null);
            if (e.NewValue is null)
                AttachFirstDetachedItem(tabs);
        });

        IsDetachedProperty.Changed.AddClassHandler<Control>((item, e) =>
        {
            item.AttachedToLogicalTree -= OnDetachedItemAttached;
            item.GetValue(DetachedStateProperty)?.Dispose();
            if (e.GetNewValue<bool>())
            {
                item.AttachedToLogicalTree += OnDetachedItemAttached;
                AttachDetachedItem(item);
            }
        });
    }

    /// <inheritdoc cref="AccessoryProperty"/>
    public static void SetAccessory(Control element, object? value) =>
        element.SetValue(AccessoryProperty, value);

    /// <inheritdoc cref="AccessoryProperty"/>
    public static object? GetAccessory(Control element) =>
        element.GetValue(AccessoryProperty);

    /// <inheritdoc cref="IsDetachedProperty"/>
    public static void SetIsDetached(Control element, bool value) =>
        element.SetValue(IsDetachedProperty, value);

    /// <inheritdoc cref="IsDetachedProperty"/>
    public static bool GetIsDetached(Control element) =>
        element.GetValue(IsDetachedProperty);

    /// <inheritdoc cref="BadgeValueProperty"/>
    public static void SetBadgeValue(Control element, int? value) =>
        element.SetValue(BadgeValueProperty, value);

    /// <inheritdoc cref="BadgeValueProperty"/>
    public static int? GetBadgeValue(Control element) =>
        element.GetValue(BadgeValueProperty);

    private static void OnDetachedItemAttached(object? sender, LogicalTreeAttachmentEventArgs e)
    {
        var item = (Control)sender!;
        AttachDetachedItem(item);
    }

    private static void AttachDetachedItem(Control item)
    {
        if (!GetIsDetached(item) || item.GetValue(DetachedStateProperty) is not null)
            return;
        if (item.FindLogicalAncestorOfType<SelectingItemsControl>() is not { } owner
            || GetAccessory(owner) is not null)
            return;

        object? glyph = item switch
        {
            HeaderedContentControl h => h.Header,
            ContentControl c => c.Content,
            _ => null,
        };
        if (glyph is null)
            return;

        switch (item)
        {
            case HeaderedContentControl h: h.Header = null; break;
            case ContentControl c when item is not HeaderedContentControl: c.Content = null; break;
        }

        item.IsVisible = false;

        var button = new Button { Content = glyph };
        if (owner.TryFindResource("CupertinoAccessoryButton", out var theme) && theme is ControlTheme ct)
            button.Theme = ct;

        var state = new DetachedState(item, owner, button, glyph);
        item.SetValue(DetachedStateProperty, state);
        owner.SetValue(OwnerDetachedStateProperty, state);
        SetAccessory(owner, button);
    }

    private static void AttachFirstDetachedItem(Control owner)
    {
        foreach (var item in owner.GetLogicalDescendants().OfType<Control>())
        {
            if (!GetIsDetached(item))
                continue;
            AttachDetachedItem(item);
            if (GetAccessory(owner) is not null)
                return;
        }
    }

    private sealed class DetachedState : IDisposable
    {
        private readonly Control _item;
        private readonly SelectingItemsControl _owner;
        private readonly Button _button;
        private readonly object _glyph;
        private object? _lastOther;
        private bool _disposed;

        public DetachedState(
            Control item, SelectingItemsControl owner, Button button, object glyph)
        {
            _item = item;
            _owner = owner;
            _button = button;
            _glyph = glyph;

            _owner.SelectionChanged += OnSelectionChanged;
            _button.Click += OnClick;
            _item.DetachedFromLogicalTree += OnItemDetached;
            SyncSelection();
        }

        private void SyncSelection()
        {
            var active = ReferenceEquals(_owner.SelectedItem, _item);
            if (!active)
                _lastOther = _owner.SelectedItem;
            var key = active ? "CupertinoTabAccentBrush" : "CupertinoLabelBrush";
            // Use variant-aware lookup for ThemeDictionaries.
            if (_owner.TryFindResource(key, _owner.ActualThemeVariant, out var brush)
                && brush is Avalonia.Media.IBrush b)
                _button.Foreground = b;
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => SyncSelection();

        private void OnClick(object? sender, RoutedEventArgs e) =>
            _owner.SelectedItem = ReferenceEquals(_owner.SelectedItem, _item) && _lastOther is not null
                ? _lastOther
                : _item;

        private void OnItemDetached(object? sender, LogicalTreeAttachmentEventArgs e) => Dispose();

        public bool Owns(object? accessory) => ReferenceEquals(accessory, _button);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _owner.SelectionChanged -= OnSelectionChanged;
            _button.Click -= OnClick;
            _item.DetachedFromLogicalTree -= OnItemDetached;
            if (ReferenceEquals(_owner.GetValue(OwnerDetachedStateProperty), this))
                _owner.SetValue(OwnerDetachedStateProperty, null);
            if (ReferenceEquals(GetAccessory(_owner), _button))
                SetAccessory(_owner, null);
            // Clear the accessory before restoring its visual header.
            _button.Content = null;
            _item.IsVisible = true;
            switch (_item)
            {
                case HeaderedContentControl headered when headered.Header is null:
                    headered.Header = _glyph;
                    break;
                case ContentControl content when _item is not HeaderedContentControl && content.Content is null:
                    content.Content = _glyph;
                    break;
            }
            if (ReferenceEquals(_item.GetValue(DetachedStateProperty), this))
                _item.SetValue(DetachedStateProperty, null);
        }
    }
}
