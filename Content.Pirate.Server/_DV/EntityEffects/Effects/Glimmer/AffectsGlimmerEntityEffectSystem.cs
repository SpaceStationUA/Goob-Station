using Content.Shared.EntityEffects;
using Content.Shared.Psionics.Glimmer;
using Content.Shared._DV.EntityEffects.Effects.Glimmer;

namespace Content.Server._DV.EntityEffects.Effects.Glimmer;

/// <summary>
///     Changes glimmer when reaction happens.
/// </summary>
public sealed partial class AffectsGlimmerEntityEffectSystem : EntitySystem
{
    [Dependency] private readonly GlimmerSystem _glimmer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExecuteEntityEffectEvent<AffectsGlimmer>>(OnExecute);
    }

    private void OnExecute(ref ExecuteEntityEffectEvent<AffectsGlimmer> args)
    {
        var reason = args.Args is EntityEffectReagentArgs reagentArgs && reagentArgs.Reagent != null
            ? $"reagent {reagentArgs.Reagent.ID}"
            : "entity effect";

        _glimmer.AdjustGlimmer(args.Effect.Amount, reason, args.Args.User ?? args.Args.TargetEntity);
    }
}
