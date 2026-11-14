using Content.Server._DV.Psionics.Systems;
using Content.Shared._DV.EntityEffects.Effects.Psionics;
using Content.Shared.EntityEffects;
using Content.Shared.Shadowkin;

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
        // Pirate: shadowkin are naturally psionic, so chemically stripping their psionics
        // doesn't just remove the powers - it breaks their mind for good.
        if (HasComp<ShadowkinComponent>(args.Args.TargetEntity))
            _psionicSystem.MakeMindBroken(args.Args.TargetEntity);
        else
            _psionicSystem.MindBreakEntity(args.Args.TargetEntity);
    }
}
