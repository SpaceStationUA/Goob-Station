// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Pirate.Shared.Familiar;
using Content.Pirate.Shared.Spawners;
using Content.Server.Construction;
using Content.Shared.Ghost.Roles.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Spawners;

/// <summary>
/// Chooses and configures the demon produced by a summoning rune.
/// </summary>
public sealed class RandomDemonSpawnerSystem : EntitySystem
{
    [Dependency] private readonly FamiliarSystem _familiar = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SpawnOnDespawnSystem _spawnOnDespawn = default!;
    private EntityQuery<RandomDemonSpawnerComponent> _spawnerQuery;

    private static readonly EntProtoId FamiliarRole = "MindRoleGhostRoleFamiliar";

    public override void Initialize()
    {
        base.Initialize();

        _spawnerQuery = GetEntityQuery<RandomDemonSpawnerComponent>();
        SubscribeLocalEvent<RandomDemonSpawnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RandomDemonSpawnerComponent, ConstructionChangeEntityEvent>(OnConstructionChanged);
        SubscribeLocalEvent<GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
    }

    private void OnMapInit(Entity<RandomDemonSpawnerComponent> ent, ref MapInitEvent args)
    {
        var demon = _random.Pick(ent.Comp.Demons);
        EnsureComp<GhostRoleMobSpawnerComponent>(ent).Prototype = demon;
        var despawn = EnsureComp<SpawnOnDespawnComponent>(ent);
        _spawnOnDespawn.SetPrototype((ent, despawn), demon);
    }

    private void OnConstructionChanged(Entity<RandomDemonSpawnerComponent> ent, ref ConstructionChangeEntityEvent args)
    {
        if (ent.Owner == args.Old || args.User is not { } user || _random.Prob(ent.Comp.HostileChance))
            return;

        ent.Comp.Familiar = true;
        MakeGhostRoleFamiliar(ent.Owner);
        _familiar.SetMaster(ent.Owner, user);
    }

    private void OnSpawnerUsed(GhostRoleSpawnerUsedEvent args)
    {
        if (!_spawnerQuery.TryComp(args.Spawner, out var comp) || !comp.Familiar)
            return;

        // The event is raised before the new mob receives its mind.
        _familiar.CopyMaster(args.Spawner, args.Spawned);
        MakeGhostRoleFamiliar(args.Spawned);
    }

    private void MakeGhostRoleFamiliar(EntityUid uid)
    {
        var role = Comp<GhostRoleComponent>(uid);
        role.RoleName = "ghost-role-information-demon-tame-name";
        role.RoleDescription = "ghost-role-information-demon-tame-desc";
        role.RoleRules = "ghost-role-information-familiar-rules";
        role.MindRoles.Clear();
        role.MindRoles.Add(FamiliarRole);
    }
}
