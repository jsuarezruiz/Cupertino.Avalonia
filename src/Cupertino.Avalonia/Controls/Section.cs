using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;

namespace Cupertino.Controls;

/// <summary>
/// Groups content with an optional header and footer.
/// </summary>
public class Section : HeaderedContentControl
{
    /// <summary>
    /// Identifies the <see cref="Footer"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Section, object?>(nameof(Footer));

    /// <summary>
    /// Identifies the <see cref="FooterTemplate"/> property.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<Section, IDataTemplate?>(nameof(FooterTemplate));

    /// <summary>
    /// Optional content below the section body.
    /// </summary>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>
    /// The data template used to render Footer.
    /// </summary>
    public IDataTemplate? FooterTemplate
    {
        get => GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == FooterProperty)
            UpdatePseudoClasses();
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":has-header", Header is not null);
        PseudoClasses.Set(":has-footer", Footer is not null);
    }
}
