// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Stunnable;
using Content.Pirate.Server.Traits.LightStep;
using Content.Shared._Pirate.Body.Chips;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Body.Organ;
using Content.Shared.Emp;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Temperature.Components;

namespace Content.Pirate.Server.Body.Chips;

public sealed class HormoneChipSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HormoneChipComponent, OrganChipInsertedEvent>(OnInserted);
        SubscribeLocalEvent<HormoneChipComponent, OrganChipRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<HormoneChipComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<HormoneChipComponent, EmpDisabledRemovedEvent>(OnEmpRecovered);
    }

    private void OnInserted(Entity<HormoneChipComponent> ent, ref OrganChipInsertedEvent args)
    {
        if (args.Body is { } body && !IsDisabled(ent.Owner))
            Apply(ent, body);
    }

    private void OnRemoved(Entity<HormoneChipComponent> ent, ref OrganChipRemovedEvent args)
    {
        if (args.Body is { } body)
            Revert(ent.Owner, body);
    }

    private void OnEmpPulse(Entity<HormoneChipComponent> ent, ref EmpPulseEvent args)
    {
        if (GetHost(ent.Owner) is { } body)
            Revert(ent.Owner, body);
    }

    private void OnEmpRecovered(Entity<HormoneChipComponent> ent, ref EmpDisabledRemovedEvent args)
    {
        if (GetHost(ent.Owner) is { } body)
            Apply(ent, body);
    }

    private bool IsDisabled(EntityUid chip)
        => TryComp<CyberneticsComponent>(chip, out var cybernetics) && cybernetics.Disabled;

    private EntityUid? GetHost(EntityUid chip)
    {
        if (!TryComp<OrganChipComponent>(chip, out var chipComp) || chipComp.Organ is not { } organ)
            return null;

        return CompOrNull<OrganComponent>(organ)?.Body;
    }

    private void Apply(Entity<HormoneChipComponent> chip, EntityUid body)
    {
        if (TerminatingOrDeleted(body))
            return;

        var host = EnsureComp<HormoneChipHostComponent>(body);
        if (host.Applied.ContainsKey(chip.Owner))
            return;

        var payload = chip.Comp;
        host.Applied[chip.Owner] = payload;

        if (payload.CritThresholdModifier != 0)
            ShiftCritThreshold(body, payload.CritThresholdModifier);

        if (!MathHelper.CloseTo(payload.StamcritMultiplier, 1f))
        {
            if (host.StamcritSources++ == 0 && !HasComp<StamcritResistComponent>(body))
            {
                AddComp(body, new StamcritResistComponent { Multiplier = 1f });
                host.OwnsStamcrit = true;
            }

            if (TryComp<StamcritResistComponent>(body, out var stamcrit))
            {
                stamcrit.Multiplier *= payload.StamcritMultiplier;
                Dirty(body, stamcrit);
            }
        }

        if (TryComp<TemperatureDamageComponent>(body, out var temperature))
        {
            temperature.HeatDamageThreshold *= payload.HeatThresholdMultiplier;
            temperature.ColdDamageThreshold *= payload.ColdThresholdMultiplier;
        }

        if (payload.LightStep && host.LightStepSources++ == 0 && !HasComp<LightStepComponent>(body))
        {
            if (TryComp<FootstepModifierComponent>(body, out var footsteps))
                host.PreviousFootsteps = footsteps.FootstepSoundCollection;
            else
                host.OwnsFootstepModifier = true;

            EnsureComp<LightStepComponent>(body);
            EnsureComp<FootstepModifierComponent>(body);
            host.OwnsLightStep = true;
        }
    }

    private void Revert(EntityUid chip, EntityUid body)
    {
        if (TerminatingOrDeleted(body) ||
            !TryComp<HormoneChipHostComponent>(body, out var host) ||
            !host.Applied.Remove(chip, out var payload))
            return;

        if (payload.CritThresholdModifier != 0)
            ShiftCritThreshold(body, -payload.CritThresholdModifier);

        if (!MathHelper.CloseTo(payload.StamcritMultiplier, 1f))
        {
            if (TryComp<StamcritResistComponent>(body, out var stamcrit))
            {
                stamcrit.Multiplier /= payload.StamcritMultiplier;
                Dirty(body, stamcrit);
            }

            if (--host.StamcritSources == 0 && host.OwnsStamcrit)
            {
                RemComp<StamcritResistComponent>(body);
                host.OwnsStamcrit = false;
            }
        }

        if (TryComp<TemperatureDamageComponent>(body, out var temperature))
        {
            temperature.HeatDamageThreshold /= payload.HeatThresholdMultiplier;
            temperature.ColdDamageThreshold /= payload.ColdThresholdMultiplier;
        }

        if (payload.LightStep && --host.LightStepSources == 0 && host.OwnsLightStep)
        {
            RemComp<LightStepComponent>(body);
            if (host.OwnsFootstepModifier)
                RemComp<FootstepModifierComponent>(body);
            else if (TryComp<FootstepModifierComponent>(body, out var footsteps))
                footsteps.FootstepSoundCollection = host.PreviousFootsteps;

            host.OwnsLightStep = false;
            host.OwnsFootstepModifier = false;
            host.PreviousFootsteps = null;
        }

        if (host.Applied.Count == 0)
            RemComp<HormoneChipHostComponent>(body);
    }

    private void ShiftCritThreshold(EntityUid body, int amount)
    {
        if (!TryComp<MobThresholdsComponent>(body, out var thresholds))
            return;

        var crit = _thresholds.GetThresholdForState(body, MobState.Critical, thresholds);
        _thresholds.SetMobStateThreshold(body, crit + amount, MobState.Critical, thresholds);
    }
}
