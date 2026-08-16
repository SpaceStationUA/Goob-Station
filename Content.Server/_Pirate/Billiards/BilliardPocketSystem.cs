// SPDX-License-Identifier: MIT

using Content.Pirate.Shared.Billiards;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Physics.Events;

namespace Content.Server._Pirate.Billiards;

public sealed class BilliardPocketSystem : EntitySystem
{
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BilliardTableComponent, StartCollideEvent>(OnTableCollide);
    }

    private void OnTableCollide(Entity<BilliardTableComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixtureId.StartsWith("pocket_", StringComparison.Ordinal) ||
            !HasComp<BilliardBallComponent>(args.OtherEntity) ||
            !TryComp<StorageComponent>(ent, out var storage))
        {
            return;
        }

        _storage.Insert(ent.Owner, args.OtherEntity, out _, storageComp: storage, playSound: false);
    }
}
