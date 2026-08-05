using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Cupertino.Controls;

public enum CupertinoListAccessory
{
    None,
    Disclosure,
    Checkmark,
}

/// <summary>
/// Displays a list row with labels and accessories.
/// </summary>
[PseudoClasses(":subtitle", ":detail", ":customaccessory", ":disclosure", ":checkmark", ":noseparator", ":destructive")]
public class CupertinoListCell : Button
{
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<CupertinoListCell, object?>(nameof(Leading));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CupertinoListCell, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<CupertinoListCell, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<CupertinoListCell, string?>(nameof(Detail));

    public static readonly StyledProperty<object?> AccessoryProperty =
        AvaloniaProperty.Register<CupertinoListCell, object?>(nameof(Accessory));

    public static readonly StyledProperty<CupertinoListAccessory> AccessoryKindProperty =
        AvaloniaProperty.Register<CupertinoListCell, CupertinoListAccessory>(nameof(AccessoryKind));

    public static readonly StyledProperty<bool> ShowsSeparatorProperty =
        AvaloniaProperty.Register<CupertinoListCell, bool>(nameof(ShowsSeparator), true);

    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<CupertinoListCell, bool>(nameof(IsDestructive));

    public object? Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public object? Accessory { get => GetValue(AccessoryProperty); set => SetValue(AccessoryProperty, value); }
    public CupertinoListAccessory AccessoryKind { get => GetValue(AccessoryKindProperty); set => SetValue(AccessoryKindProperty, value); }
    public bool ShowsSeparator { get => GetValue(ShowsSeparatorProperty); set => SetValue(ShowsSeparatorProperty, value); }
    public bool IsDestructive { get => GetValue(IsDestructiveProperty); set => SetValue(IsDestructiveProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SubtitleProperty)
            PseudoClasses.Set(":subtitle", !string.IsNullOrWhiteSpace(Subtitle));
        else if (change.Property == DetailProperty)
            PseudoClasses.Set(":detail", !string.IsNullOrWhiteSpace(Detail));
        else if (change.Property == AccessoryProperty)
            PseudoClasses.Set(":customaccessory", Accessory is not null);
        else if (change.Property == AccessoryKindProperty)
        {
            PseudoClasses.Set(":disclosure", AccessoryKind == CupertinoListAccessory.Disclosure);
            PseudoClasses.Set(":checkmark", AccessoryKind == CupertinoListAccessory.Checkmark);
        }
        else if (change.Property == ShowsSeparatorProperty)
            PseudoClasses.Set(":noseparator", !ShowsSeparator);
        else if (change.Property == IsDestructiveProperty)
            PseudoClasses.Set(":destructive", IsDestructive);

        if (change.Property == TitleProperty && string.IsNullOrWhiteSpace(AutomationProperties.GetName(this)))
            AutomationProperties.SetName(this, Title);
    }
}

/// <summary>
/// A labelled form row that keeps help and validation text aligned with its editor.
/// </summary>
[PseudoClasses(":help", ":error", ":required")]
public class CupertinoFormRow : ContentControl
{
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<CupertinoFormRow, object?>(nameof(Label));

    public static readonly StyledProperty<string?> HelpTextProperty =
        AvaloniaProperty.Register<CupertinoFormRow, string?>(nameof(HelpText));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<CupertinoFormRow, string?>(nameof(ErrorText));

    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<CupertinoFormRow, bool>(nameof(IsRequired));

    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? HelpText { get => GetValue(HelpTextProperty); set => SetValue(HelpTextProperty, value); }
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HelpTextProperty)
            PseudoClasses.Set(":help", !string.IsNullOrWhiteSpace(HelpText));
        else if (change.Property == ErrorTextProperty)
            PseudoClasses.Set(":error", !string.IsNullOrWhiteSpace(ErrorText));
        else if (change.Property == IsRequiredProperty)
            PseudoClasses.Set(":required", IsRequired);
    }
}
