using Content.Server._DV.Psionics.Systems;
using Content.Shared._DV.EntityEffects.Effects.Psionics;
using Content.Shared.EntityEffects;

namespace Content.Server._DV.EntityEffects.Effects.Psionics;

/// <summary>
/// Removes psionic abilities.
/// </summary>
public sealed partial class RemovePsionicAbilitiesEntityEffectSystem : EntitySystem
{
    [Dependency] private readonly PsionicSystem _psionicSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<RemovePsionicAbilities>>(OnExecute);
    }

    private void OnExecute(ref ExecuteEntityEffectEvent<RemovePsionicAbilities> args)
    {
        _psionicSystem.MindBreakEntity(args.Args.TargetEntity);
    }
}
