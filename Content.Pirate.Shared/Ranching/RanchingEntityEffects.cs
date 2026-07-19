// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.EntitySpawning;
using Content.Shared.NPC.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Ranching;

public sealed partial class AddComponents : EntityEffectBase<AddComponents>
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

public sealed class AddComponentsEffectSystem : EntityEffectSystem<MetaDataComponent, AddComponents>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<AddComponents> args)
    {
        EntityManager.AddComponents(ent.Owner, args.Effect.Components);
    }
}

public sealed partial class RemoveComponents : EntityEffectBase<RemoveComponents>
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

public sealed class RemoveComponentsEffectSystem : EntityEffectSystem<MetaDataComponent, RemoveComponents>
{
    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<RemoveComponents> args)
    {
        EntityManager.RemoveComponents(ent.Owner, args.Effect.Components);
    }
}

public sealed partial class SpawnRandomEntities : BaseSpawnEntityEntityEffect<SpawnRandomEntities>
{
    [DataField]
    public int Min = 1;
}

public sealed class SpawnRandomEntitiesEffectSystem : EntityEffectSystem<TransformComponent, SpawnRandomEntities>
{
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Effect(Entity<TransformComponent> ent, ref EntityEffectEvent<SpawnRandomEntities> args)
    {
        var seed = SharedRandomExtensions.HashCodeCombine(
            (int) _timing.CurTick.Value,
            GetNetEntity(ent.Owner).Id);
        var random = new Random(seed);
        var quantity = random.Next(args.Effect.Min, args.Effect.Number + 1);

        for (var i = 0; i < quantity; i++)
            PredictedSpawnNextToOrDrop(args.Effect.Entity, ent);
    }
}

public sealed partial class SpawnFriendly : BaseSpawnEntityEntityEffect<SpawnFriendly>;

public sealed class SpawnFriendlyEffectSystem : EntityEffectSystem<TransformComponent, SpawnFriendly>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;

    protected override void Effect(Entity<TransformComponent> ent, ref EntityEffectEvent<SpawnFriendly> args)
    {
        var quantity = args.Effect.ShouldScale
            ? args.Effect.Number * (int) Math.Floor(args.Scale)
            : args.Effect.Number;

        for (var i = 0; i < quantity; i++)
        {
            EntityUid spawned;
            if (args.Effect.Predicted)
                spawned = PredictedSpawnNextToOrDrop(args.Effect.Entity, ent);
            else if (_net.IsServer)
                spawned = SpawnNextToOrDrop(args.Effect.Entity, ent);
            else
                continue;

            _factions.IgnoreEntity(spawned, ent.Owner);
        }
    }
}

public sealed partial class ThrowRandomly : EntityEffectBase<ThrowRandomly>
{
    [DataField]
    public float Speed = 10f;
}

public sealed class ThrowRandomlyEffectSystem : EntityEffectSystem<MetaDataComponent, ThrowRandomly>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<ThrowRandomly> args)
    {
        var seed = SharedRandomExtensions.HashCodeCombine(
            (int) _timing.CurTick.Value,
            GetNetEntity(ent.Owner).Id);
        var random = new Random(seed);
        _throwing.TryThrow(
            ent.Owner,
            random.NextAngle().ToVec(),
            baseThrowSpeed: args.Effect.Speed,
            user: args.User);
    }
}

public sealed partial class RelayNearby : EntityEffectBase<RelayNearby>
{
    [DataField(required: true)]
    public EntityEffect Effect = default!;

    [DataField(required: true)]
    public string CompName = string.Empty;

    internal Type? Comp;

    [DataField]
    public float Range = 5f;

    [DataField]
    public LookupFlags Flags = LookupFlags.All;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}

public sealed class RelayNearbyEffectSystem : EntityEffectSystem<TransformComponent, RelayNearby>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<IComponent>> _found = new();

    protected override void Effect(Entity<TransformComponent> ent, ref EntityEffectEvent<RelayNearby> args)
    {
        var effect = args.Effect;
        var type = effect.Comp ??= Factory.GetRegistration(effect.CompName).Type;

        _found.Clear();
        var coordinates = _transform.GetMapCoordinates(ent.Owner, ent.Comp);
        _lookup.GetEntitiesInRange(type, coordinates, effect.Range, _found, effect.Flags);

        foreach (var found in _found)
        {
            if (found.Owner == ent.Owner ||
                !_whitelist.CheckBoth(found.Owner, effect.Blacklist, effect.Whitelist))
                continue;

            _effects.TryApplyEffect(found.Owner, effect.Effect, args.Scale, args.User);
        }
    }
}
