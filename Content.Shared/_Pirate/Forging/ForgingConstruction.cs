// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Construction;
using Content.Shared.Examine;

namespace Content.Shared._Pirate.Forging;

public sealed partial class FinishForgedItem : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        entityManager.System<ForgingSystem>().FinishForgedItem(uid, userUid);
    }
}

public sealed partial class QuenchMetal : IGraphCondition
{
    public bool Condition(EntityUid uid, IEntityManager entityManager)
        => !entityManager.System<SharedMetalSystem>().IsWorkable(uid);

    public bool DoExamine(ExaminedEvent args)
    {
        var metal = IoCManager.Resolve<IEntityManager>().System<SharedMetalSystem>();
        if (!metal.IsWorkable(args.Examined))
            return false;

        args.PushMarkup(Loc.GetString("forging-quench-first") + "\n");
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry();
    }
}
