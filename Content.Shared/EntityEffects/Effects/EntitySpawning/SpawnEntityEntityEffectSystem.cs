using Robust.Shared.Network;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a number of entities of a given prototype at the coordinates of this entity.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnEntityEntityEffectSystem : EntityEffectSystem<TransformComponent, SpawnEntity>
{
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<SpawnEntity> args)
    {
        var quantity = args.Effect.ShouldScale ? args.Effect.Number * (int) Math.Floor(args.Scale) : args.Effect.Number; // Goobstation - Added ShouldSCale
        var proto = args.Effect.Entity;

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                if (args.Effect.SpawnAtPosition)
                    PredictedSpawnAtPosition(proto, entity.Comp.Coordinates);
                else
                    PredictedSpawnNextToOrDrop(proto, entity, entity.Comp);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                if (args.Effect.SpawnAtPosition)
                    SpawnAtPosition(proto, entity.Comp.Coordinates);
                else
                    SpawnNextToOrDrop(proto, entity, entity.Comp);
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnEntity : BaseSpawnEntityEntityEffect<SpawnEntity>
{
    /// <summary>
    /// Pirate: initialize the spawned entity at the target's map/grid position before MapInit.
    /// </summary>
    [DataField]
    public bool SpawnAtPosition;
}
