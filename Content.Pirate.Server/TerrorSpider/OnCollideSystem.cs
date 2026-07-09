using Content.Pirate.Shared.TerrorSpider;
using Robust.Shared.Physics.Events;

namespace Content.Pirate.Server.TerrorSpider;

public sealed partial class OnCollideSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnCollideComponent, StartCollideEvent>(SpawnOnCollide);
    }

    private void SpawnOnCollide(Entity<SpawnOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.Collided)
            return;

        ent.Comp.Collided = true;
        SpawnAtPosition(ent.Comp.Prototype, Transform(args.OtherEntity).Coordinates);
    }
}
