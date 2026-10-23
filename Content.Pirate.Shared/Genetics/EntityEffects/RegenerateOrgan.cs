// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;

using Content.Pirate.Shared.Genetics;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.EntityEffects;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Spawns and attaches an organ from the body's initial organs, to this body part entity.
/// </summary>
public sealed partial class RegenerateOrgan : EntityEffectBase<RegenerateOrgan>
{
    /// <summary>
    /// The part slot to regenerate.
    /// It must exist on this part and in the initial body organs.
    /// </summary>
    [DataField(required: true)]
    public string Slot = default!;

    /// <summary>
    /// Whether to also regenerate child organs.
    /// </summary>
    [DataField]
    public bool Recursive = true;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-regenerate-part", ("chance", Probability), ("slot", Slot));
}

public sealed partial class RegenerateOrganEffectSystem : EntityEffectSystem<BodyPartComponent, RegenerateOrgan>
{
    [Dependency] private GeneticsBodySystem _part = default!;

    protected override void Effect(Entity<BodyPartComponent> ent, ref EntityEffectEvent<RegenerateOrgan> args)
    {
        var e = args.Effect;
        _part.RestoreInitialChild(ent.Owner, e.Slot, recursive: e.Recursive);
    }
}
