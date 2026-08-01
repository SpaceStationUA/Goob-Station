// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Pirate.BountyHunter;

namespace Content.Server._Pirate.BountyHunter;

public sealed class BountyHunterSpawnerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BountyHunterSpawnerComponent, TakeGhostRoleEvent>(
            OnTakeGhostRole,
            before: [typeof(AntagSelectionSystem)]);
    }

    private void OnTakeGhostRole(Entity<BountyHunterSpawnerComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (args.TookRole ||
            args.Cancelled ||
            BountyHunterTargetCondition.HasEligibleTarget(EntityManager, args.Player.UserId))
            return;

        args.Cancelled = true;
        QueueDel(ent);
    }
}
