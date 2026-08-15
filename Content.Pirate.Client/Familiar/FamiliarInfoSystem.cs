// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.CharacterInfo;
using Content.Pirate.Shared.Familiar;
using Robust.Client.UserInterface.Controls;

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

        master = FormattedMessage.EscapeText(master);
        args.Controls.Add(new RichTextLabel
        {
            Text = $"[bold]{master}[/bold] is your master, serve them faithfully!",
            Margin = new Thickness(8, 4)
        });
    }
}
