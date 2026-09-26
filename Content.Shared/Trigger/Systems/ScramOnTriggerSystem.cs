using Content.Shared._Pirate.Trigger; // Pirate: scram grid teleport
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: scram implant station scope
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components; // Pirate: scram implant station scope
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Containers; // Goobstation

namespace Content.Shared.Trigger.Systems;

public sealed class ScramOnTriggerSystem : XOnTriggerSystem<ScramOnTriggerComponent>
{
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly TurfSystem _turfSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!; // Goobstation

    protected override void OnTrigger(Entity<ScramOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        EntityCoordinates? targetCoords = null;

        #region Pirate: scram grid teleport - teleport to the default station map with the scoped grid selection as fallback
        // Pirate: only commit the server-side trigger when there is somewhere to teleport.
        if (_net.IsServer)
        {
            var destination = new ScramDefaultMapDestinationEvent(target);
            RaiseLocalEvent(ent.Owner, ref destination);
            targetCoords = destination.Coordinates ?? SelectRandomTileInRange(target, ent.Comp.TeleportRadius);
            if (targetCoords == null)
                return;
        }
        #endregion

        // We need stop the user from being pulled so they don't just get "attached" with whoever is pulling them.
        // This can for example happen when the user is cuffed and being pulled.
        if (TryComp<PullableComponent>(target, out var pull) && _pulling.IsPulled(target, pull))
            _pulling.TryStopPull(target, pull);

        // Check if the user is pulling anything, and drop it if so.
        if (TryComp<PullerComponent>(target, out var puller) && TryComp<PullableComponent>(puller.Pulling, out var pullable))
            _pulling.TryStopPull(puller.Pulling.Value, pullable);

        _audio.PlayPredicted(ent.Comp.TeleportSound, ent, args.User);
        args.Handled = true;

        // Can't predict picking random grids and the target location might be out of PVS range.
        if (_net.IsClient)
            return;

        _transform.SetCoordinates(target, targetCoords!.Value);
    }

    /// <summary>
    /// Finds a random empty tile within a certain radius, restricted to the grid the user is
    /// currently standing on and any multiz floors physically linked to it via z-network. Will
    /// not select off-grid tiles or the current tile.
    /// </summary>
    #region Pirate: scram implant station scope - StationDataComponent.Grids also contains docked shuttles (cargo, ATS, etc), so this uses CEZLinkedGridComponent instead, which only links a station's own floor grids across Z and never shuttles
    private EntityCoordinates? SelectRandomTileInRange(EntityUid uid, float radius, PhysicsComponent? physicsComponent = null)
    {
        if (!Resolve(uid, ref physicsComponent))
            return null;

        // Pirate: an entity inside a locker or backpack scrams from the outer container's position.
        var userXform = Transform(uid);
        if (_container.IsEntityOrParentInContainer(uid))
        {
            if (!_container.TryGetOuterContainer(uid, userXform, out var container))
                return null;

            userXform = Transform(container.Owner);
        }

        var userCoords = userXform.Coordinates;
        if (userXform.GridUid is not { } currentGridUid)
            return null;

        // PeerGrids excludes the grid's own depth, so include this floor and the linked floors.
        List<EntityUid> candidateGrids;
        if (TryComp<CEZLinkedGridComponent>(currentGridUid, out var linkedGrid))
        {
            candidateGrids = new List<EntityUid>(linkedGrid.PeerGrids.Count + 1) { currentGridUid };
            candidateGrids.AddRange(linkedGrid.PeerGrids.Values);
        }
        else
        {
            candidateGrids = new List<EntityUid> { currentGridUid };
        }

        var userMapCoords = _transform.ToMapCoordinates(userCoords);
        var currentTile = _turfSystem.GetTileRef(userCoords);
        var radiusSquared = radius * radius;
        var candidates = new List<TileRef>();

        foreach (var gridUid in candidateGrids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
                continue;

            foreach (var tile in _mapSystem.GetAllTiles(gridUid, gridComp))
            {
                if (_turfSystem.IsSpace(tile)
                    || currentTile is { } current && tile.GridUid == current.GridUid && tile.GridIndices == current.GridIndices)
                    continue;

                // Linked floor grids are kept position/rotation-synced with their peers (see
                // CEZLevelsSystem.GridSync), so comparing raw world positions across different
                // maps still gives a meaningful horizontal distance here.
                var tilePosition = _mapSystem.GridTileToWorldPos(gridUid, gridComp, tile.GridIndices);
                if ((tilePosition - userMapCoords.Position).LengthSquared() <= radiusSquared)
                    candidates.Add(tile);
            }
        }

        _random.Shuffle(candidates);

        foreach (var tile in candidates)
        {
            if (!_turfSystem.IsTileBlocked(tile, (CollisionGroup)physicsComponent.CollisionMask))
                return _turfSystem.GetTileCenter(tile);
        }

        return null;
    }
    #endregion
}
