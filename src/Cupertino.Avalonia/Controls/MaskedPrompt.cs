using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace Cupertino.Controls;

/// <summary>
/// Marks an empty masked field so its prompt can use placeholder colour.
/// </summary>
public static class MaskedPrompt
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(MaskedPrompt));

    static MaskedPrompt()
    {
        MaskedTextBox.TextProperty.Changed.AddClassHandler<MaskedTextBox>((m, _) => Update(m));
        IsEnabledProperty.Changed.AddClassHandler<MaskedTextBox>((m, _) => Update(m));
    }

    public static void SetIsEnabled(Control element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(Control element) =>
        element.GetValue(IsEnabledProperty);

    private static void Update(MaskedTextBox m)
    {
        if (!GetIsEnabled(m))
        {
            m.Classes.Set("cupertino-prompt", false);
            return;
        }
        var t = m.Text;
        var empty = string.IsNullOrEmpty(t)
            || t.All(c => c == m.PromptChar || !char.IsLetterOrDigit(c));
        m.Classes.Set("cupertino-prompt", empty);
    }
}
