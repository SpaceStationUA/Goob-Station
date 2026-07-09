using Content.Server.Objectives.Components;

namespace Content.Server.Objectives.Systems;

public sealed partial class NumberObjectiveSystem
{
    public void SetTarget(EntityUid uid, int target, NumberObjectiveComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Target = target;
        RefreshNumberObjectiveMetadata(uid, comp);
    }

    public void SetTitle(EntityUid uid, string title, NumberObjectiveComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Title = title;
        RefreshNumberObjectiveMetadata(uid, comp);
    }

    public void SetDescription(EntityUid uid, string description, NumberObjectiveComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Description = description;
        RefreshNumberObjectiveMetadata(uid, comp);
    }

    private void RefreshNumberObjectiveMetadata(EntityUid uid, NumberObjectiveComponent comp)
    {
        if (!TryComp<MetaDataComponent>(uid, out var meta))
            return;

        if (comp.Title != null)
            _metaData.SetEntityName(uid, Loc.GetString(comp.Title, ("count", comp.Target)), meta);

        if (comp.Description != null)
            _metaData.SetEntityDescription(uid, Loc.GetString(comp.Description, ("count", comp.Target)), meta);
    }
}
