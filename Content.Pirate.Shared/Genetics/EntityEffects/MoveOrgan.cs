// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;

using Content.Shared.Body.Part;
using Content.Pirate.Shared.Genetics;
using Content.Shared.Body.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Moves an organ from one body part to another.
/// The parent organ will be changed.
/// The target entity must be the body.
/// </summary>
public sealed partial class MoveOrgan : EntityEffectBase<MoveOrgan>
{
    /// <summary>
    /// The slot of the organ to be moved.
    /// </summary>
    [DataField(required: true)]
    public string Organ = default!;

    /// <summary>
    /// The part to move the organ into.
    /// </summary>
    [DataField(required: true)]
    public string Dest = default!;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-move-organ", ("chance", Probability), ("organ", Organ), ("dest", Dest));
}

public sealed partial class MoveOrganEffectSystem : EntityEffectSystem<BodyComponent, MoveOrgan>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GeneticsBodySystem _body = default!;

    protected override void Effect(Entity<BodyComponent> ent, ref EntityEffectEvent<MoveOrgan> args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _body.MoveOrgan(ent, args.Effect.Organ, args.Effect.Dest);
    }
}
