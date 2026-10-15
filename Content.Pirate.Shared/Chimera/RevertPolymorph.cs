// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Chimera;

/// <summary>
/// Reverts the polymorph of an entity treated with a Chimera cure.
/// </summary>
public sealed partial class RevertPolymorph : EntityEffectBase<RevertPolymorph>
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Prototype;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var polymorph = prototype.Index(Prototype);
        var entName = polymorph.Configuration.Entity is { } entity
            ? prototype.Index(entity).Name
            : Loc.GetString("generic-unknown");

        return Loc.GetString("reagent-effect-guidebook-revert-polymorph", ("chance", Probability), ("entityname", entName));
    }
}

public abstract partial class SharedRevertPolymorphEntityEffectSystem : EntityEffectSystem<MetaDataComponent, RevertPolymorph>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<RevertPolymorph> args)
    {
        RevertPolymorph(entity.Owner);
    }

    protected virtual void RevertPolymorph(EntityUid uid)
    {
    }
}
