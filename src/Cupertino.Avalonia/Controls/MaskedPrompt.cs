using Avalonia;
using Avalonia.Controls;

namespace Cupertino.Controls;

/// <summary>
/// Marks an empty masked field so its prompt can use placeholder colour.
/// </summary>
public static class MaskedPrompt
{
    /// <summary>
    /// Identifies the <see cref="GetIsEnabled"/> attached setting.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(MaskedPrompt));

    static MaskedPrompt()
    {
        MaskedTextBox.TextProperty.Changed.AddClassHandler<MaskedTextBox>((m, _) => Update(m));
        IsEnabledProperty.Changed.AddClassHandler<MaskedTextBox>((m, _) => Update(m));
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static void SetIsEnabled(Control element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    /// <inheritdoc cref="IsEnabledProperty"/>
    public static bool GetIsEnabled(Control element) =>
        element.GetValue(IsEnabledProperty);

    private static void Update(MaskedTextBox m)
    {
        if (!GetIsEnabled(m))
        {
            m.Classes.Set("cupertino-prompt", false);
            return;
        }
        var text = m.Text;
        var empty = string.IsNullOrEmpty(text) || IsMaskOnly(text, m.PromptChar);
        m.Classes.Set("cupertino-prompt", empty);
    }

    // A masked field reads as empty while it only holds prompt characters and separators.
    private static bool IsMaskOnly(string text, char promptChar)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character != promptChar && char.IsLetterOrDigit(character))
                return false;
        }
        return true;
    }
}
