using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class MassSleepPowerSystem : SharedMassSleepPowerSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    public static readonly EntProtoId MassSleepStatusEffect = "MassSleepForcedSleepStatusEffect";

    protected override void OnPowerUsed(Entity<MassSleepPowerComponent> psionic, ref MassSleepPowerActionEvent args)
    {
        // Instant AoE at the cursor point, like Metapsionic Pulse.
        foreach (var target in _lookup.GetEntitiesInRange(args.Target, psionic.Comp.Radius))
        {
            // Psionically shielded entities (e.g. a DarkSwap user in the shadow realm) are unaffected.
            if (!Psionic.CanBeTargeted(target, ignorePsionicRequirement: true))
                continue;

            _statusEffects.TryUpdateStatusEffectDuration(target, MassSleepStatusEffect, psionic.Comp.Duration);
            Popup.PopupEntity(Loc.GetString("psionic-power-mass-sleep-warning"), target, target, PopupType.LargeCaution);
        }

        AfterPowerUsed(psionic, args.Performer);
    }
}
