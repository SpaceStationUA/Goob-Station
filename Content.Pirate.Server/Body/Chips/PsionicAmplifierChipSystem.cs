// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Psionics.PsionicAmplifier;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Systems;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Emp;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Pirate.Server.Body.Chips;

public sealed class PsionicAmplifierChipSystem : EntitySystem
{
    [Dependency] private readonly OrganChipSystem _chips = default!;
    [Dependency] private readonly SharedDispelPowerSystem _dispel = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsionicAmplifierChipComponent, OrganChipInsertedEvent>(OnInserted);
        SubscribeLocalEvent<PsionicAmplifierChipComponent, OrganChipRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<PsionicAmplifierChipComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<PsionicAmplifierChipComponent, EmpDisabledRemovedEvent>(OnEmpRecovered);

        SubscribeLocalEvent<PsionicAmplifiedComponent, DispelledEvent>(OnDispelled);
        SubscribeLocalEvent<PsionicAmplifiedComponent, AttackedEvent>(OnAttacked);
    }

    private void OnInserted(Entity<PsionicAmplifierChipComponent> ent, ref OrganChipInsertedEvent args)
    {
        if (args.Body is { } body && !IsDisabled(ent.Owner))
            Grant(ent, body);
    }

    private void OnRemoved(Entity<PsionicAmplifierChipComponent> ent, ref OrganChipRemovedEvent args)
    {
        if (args.Body is { } body)
            Revoke(ent.Owner, body);
    }

    private void OnEmpPulse(Entity<PsionicAmplifierChipComponent> ent, ref EmpPulseEvent args)
    {
        if (GetHost(ent.Owner) is { } body)
            Revoke(ent.Owner, body);
    }

    private void OnEmpRecovered(Entity<PsionicAmplifierChipComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        if (GetHost(ent.Owner) is { } body)
            Grant(ent, body);
    }

    private void OnDispelled(Entity<PsionicAmplifiedComponent> ent, ref DispelledEvent args)
    {
        if (ent.Comp.DispelDamage is { } damage)
            _dispel.DealDispelDamage(ent.Owner, damage, args.Dispeller);
    }

    private void OnAttacked(Entity<PsionicAmplifiedComponent> ent, ref AttackedEvent args)
    {
        if (HasComp<AntiPsionicWeaponComponent>(args.Used))
            _statusEffects.TryUpdateStatusEffectDuration(ent.Owner, SharedPsionicSystem.PsionicsDisabledProtoId, ent.Comp.OverloadDuration);
    }

    private bool IsDisabled(EntityUid chip)
        => TryComp<CyberneticsComponent>(chip, out var cybernetics) && cybernetics.Disabled;

    private EntityUid? GetHost(EntityUid chip)
    {
        if (!TryComp<OrganChipComponent>(chip, out var chipComp) || chipComp.Organ is not { } organ)
            return null;

        return _chips.GetOrganBody(organ);
    }

    private void Grant(Entity<PsionicAmplifierChipComponent> chip, EntityUid body)
    {
        // Only one chip can own the amplified effect.
        if (TerminatingOrDeleted(body) || HasComp<PsionicAmplifiedComponent>(body))
            return;

        var amplified = new PsionicAmplifiedComponent
        {
            Source = chip.Owner,
            CooldownMultiplier = chip.Comp.CooldownMultiplier,
            DispelDamage = chip.Comp.DispelDamage,
            OverloadDuration = chip.Comp.OverloadDuration,
        };
        AddComp(body, amplified);
        Dirty(body, amplified);
    }

    private void Revoke(EntityUid chip, EntityUid body)
    {
        if (TerminatingOrDeleted(body) ||
            !TryComp<PsionicAmplifiedComponent>(body, out var amplified) ||
            amplified.Source != chip)
            return;

        RemComp<PsionicAmplifiedComponent>(body);
    }
}
