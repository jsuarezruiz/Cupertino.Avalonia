using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Cupertino.Controls;

/// <summary>
/// Built-in trailing indicators for a list cell.
/// </summary>
public enum CupertinoListAccessory
{
    /// <summary>
    /// No built-in accessory.
    /// </summary>
    None,
    /// <summary>
    /// A chevron indicating navigation.
    /// </summary>
    Disclosure,
    /// <summary>
    /// A checkmark indicating selection.
    /// </summary>
    Checkmark,
}

/// <summary>
/// Displays a list row with labels and accessories.
/// </summary>
[PseudoClasses(":subtitle", ":detail", ":customaccessory", ":disclosure", ":checkmark", ":noseparator", ":destructive")]
public class CupertinoListCell : Button
{
    /// <summary>
    /// Identifies the <see cref="Leading"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<CupertinoListCell, object?>(nameof(Leading));

    /// <summary>
    /// Identifies the <see cref="Title"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CupertinoListCell, string?>(nameof(Title));

    /// <summary>
    /// Identifies the <see cref="Subtitle"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<CupertinoListCell, string?>(nameof(Subtitle));

    /// <summary>
    /// Identifies the <see cref="Detail"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<CupertinoListCell, string?>(nameof(Detail));

    /// <summary>
    /// Identifies the <see cref="Accessory"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> AccessoryProperty =
        AvaloniaProperty.Register<CupertinoListCell, object?>(nameof(Accessory));

    /// <summary>
    /// Identifies the <see cref="AccessoryKind"/> property.
    /// </summary>
    public static readonly StyledProperty<CupertinoListAccessory> AccessoryKindProperty =
        AvaloniaProperty.Register<CupertinoListCell, CupertinoListAccessory>(nameof(AccessoryKind));

    /// <summary>
    /// Identifies the <see cref="ShowsSeparator"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowsSeparatorProperty =
        AvaloniaProperty.Register<CupertinoListCell, bool>(nameof(ShowsSeparator), true);

    /// <summary>
    /// Identifies the <see cref="IsDestructive"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<CupertinoListCell, bool>(nameof(IsDestructive));

    /// <summary>
    /// Optional content before the row labels, such as an icon or avatar.
    /// </summary>
    public object? Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    /// <summary>
    /// The primary row label. Also supplies the accessible name unless the application explicitly overrides it.
    /// </summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>
    /// Optional secondary text below the title; null or whitespace hides it.
    /// </summary>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    /// <summary>
    /// Optional trailing detail text; null or whitespace hides it.
    /// </summary>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    /// <summary>
    /// Custom trailing accessory content.
    /// </summary>
    public object? Accessory { get => GetValue(AccessoryProperty); set => SetValue(AccessoryProperty, value); }
    /// <summary>
    /// The built-in trailing accessory: none, disclosure, or checkmark.
    /// </summary>
    public CupertinoListAccessory AccessoryKind { get => GetValue(AccessoryKindProperty); set => SetValue(AccessoryKindProperty, value); }
    /// <summary>
    /// Whether to draw the row separator.
    /// </summary>
    public bool ShowsSeparator { get => GetValue(ShowsSeparatorProperty); set => SetValue(ShowsSeparatorProperty, value); }
    /// <summary>
    /// Whether to use the destructive-action styling for the row.
    /// </summary>
    public bool IsDestructive { get => GetValue(IsDestructiveProperty); set => SetValue(IsDestructiveProperty, value); }

    /// <summary>
    /// Creates a CupertinoListCell with its default settings.
    /// </summary>
    public CupertinoListCell() => Bind(AutomationProperties.NameProperty,
        this.GetObservable(TitleProperty), Avalonia.Data.BindingPriority.Style);

    /// <inheritdoc/>
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

    }
}

/// <summary>
/// A labelled form row that keeps help and validation text aligned with its editor.
/// </summary>
[PseudoClasses(":help", ":error", ":required")]
public class CupertinoFormRow : ContentControl
{
    /// <summary>
    /// Identifies the <see cref="Label"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> LabelProperty =
        AvaloniaProperty.Register<CupertinoFormRow, object?>(nameof(Label));

    /// <summary>
    /// Identifies the <see cref="HelpText"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> HelpTextProperty =
        AvaloniaProperty.Register<CupertinoFormRow, string?>(nameof(HelpText));

    /// <summary>
    /// Identifies the <see cref="ErrorText"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<CupertinoFormRow, string?>(nameof(ErrorText));

    /// <summary>
    /// Identifies the <see cref="IsRequired"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<CupertinoFormRow, bool>(nameof(IsRequired));

    /// <summary>
    /// The label identifying the row editor.
    /// </summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    /// <summary>
    /// Optional help text; null or whitespace hides the help region.
    /// </summary>
    public string? HelpText { get => GetValue(HelpTextProperty); set => SetValue(HelpTextProperty, value); }
    /// <summary>
    /// Optional validation message; null or whitespace clears the error presentation.
    /// </summary>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    /// <summary>
    /// Whether to display the required-field indication; this does not perform validation.
    /// </summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <inheritdoc/>
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
