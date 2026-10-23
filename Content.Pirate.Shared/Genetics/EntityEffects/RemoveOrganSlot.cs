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
/// Removes an organ slot from the target entity, which must be a body part.
/// </summary>
public sealed partial class RemoveOrganSlot : EntityEffectBase<RemoveOrganSlot>
{
    [DataField(required: true)]
    public string Slot = default!;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-part-remove-slot", ("chance", Probability), ("slot", Slot));
}

public sealed partial class RemoveOrganSlotEffectSystem : EntityEffectSystem<BodyPartComponent, RemoveOrganSlot>
{
    [Dependency] private GeneticsBodySystem _part = default!;

    protected override void Effect(Entity<BodyPartComponent> ent, ref EntityEffectEvent<RemoveOrganSlot> args)
    {
        _part.TryRemoveSlot(ent.Owner, args.Effect.Slot);
    }
}
