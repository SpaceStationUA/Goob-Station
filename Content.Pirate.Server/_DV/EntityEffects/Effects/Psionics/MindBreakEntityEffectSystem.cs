using Content.Server._DV.Psionics.Systems;
using Content.Shared._DV.EntityEffects.Effects.Psionics;
using Content.Shared.EntityEffects;

namespace Content.Server._DV.EntityEffects.Effects.Psionics;

/// <summary>
///     Permanently mindbreaks the target (Soulbreaker Toxin).
/// </summary>
public sealed partial class MindBreakEntityEffectSystem : EntitySystem
{
    [Dependency] private readonly PsionicSystem _psionicSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<MindBreak>>(OnExecute);
    }

    private void OnExecute(ref ExecuteEntityEffectEvent<MindBreak> args)
    {
        _psionicSystem.MakeMindBroken(args.Args.TargetEntity);
    }
}
