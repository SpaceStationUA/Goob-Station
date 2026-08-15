// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.CharacterInfo;
using Content.Pirate.Shared.Familiar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Familiar;

/// <summary>
/// Displays a familiar's master in the character menu.
/// </summary>
public sealed class FamiliarInfoSystem : EntitySystem
{
    [Dependency] private readonly FamiliarSystem _familiar = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CharacterInfoSystem.GetCharacterInfoControlsEvent>(OnGetCharacterInfoControls);
    }

    private void OnGetCharacterInfoControls(ref CharacterInfoSystem.GetCharacterInfoControlsEvent args)
    {
        if (_familiar.GetMasterName(args.Entity) is not { } master)
            return;

        args.Controls.Add(new RichTextLabel
        {
            Text = Loc.GetString("familiar-master-info",
                ("master", FormattedMessage.EscapeText(master))),
            Margin = new Thickness(8, 4)
        });
    }
}
