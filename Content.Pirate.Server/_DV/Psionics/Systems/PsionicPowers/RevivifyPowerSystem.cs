using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events.PowerActionEvents;
using Content.Shared._DV.Psionics.Events.PowerDoAfterEvents;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Server._DV.Psionics.Systems.PsionicPowers;

public sealed class RevivifyPowerSystem : SharedRevivifyPowerSystem
{
    [Dependency] private readonly SharedRottingSystem _rotting = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RevivifyPowerComponent, PsionicHealOtherDoAfterEvent>(OnDoAfter);
    }

    protected override void OnPowerUsed(Entity<RevivifyPowerComponent> psionic, ref RevivifyPowerActionEvent args)
    {
        // Blocked by psionic shielding (e.g. a DarkSwap user in the shadow realm) - don't consume the cooldown.
        if (!Psionic.CanBeTargeted(args.Target, ignorePsionicRequirement: true, hasAggressor: args.Performer))
        {
            args.Handled = false;
            return;
        }

        var ev = new PsionicHealOtherDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, args.Performer, psionic.Comp.UseDelay, ev, psionic, target: args.Target)
        {
            BreakOnMove = true,
        };

        if (!DoAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return;

        psionic.Comp.SaveDoAfterId(doAfterId.Value);
        Dirty(psionic);

        // The caster is forced to speak their words by ForceSpeechSystem.
        AfterPowerUsed(psionic, args.Performer);
    }

    private void OnDoAfter(Entity<RevivifyPowerComponent> psionic, ref PsionicHealOtherDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        psionic.Comp.RemoveSavedDoAfterId();
        Dirty(psionic);

        if (args.Cancelled || args.Target is not { } target)
            return;

        if (psionic.Comp.RotReduction > 0)
            _rotting.ReduceAccumulator(target, TimeSpan.FromSeconds(psionic.Comp.RotReduction));

        if (psionic.Comp.HealingAmount is { } healing && TryComp<DamageableComponent>(target, out var damageable))
            _damageable.TryChangeDamage(target, healing, true, false, damageable, psionic);

        if (!psionic.Comp.DoRevive
            || _rotting.IsRotten(target)
            || !TryComp<MobStateComponent>(target, out var mob)
            || !_mobState.IsDead(target, mob)
            || !_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold)
            || !TryComp<DamageableComponent>(target, out var damageableComp)
            || damageableComp.TotalDamage > threshold.Value)
            return;

        _mobState.ChangeMobState(target, MobState.Critical, mob, psionic);
    }
}
