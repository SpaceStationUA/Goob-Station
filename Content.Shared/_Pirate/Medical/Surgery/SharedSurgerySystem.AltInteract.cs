// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.CombatMode;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Shitmed.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private const int PirateSurgeryAltInteractPriority = 10;

    private void InitializePirateAltInteract()
    {
        SubscribeLocalEvent<SurgeryTargetComponent, GetVerbsEvent<AlternativeVerb>>(OnPirateAlternativeVerb);
    }

    private void OnPirateAlternativeVerb(Entity<SurgeryTargetComponent> target,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        if (!args.CanInteract
            || !args.CanAccess
            || args.Using is not { } tool
            || !TryComp(tool, out SurgeryToolComponent? surgeryTool)
            || !CanPirateQuickOpenSurgery(user))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => AttemptStartSurgery((tool, surgeryTool), user, target),
            Icon = new SpriteSpecifier.Texture(new("/Textures/_Shitmed/Interface/Examine/scalpel.png")),
            Text = Loc.GetString("surgery-verb-text"),
            Message = Loc.GetString("surgery-verb-message"),
            Priority = PirateSurgeryAltInteractPriority,
            DoContactInteraction = true,
        });
    }

    private bool CanPirateQuickOpenSurgery(EntityUid user)
    {
        return !TryComp(user, out CombatModeComponent? combatMode) || !combatMode.IsInCombatMode;
    }
}
