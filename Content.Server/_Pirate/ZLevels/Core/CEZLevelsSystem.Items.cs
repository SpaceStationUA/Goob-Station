/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    private const float ItemZGravityForce = 9.8f;
    private const float ItemZVelocityLimit = 20f;
    private const float ItemImpactVelocityLimit = 3f;
    // Initial upward Z-velocity given to an item thrown while the thrower has LookUp on. Matches
    // CMU's CMUSharedZLevelsSystem.Throwing.cs (ThrowUpZVelocity = 6.5).
    private const float ItemThrowUpZVelocity = 6.5f;
    private static readonly EntProtoId ItemFallVfx = "CEDustEffect";

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    private readonly HashSet<EntityUid> _tileItemLookup = new();

    private void InitItems()
    {
        SubscribeLocalEvent<ItemComponent, StopThrowEvent>(OnItemStopThrow);
        SubscribeLocalEvent<ItemComponent, DroppedEvent>(OnItemDropped);
        SubscribeLocalEvent<ItemComponent, MoveEvent>(OnItemMoved);
        SubscribeLocalEvent<ItemComponent, IsWeightlessEvent>(OnItemIsWeightless);
        SubscribeLocalEvent<ItemComponent, ThrowEvent>(OnItemThrown); // Pirate: multiz - ThrownEvent renamed to ThrowEvent upstream
        SubscribeLocalEvent<CEZItemPhysicsComponent, GotEquippedHandEvent>(OnItemGotEquippedHand);
        SubscribeLocalEvent<CEZItemPhysicsComponent, GotEquippedEvent>(OnItemGotEquipped);
        SubscribeLocalEvent<CEZItemPhysicsComponent, EntGotInsertedIntoContainerMessage>(OnItemInsertedIntoContainer);
        SubscribeLocalEvent<CEZItemPhysicsComponent, EntParentChangedMessage>(OnItemZPhysicsParentChanged);
        // TileChangedEvent is subscribed once on the shared base; we hook in via OnTileChangedServer.
    }

    // A settled item sheds its Z-physics component, so nothing re-engages it when the floor tile
    // beneath is later removed (deletion fires no MoveEvent) — it would hover over the new hole.
    // Re-arm physics for items on any tile that just became empty so they fall.
    protected override void OnTileChangedServer(Entity<MapGridComponent> grid, ReadOnlySpan<TileChangedEntry> changes)
    {
        foreach (var change in changes)
        {
            if (change.OldTile.IsEmpty || !change.NewTile.IsEmpty)
                continue;

            _tileItemLookup.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid.Owner, change.GridIndices, _tileItemLookup, -0.05f, LookupFlags.Uncontained);
            foreach (var uid in _tileItemLookup)
            {
                if (HasComp<ItemComponent>(uid) && !HasComp<CEZItemPhysicsComponent>(uid))
                    TryAddItemZPhysics(uid, requireZGravity: true);
            }
        }
    }

    // Only engage Z-physics on throw when aiming across Z (LookUp). Plain throws must stay free of
    // the component during flight, else the client's per-frame sprite drive fights the throw
    // animation and short throws look twitchy.
    private void OnItemThrown(Entity<ItemComponent> ent, ref ThrowEvent args) // Pirate: multiz - ThrownEvent renamed to ThrowEvent upstream
    {
        if (args.User is not { } user ||
            !TryComp<CEZLevelViewerComponent>(user, out var viewer) ||
            !viewer.LookUp)
        {
            return;
        }

        TryAddItemZPhysics(ent.Owner);

        if (!TryComp<CEZItemPhysicsComponent>(ent.Owner, out var zItem) ||
            zItem.ZVelocity >= ItemThrowUpZVelocity)
        {
            return;
        }

        zItem.ZVelocity = ItemThrowUpZVelocity;
        Dirty(ent.Owner, zItem);
    }

    // requireZGravity: only acquire the component when the item is over an opening. Adding it on
    // solid floor and dropping it on the next settle tick churns networked state every throw,
    // which resets the sprite mid-flight (short-throw twitch).
    private void OnItemStopThrow(Entity<ItemComponent> ent, ref StopThrowEvent args)
    {
        TryAddItemZPhysics(ent.Owner, requireZGravity: true);
    }

    private void OnItemDropped(Entity<ItemComponent> ent, ref DroppedEvent args)
    {
        TryAddItemZPhysics(ent.Owner, requireZGravity: true);
    }

    private void OnItemMoved(Entity<ItemComponent> ent, ref MoveEvent args)
    {
        if (HasComp<CEZItemPhysicsComponent>(ent.Owner))
            return;

        TryAddItemZPhysics(ent.Owner, true);
    }

    private void OnItemGotEquippedHand(Entity<CEZItemPhysicsComponent> ent, ref GotEquippedHandEvent args)
    {
        RemoveItemZPhysics(ent.Owner);
    }

    private void OnItemGotEquipped(Entity<CEZItemPhysicsComponent> ent, ref GotEquippedEvent args)
    {
        RemoveItemZPhysics(ent.Owner);
    }

    private void OnItemInsertedIntoContainer(Entity<CEZItemPhysicsComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        RemoveItemZPhysics(ent.Owner);
    }

    private void OnItemZPhysicsParentChanged(Entity<CEZItemPhysicsComponent> ent, ref EntParentChangedMessage args)
    {
        if (!IsItemRestingOnMapOrGrid(args.Transform))
            RemoveItemZPhysics(ent.Owner);
    }

    private void TryAddItemZPhysics(EntityUid item, bool requireZGravity = false)
    {
        var xform = Transform(item);
        if (!IsItemRestingOnMapOrGrid(xform))
            return;

        if (requireZGravity && !CanItemExperienceZGravity(item, xform))
            return;

        var zItem = EnsureComp<CEZItemPhysicsComponent>(item);
        zItem.LocalPosition = 0f;
        zItem.ZVelocity = 0f;
        zItem.HadZFall = false;
        Dirty(item, zItem);
        UpdateItemGravityInfluence(item, xform);
    }

    private void RemoveItemZPhysics(EntityUid item)
    {
        RemComp<CEZItemPhysicsComponent>(item);
        RemComp<CEZGravityInfluencedComponent>(item);
    }

    // Per-item vertical physics tick, mirroring CMU's ProcessZPhysics.
    private void UpdateItems(float frameTime)
    {
        var query = EntityQueryEnumerator<CEZItemPhysicsComponent, ItemComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zItem, out _, out var xform))
        {
            if (!IsItemRestingOnMapOrGrid(xform))
            {
                RemoveItemZPhysics(uid);
                continue;
            }

            var oldLocalPosition = zItem.LocalPosition;
            TryComp<PhysicsComponent>(uid, out var physics);
            var onGround = physics is null || physics.BodyStatus == BodyStatus.OnGround;

            var groundHeight = ComputeItemGroundHeight(uid, xform);
            var hasSupport = groundHeight >= 0f;

            // Gravity applies on the ground or while rising. The `|| ZVelocity > 0` clause is what
            // decelerates a LookUp-throw into a parabolic arc instead of a straight launch.
            if (onGround || zItem.ZVelocity > 0f)
                zItem.ZVelocity = MathF.Max(zItem.ZVelocity - ItemZGravityForce * frameTime, -ItemZVelocityLimit);

            zItem.LocalPosition += zItem.ZVelocity * frameTime;
            var distanceToGround = zItem.LocalPosition - groundHeight;

            // AutoStep — snap up to ground top. Killing downward velocity here lets the settle
            // check below trip, otherwise gravity keeps accumulating against the floor.
            if (hasSupport && distanceToGround < 0f)
            {
                zItem.LocalPosition = groundHeight;
                if (zItem.ZVelocity < 0f)
                    zItem.ZVelocity = 0f;
            }

            // Fall-through: below local floor and no support here → descend a Z.
            if (zItem.LocalPosition < 0f)
            {
                if (CanItemDescendFromCurrentTile(uid, xform) && TryMoveDown(uid, bypassPassability: true))
                {
                    zItem.LocalPosition += 1f;
                    zItem.HadZFall = true;
                    var fallEv = new CEZLevelFallMapEvent();
                    RaiseLocalEvent(uid, ref fallEv);
                }
                else
                {
                    FinishItemZFall(uid, zItem);
                    continue;
                }
            }

            // Rise-through: open ceiling → move up a Z; closed → cap at 1. Gated on ZVelocity > 0
            // (thrown-up only): a resting item snapped to a stair peak (1.05) would otherwise loop
            // up through the hole above the stairs and fall back forever.
            if (zItem.LocalPosition >= 1f)
            {
                if (zItem.ZVelocity > 0f && !HasTileAbove(uid) && TryMoveUp(uid, bypassPassability: true))
                {
                    zItem.LocalPosition -= 1f;
                }
                else
                {
                    zItem.LocalPosition = 1f;
                    if (zItem.ZVelocity > 0f)
                        zItem.ZVelocity = 0f;
                }
            }

            // Settle once on the ground at rest and the throw has finished. Stays active during the
            // throw so AutoStep can still trigger on tiles crossed mid-flight.
            if (onGround &&
                hasSupport &&
                !HasComp<ThrownItemComponent>(uid) &&
                MathF.Abs(zItem.LocalPosition - MathF.Max(0f, groundHeight)) <= 0.05f &&
                MathF.Abs(zItem.ZVelocity) < 0.3f)
            {
                zItem.LocalPosition = MathF.Max(0f, groundHeight);
                FinishItemZFall(uid, zItem);
                continue;
            }

            DirtyItemZVisuals(uid, zItem, oldLocalPosition);
        }
    }

    /// <summary>
    /// Height of solid support at the item's tile: <c>&gt;= 0</c> = support (0 plain floor,
    /// 1.05 high-ground), <c>&lt; 0</c> = no support, item falls through.
    /// </summary>
    private float ComputeItemGroundHeight(EntityUid uid, TransformComponent xform)
    {
        var worldPos = _transform.GetWorldPosition(uid);
        EntityUid gridUid;
        MapGridComponent grid;

        if (xform.GridUid is { } dg && TryComp<MapGridComponent>(dg, out var dgc))
        {
            gridUid = dg;
            grid = dgc;
        }
        else if (xform.MapUid is { } mapUid &&
                 TryResolveGridAtWorldPositionOnMap(mapUid, worldPos, out var rg, out var rgc))
        {
            gridUid = rg;
            grid = rgc;
        }
        else
        {
            return -1f;
        }
        var tileIndices = _map.WorldToTile(gridUid, grid, worldPos);

        var found = false;
        var peakHeight = 0f;
        var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
        while (enumerator.MoveNext(out var anchored))
        {
            // SupportOnlyFromAbove high-ground (ladder bases) holds climbers from above only; it
            // must not lift an item resting on the same tile.
            if (anchored is not { } anchoredUid ||
                !TryComp<CEZLevelHighGroundComponent>(anchoredUid, out var hg) ||
                hg.SupportOnlyFromAbove ||
                hg.HeightCurve.Count == 0)
                continue;

            var peak = 0f;
            for (var i = 0; i < hg.HeightCurve.Count; i++)
                peak = MathF.Max(peak, hg.HeightCurve[i]);

            if (!found || peak > peakHeight)
            {
                peakHeight = peak;
                found = true;
            }
        }

        if (found)
            return peakHeight;

        if (_map.TryGetTileRef(gridUid, grid, tileIndices, out var tileRef) && !tileRef.Tile.IsEmpty)
            return 0f;

        return -1f;
    }

    private void DirtyItemZVisuals(EntityUid item, CEZItemPhysicsComponent zItem, float oldLocalPosition)
    {
        if (Math.Abs(oldLocalPosition - zItem.LocalPosition) > 0.01f)
            DirtyField(item, zItem, nameof(CEZItemPhysicsComponent.LocalPosition));
    }

    private void FinishItemZFall(EntityUid item, CEZItemPhysicsComponent zItem, bool spawnImpact = true)
    {
        if (spawnImpact &&
            zItem.HadZFall &&
            MathF.Abs(zItem.ZVelocity) >= ItemImpactVelocityLimit)
        {
            SpawnAtPosition(ItemFallVfx, Transform(item).Coordinates);
        }

        zItem.LocalPosition = 0f;
        zItem.ZVelocity = 0f;
        RemoveItemZPhysics(item);
    }

    private bool UpdateItemGravityInfluence(EntityUid item, TransformComponent xform)
    {
        var hasZGravity = CanItemExperienceZGravity(item, xform);

        if (hasZGravity)
            EnsureComp<CEZGravityInfluencedComponent>(item);
        else
            RemComp<CEZGravityInfluencedComponent>(item);

        return hasZGravity;
    }

    private void OnItemIsWeightless(Entity<ItemComponent> ent, ref IsWeightlessEvent args)
    {
        if (args.Handled || !TryComp(ent.Owner, out TransformComponent? xform))
            return;

        if (!CanItemExperienceZGravity(ent.Owner, xform))
            return;

        args.IsWeightless = false;
        args.Handled = true;
    }

    private bool CanItemExperienceZGravity(EntityUid item, TransformComponent xform)
    {
        if (!IsItemRestingOnMapOrGrid(xform))
            return false;

        var worldPos = _transform.GetWorldPosition(item);
        if (HasCurrentLevelFloor(xform, worldPos))
            return false;

        return HasGridGravityFromBelow(item, xform);
    }

    private bool CanItemDescendFromCurrentTile(EntityUid item, TransformComponent xform)
    {
        if (!CanItemExperienceZGravity(item, xform))
            return false;

        if (IsLandingBlocked(item, xform))
            return false;

        return true;
    }

    private bool HasCurrentLevelFloor(TransformComponent xform, Vector2 worldPos)
    {
        if (xform.GridUid is { } currentGridUid &&
            TryComp<MapGridComponent>(currentGridUid, out var currentGrid))
        {
            return HasFloorTile(currentGridUid, currentGrid, worldPos);
        }

        if (xform.MapUid is { } mapUid &&
            TryResolveGridAtWorldPositionOnMap(mapUid, worldPos, out var resolvedGridUid, out var resolvedGrid))
        {
            return HasFloorTile(resolvedGridUid, resolvedGrid, worldPos);
        }

        return false;
    }

    private bool HasFloorTile(EntityUid gridUid, MapGridComponent grid, Vector2 worldPos)
    {
        var tile = _map.GetTileRef(gridUid, grid, _map.WorldToTile(gridUid, grid, worldPos));
        return !tile.Tile.IsEmpty;
    }

    private static bool IsItemRestingOnMapOrGrid(TransformComponent xform)
    {
        // Anchored devices that carry ItemComponent are fixed to the grid — never fall/rise.
        if (xform.Anchored)
            return false;

        if (xform.MapUid is { } mapUid && xform.ParentUid == mapUid)
            return true;

        return xform.GridUid is { } gridUid && xform.ParentUid == gridUid;
    }
}
