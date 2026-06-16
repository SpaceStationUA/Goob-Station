using Content.Server._DV.Psionics.Systems;
using Content.Shared._DV.EntityEffects.Effects.Psionics;
using Content.Shared.EntityEffects;
using Content.Shared._DV.Psionics.Components;

namespace Content.Server._DV.EntityEffects.Effects.Psionics;

/// <summary>
/// Rolls for a new psionic power.
/// </summary>
public sealed partial class RollPsionicAbilityEntityEffectSystem : EntitySystem
{
    [Dependency] private readonly PsionicSystem _psionic = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<RollPsionicAbility>>(OnExecute);
    }

    private void OnExecute(ref ExecuteEntityEffectEvent<RollPsionicAbility> args)
    {
        if (!TryComp<PotentialPsionicComponent>(args.Args.TargetEntity, out var potential))
            return;

        _psionic.TryRollPsionic((args.Args.TargetEntity, potential), args.Effect.BonusMultiplier);
    }
}
