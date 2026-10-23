using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared.Wetness.Components;
using Content.Pirate.Shared.Wetness.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Fluids;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Wetness;

public sealed class WetnessSystem : SharedWetnessSystem
{
    [Dependency] private readonly SharedPuddleSystem _puddle = null!;
    [Dependency] private readonly SharedContainerSystem _container = null!;
    [Dependency] private readonly IRobustRandom _random = null!;

    private readonly List<Entity<WettableComponent>> _dueToDry = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WettableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WettableComponent, WetnessChangedEvent>(OnWetnessChanged);
    }

    private void OnMapInit(Entity<WettableComponent> ent, ref MapInitEvent args)
    {
        UpdateDrying(ent);
    }

    private void OnWetnessChanged(Entity<WettableComponent> ent, ref WetnessChangedEvent args)
    {
        UpdateDrying(ent);
    }

    private void UpdateDrying(Entity<WettableComponent> ent)
    {
        if (ent.Comp.Wetness > 0)
            EnsureComp<WetDryingComponent>(ent);
        else
            RemComp<WetDryingComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;

        // Defer marker changes until after enumeration.
        _dueToDry.Clear();
        var query = EntityQueryEnumerator<WetDryingComponent, WettableComponent>();
        while (query.MoveNext(out var uid, out _, out var comp))
        {
            if (now >= comp.NextDryTime)
                _dueToDry.Add((uid, comp));
        }

        foreach (var ent in _dueToDry)
        {
            var (uid, comp) = ent;
            if (TerminatingOrDeleted(uid) || comp.Wetness <= 0)
                continue;

            var dried = FixedPoint2.Min(comp.DryPerStep, comp.Wetness);
            var remaining = comp.Wetness - dried;

            var drip = FixedPoint2.Zero;
            if (remaining > 0 && _random.Prob(comp.DripChance) && CanDrip(uid))
                drip = FixedPoint2.Min(comp.DripAmount, remaining);

            RemoveWetness(ent, dried + drip);

            if (drip > 0)
            {
                var water = new Solution();
                water.AddReagent("Water", drip);
                _puddle.TrySpillAt(uid, water, out _, sound: false);
            }

            comp.NextDryTime = comp.Wetness > 0 ? now + NextDryDelay(comp) : TimeSpan.Zero;
            Dirty(uid, comp);
        }
    }

    private bool CanDrip(EntityUid item)
    {
        if (!_container.TryGetContainingContainer(item, out var container))
            return true;

        return HasComp<InventoryComponent>(container.Owner) || HasComp<HandsComponent>(container.Owner);
    }
}
