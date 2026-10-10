// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Examine;

namespace Content.Pirate.Shared.Psionics.PsionicAmplifier;

public sealed class PsionicAmplifierSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsionicActionComponent, ActionPerformedEvent>(OnActionPerformed);
        SubscribeLocalEvent<PsionicAmplifierChipComponent, ExaminedEvent>(OnChipExamined);
    }

    private void OnActionPerformed(Entity<PsionicActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (!TryComp<PsionicAmplifiedComponent>(args.Performer, out var amplified) ||
            !TryComp<ActionComponent>(ent, out var action) ||
            action.Cooldown is not { } cooldown)
            return;

        // ActionPerformed fires after cooldown setup, so scale the active cooldown here.
        var length = (cooldown.End - cooldown.Start) * amplified.CooldownMultiplier;
        _actions.SetCooldown((ent.Owner, action), cooldown.Start, cooldown.Start + length);
    }

    private void OnChipExamined(Entity<PsionicAmplifierChipComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(PsionicAmplifierChipComponent)))
        {
            args.PushMarkup(Loc.GetString("psionic-amplifier-chip-examine"));
            args.PushMarkup(Loc.GetString("psionic-amplifier-chip-examine-cooldown",
                ("percent", (int) MathF.Round((1f - ent.Comp.CooldownMultiplier) * 100f))));
            args.PushMarkup(Loc.GetString("psionic-amplifier-chip-examine-dispel"));
            args.PushMarkup(Loc.GetString("psionic-amplifier-chip-examine-overload",
                ("seconds", (int) ent.Comp.OverloadDuration.TotalSeconds)));
        }
    }
}
