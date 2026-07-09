using Content.Pirate.Shared.Stains.Components;
using Content.Pirate.Shared.Stains.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Tag;

namespace Content.Pirate.Server.Stains;

public sealed class StainSystem : SharedStainSystem
{
    [Dependency] private readonly TagSystem _tag = null!;

    protected override void OnStained(Entity<StainableComponent> ent, Entity<SolutionComponent> solution)
    {
        base.OnStained(ent, solution);

        if (_tag.HasTag(ent.Owner, "DNASolutionScannable"))
            return;

        _tag.AddTag(ent.Owner, "DNASolutionScannable");
        ent.Comp.AddedDnaSolutionScannable = true;
    }

    protected override void OnCleaned(Entity<StainableComponent> ent)
    {
        base.OnCleaned(ent);

        if (!ent.Comp.AddedDnaSolutionScannable)
            return;

        _tag.RemoveTag(ent.Owner, "DNASolutionScannable");
        ent.Comp.AddedDnaSolutionScannable = false;
    }
}
