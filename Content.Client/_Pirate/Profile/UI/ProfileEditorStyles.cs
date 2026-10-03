using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.Profile.UI;

/// <summary>
/// Shared palette for the profile editor's custom pages
/// </summary>
internal static class ProfileEditorStyles
{
    public static readonly Color CategoryBackground = Color.FromHex("#2a2a35");
    public static readonly Color ContentBackground = Color.FromHex("#22222a");
    public static readonly Color EntryBackground = Color.FromHex("#2a2a35");
    public static readonly Color Border = Color.FromHex("#32323e");
    public static readonly Color TextPrimary = Color.FromHex("#E0E0E0");
    public static readonly Color TextSecondary = Color.FromHex("#A0A0A0");
    public static readonly Color Accent = Color.FromHex("#60a5fa");

    public static StyleBoxFlat CreateCategoryHeader()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = CategoryBackground,
            BorderColor = Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    public static StyleBoxFlat CreateCategoryContent()
    {
        return new StyleBoxFlat { BackgroundColor = ContentBackground };
    }

    public static StyleBoxFlat CreateEntryPanel()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = EntryBackground,
            BorderColor = Border,
            BorderThickness = new Thickness(1),
        };
    }
}
