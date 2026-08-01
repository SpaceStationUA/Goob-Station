// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Decals;
using Content.Shared.Coordinates;
using Content.Shared.Throwing;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed class BloodSplatterOnLandSystem : EntitySystem
{
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodSplatterOnLandComponent, LandEvent>(OnLand);
    }

    private void OnLand(Entity<BloodSplatterOnLandComponent> ent, ref LandEvent args)
    {
        _decals.TryAddDecal(
            ent.Comp.Decal,
            ent.Owner.ToCoordinates(),
            out _,
            ent.Comp.Color,
            _random.NextAngle(),
            zIndex: 5,
            cleanable: true);

        if (ent.Comp.DeleteEntity)
            QueueDel(ent);
    }
}
