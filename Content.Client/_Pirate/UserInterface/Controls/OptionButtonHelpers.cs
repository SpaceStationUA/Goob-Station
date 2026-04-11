using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.UserInterface.Controls;

/// <summary>
/// Shared helpers for safe <see cref="OptionButton"/> placeholder handling.
/// </summary>
public static class OptionButtonHelpers
{
    private const int PlaceholderId = int.MinValue;

    /// <summary>
    /// Adds or updates a disabled placeholder entry and selects it so the button label can
    /// represent an unavailable value without leaving <see cref="OptionButton.SelectedId"/>
    /// pointing at a removed item.
    /// If a caller ever must remove the placeholder instead, it must immediately select a
    /// surviving item via <see cref="OptionButton.SelectId"/> or <see cref="OptionButton.TrySelectId"/>.
    /// </summary>
    public static void SetPlaceholder(OptionButton button, string placeholder)
    {
        if (!button.TrySelectId(PlaceholderId))
        {
            button.AddItem(placeholder, PlaceholderId);
        }

        var placeholderIndex = button.GetIdx(PlaceholderId);
        button.SetItemText(placeholderIndex, placeholder);
        button.SetItemDisabled(placeholderIndex, true);
        button.SelectId(PlaceholderId);
    }
}
