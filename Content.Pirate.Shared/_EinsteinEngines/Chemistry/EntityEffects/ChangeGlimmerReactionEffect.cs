using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.ReactionEffects;

/// <summary>
/// Reaction effect that changes station glimmer.
/// Implemented as an <see cref="EventEntityEffect{T}"/> so it can be instantiated client-side,
/// while still executing server-side.
/// </summary>
[UsedImplicitly]
public sealed partial class ChangeGlimmerReactionEffect : EventEntityEffect<ChangeGlimmerReactionEffect>
{
    /// <summary>
    /// Amount added to glimmer when the reaction occurs.
    /// </summary>
    [DataField]
    public float Count = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-change-glimmer-reaction-effect", ("chance", Probability),
            ("count", Count));
}

