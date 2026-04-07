using Content.Pirate.Shared.Alchemy.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Movement.Events;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistTrailSystem : EntitySystem
{
    [Dependency] private readonly PuddleSystem _puddles = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlchemistTrailComponent, MoveEvent>(OnMove);
    }

    private void OnMove(Entity<AlchemistTrailComponent> ent, ref MoveEvent args)
    {
        if (args.OldPosition == args.NewPosition)
            return;

        var quantity = FixedPoint2.New(ent.Comp.SpillQuantity);
        var solution = new Solution(ent.Comp.TrailReagent, quantity);
        _puddles.TrySpillAt(ent.Owner, solution, out _, sound: false);
    }
}
