using Content.Shared._Pirate.Cleanup; // Pirate
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
                var spawned = args.Effect.SpawnAtPosition
                    ? PredictedSpawnAtPosition(proto, entity.Comp.Coordinates)
                    : PredictedSpawnNextToOrDrop(proto, entity, entity.Comp);
                if (args.Effect.ProducesLitter) // Pirate
                    EntityManager.NotifyLitterProduced(spawned);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                var spawned = args.Effect.SpawnAtPosition
                    ? SpawnAtPosition(proto, entity.Comp.Coordinates)
                    : SpawnNextToOrDrop(proto, entity, entity.Comp);
                if (args.Effect.ProducesLitter) // Pirate
                    EntityManager.NotifyLitterProduced(spawned);
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

    /// <summary>Pirate: this effect produces a temporary spill or disposable item.</summary>
    [DataField]
    public bool ProducesLitter;
}
