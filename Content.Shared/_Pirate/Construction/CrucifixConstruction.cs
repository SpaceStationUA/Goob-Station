// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Buckle.Components;
using Content.Shared.Construction;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;

namespace Content.Shared._Pirate.Construction;

/// <summary>
/// Requires at least one entity to be strapped to the construction entity.
/// </summary>
public sealed partial class HasStrappedEntity : IGraphCondition
{
    public bool Condition(EntityUid uid, IEntityManager entityManager)
        => entityManager.TryGetComponent<StrapComponent>(uid, out var strap) &&
           strap.BuckledEntities.Count > 0;

    public bool DoExamine(ExaminedEvent args)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (!entityManager.TryGetComponent<StrapComponent>(args.Examined, out var strap) ||
            strap.BuckledEntities.Count > 0)
        {
            return false;
        }

        args.PushMarkup(Loc.GetString("construction-examine-condition-strap-entity",
            ("strap", args.Examined)) + "\n");
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry();
    }
}

/// <summary>
/// Applies entity effects to the construction entity after a graph transition.
/// </summary>
public sealed partial class EffectGraphAction : IGraphAction
{
    [DataField(required: true)]
    public EntityEffect[] Effects = default!;

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        entityManager.System<SharedEntityEffectsSystem>().ApplyEffects(uid, Effects, user: userUid);
    }
}
