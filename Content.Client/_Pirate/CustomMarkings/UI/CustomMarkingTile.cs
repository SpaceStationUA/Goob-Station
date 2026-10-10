// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>Grid tile for a marking: tinted icon with the name underneath, toggles like a radio or check button.</summary>
public sealed class CustomMarkingTile : ContainerButton
{
    public const string StyleClassTile = "MarkingTile";
    public const float TileWidth = 108f;

    private readonly CustomMarkingPreviewIcon _icon;

    /// <summary>Marking prototype id, null for the "none" tile.</summary>
    public string? MarkingId { get; }

    public CustomMarkingTile(string? markingId, string name, IReadOnlyList<SpriteSpecifier>? sprites, Direction direction)
    {
        MarkingId = markingId;
        AddStyleClass(StyleClassTile);
        ToggleMode = true;
        ToolTip = name;
        MinSize = new Vector2(TileWidth - 4, 96);

        _icon = new CustomMarkingPreviewIcon(sprites, direction);

        var label = new Label
        {
            Text = name,
            ClipText = true,
            Align = Label.AlignMode.Center,
            HorizontalAlignment = HAlignment.Stretch,
            StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorFieldLabel },
        };
        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(4, 4, 4, 2),
            Children = { _icon, label },
        });
    }

    /// <summary>Colour every layer of the icon is drawn in, so tiles preview the currently chosen colour.</summary>
    public Color Tint
    {
        set => _icon.Tint = value;
    }

    /// <summary>Colour per marking layer, for tiles whose marking is already applied.</summary>
    public void SetColors(IReadOnlyList<Color>? colors) => _icon.SetColors(colors);

    /// <summary>Shows the sprites facing the given direction, matching the preview pawn.</summary>
    public void SetDirection(Direction direction) => _icon.SetDirection(direction);
}
