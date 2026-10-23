// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameStates;

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Pirate.Shared.Genetics;
using Content.Shared.EntityEffects;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Detaches this target organ/part from its parent part.
/// </summary>
public sealed partial class DetachOrgan : EntityEffectBase<DetachOrgan>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-detach-part", ("chance", Probability));
}

public sealed partial class DetachOrganEffectSystem : EntityEffectSystem<MetaDataComponent, DetachOrgan>
{
    [Dependency] private GeneticsBodySystem _part = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<DetachOrgan> args)
    {
        _part.Detach(ent);
    }
}
