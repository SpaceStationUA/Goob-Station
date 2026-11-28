// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using System.Numerics;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Suspicion.Spawners;

[RegisterComponent]
public sealed partial class SuspicionLootSpawnerComponent : Component
{
    [DataField(required: true)] public EntityTableSelector Table = default!;
    [DataField] public TimeSpan Delay;
    [DataField] public float Offset = 0.2f;
    [DataField] public int Count = 1;
}

public sealed class SuspicionLootSpawnerSystem : EntitySystem
{
    [Dependency] private readonly EntityTableSystem _tables = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SuspicionLootSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<SuspicionLootSpawnerComponent> ent, ref MapInitEvent args)
    {
        var coords = Transform(ent).Coordinates;
        foreach (var proto in _tables.GetSpawns(ent.Comp.Table))
        {
            var offset = ent.Comp.Offset;
            var position = coords.Offset(new Vector2(_random.NextFloat(-offset, offset), _random.NextFloat(-offset, offset)));
            var count = ent.Comp.Count;
            Timer.Spawn(ent.Comp.Delay, () =>
            {
                if (!position.IsValid(EntityManager))
                    return;
                for (var i = 0; i < count; i++)
                    SpawnAtPosition(proto, position);
            });
        }
        QueueDel(ent);
    }
}
