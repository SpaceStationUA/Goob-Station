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
/// Adds a organ/bodypart slot to the target entity, which must be a body part.
/// </summary>
public sealed partial class AddOrganSlot : EntityEffectBase<AddOrganSlot>
{
    [DataField(required: true)]
    public string Category = default!;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-part-add-slot", ("chance", Probability), ("slot", Category));
}

public sealed partial class AddOrganSlotEffectSystem : EntityEffectSystem<BodyPartComponent, AddOrganSlot>
{
    [Dependency] private GeneticsBodySystem _part = default!;

    protected override void Effect(Entity<BodyPartComponent> ent, ref EntityEffectEvent<AddOrganSlot> args)
    {
        _part.TryAddSlot(ent.Owner, args.Effect.Category);
    }
}
