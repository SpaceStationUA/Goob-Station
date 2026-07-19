using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Pirate.ZLevels.Power;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Pirate.ZLevels.Core;

/// <summary>
/// Handles physical synchronization of grids across Z-levels in a Z-network.
/// When grids are linked, the leader grid (depth 0) drives position, rotation,
/// and velocity for all peer grids, keeping multi-level structures moving as one.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    /// <summary>
    /// Minimum squared magnitude for the previously-synced linear velocity to count as "the
    /// structure was actually moving last frame." Below this, the detection pass skips the
    /// peer-deceleration check entirely because there's no meaningful baseline to compare
    /// against — accelerating from rest would otherwise trip a false positive every tick.
    /// </summary>
    private const float MinLinearVelocityForDetectionSq = 0.0001f;

    /// <summary>
    /// Minimum absolute magnitude for the previously-synced angular velocity to count as
    /// "the structure was actually spinning last frame." Same purpose as the linear constant.
    /// </summary>
    private const float MinAngularVelocityForDetection = 0.01f;

    /// <summary>
    /// How much of last frame's synced velocity (along the same direction) the peer must
    /// retain after physics. Below this, we treat the peer as held back by a constraint
    /// (collision or joint) on its own map and propagate the brake to the leader.
    /// Intentionally lenient — per-tick natural drag on a shuttle is far smaller than 50%
    /// of last frame's velocity, so drag won't trip it.
    /// </summary>
    private const float PeerDecelerationThreshold = 0.5f;

    /// <summary>
    /// Reentry guard. <see cref="_transform.SetLocalPositionRotation"/> on a peer fires
    /// <c>MoveEvent</c>, which can be observed by handlers anywhere in content; if any of
    /// those handlers eventually re-enters this update we'd recurse and double-sync the
    /// same network in one tick. The guard makes UpdateGridSync a no-op when already in
    /// flight rather than allowing reentry.
    /// </summary>
    private bool _gridSyncing;

    // Per-network record of the leader's linear AND angular velocity at the END of the previous
    // sync — i.e., the values that were just written into every peer. If a peer's current
    // velocity is now lower than this in the leader's direction of motion, physics has since
    // slowed it (collision or joint constraint on its own map). Tracking both axes catches
    // angular constraints too — e.g., a peer docked via WeldJoint to a static target has its
    // angular velocity pulled back by the joint while the leader spins freely.
    private readonly Dictionary<EntityUid, (Vector2 Linear, float Angular)> _lastSyncedVelocity = new();

    private void InitGridSync()
    {
        SubscribeLocalEvent<CEZLinkedGridComponent, ComponentRemove>(OnLinkedGridRemoved);
    }

    /// <summary>
    /// Finds the main grid on each map in the Z-network and links them together.
    /// </summary>
    private void LinkNetworkGrids(Entity<CEZLevelsNetworkComponent> network)
    {
        var grids = new Dictionary<int, EntityUid>();

        var mapToDepth = new Dictionary<EntityUid, int>();
        foreach (var (depth, mapUid) in network.Comp.ZLevels)
        {
            if (mapUid is null)
                continue;
            mapToDepth[mapUid.Value] = depth;
        }

        var gridQuery = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _, out var gridXform))
        {
            if (gridXform.MapUid is null)
                continue;

            if (!mapToDepth.TryGetValue(gridXform.MapUid.Value, out var depth))
                continue;

            // Multiple grids on the same map → pick the smallest UID so the choice is stable across runs.
            if (!grids.TryGetValue(depth, out var existing) || gridUid.Id < existing.Id)
                grids[depth] = gridUid;
        }

        ApplyLinkage(network, grids);
    }

    /// <summary>
    /// Links grids directly using known grid UIDs (bypasses grid discovery).
    /// </summary>
    private void LinkGridsDirectly(Entity<CEZLevelsNetworkComponent> network, Dictionary<int, EntityUid> gridsByDepth)
    {
        ApplyLinkage(network, gridsByDepth);
    }

    /// <summary>
    /// Writes a fresh <see cref="CEZLinkedGridComponent"/> onto every grid in
    /// <paramref name="gridsByDepth"/>, replacing any prior linkage. Any old peers a grid was
    /// previously linked to are notified that this grid has left their network so they don't
    /// retain a stale reference and end up driving sync into a now-foreign body.
    /// </summary>
    private void ApplyLinkage(Entity<CEZLevelsNetworkComponent> network, Dictionary<int, EntityUid> gridsByDepth)
    {
        if (gridsByDepth.Count < 2)
            return;

        // First, evict each grid from whatever network it was previously in. Without this,
        // old peers' PeerGrids dictionaries still contain stale entries pointing at the grid
        // we're about to relink — and on the next sync the old leader would forcibly write
        // position/velocity into what is now a foreign body. This is the same kind of stale
        // cross-network linkage the sync-pass guards defensively skip; better to prevent it
        // at the source. Also drop the prior network's cached sync-velocity entry while we
        // still know the leader being evicted, otherwise EntityUid recycling could later make
        // the orphaned entry collide with a different leader.
        foreach (var (_, gridUid) in gridsByDepth)
        {
            if (!TryComp<CEZLinkedGridComponent>(gridUid, out var existing) ||
                existing.ZNetwork == network.Owner)
            {
                continue;
            }

            UnlinkFromPriorNetwork(existing);

            if (existing.Depth == 0)
                _lastSyncedVelocity.Remove(existing.ZNetwork);
        }

        foreach (var (depth, gridUid) in gridsByDepth)
        {
            var comp = EnsureComp<CEZLinkedGridComponent>(gridUid);
            comp.ZNetwork = network.Owner;
            comp.Depth = depth;
            comp.PeerGrids = new Dictionary<int, EntityUid>(gridsByDepth);
            comp.PeerGrids.Remove(depth);
            Dirty(gridUid, comp);
        }

        // Snap peers onto the leader's transform up front, before the peer maps initialize and
        // their cross-Z nodes first reflood in response to the notification below. See
        // AlignPeersToLeader for why this can't wait for the per-tick sync.
        AlignPeersToLeader(gridsByDepth);

        RaiseLinkedGridPeersChanged(gridsByDepth.Values);
    }

    /// <summary>
    /// Snaps every peer grid onto the depth-0 leader's local transform at the moment linkage is
    /// established. Cross-Z nodes — cable hubs (<see cref="Power.CEMultizCableHubNode"/>) and atmos
    /// pipe adapters — locate their vertical counterpart by reprojecting a world position onto the
    /// peer grid and flooring to a tile. If the decks are still offset by a sub-tile amount at that
    /// instant (a mapper nudge, or simply the saved grid position before the runtime sync snaps it),
    /// the floored lookup lands one tile off and the link silently fails to form. That load-time
    /// reflood is one-shot — a later position sync does <b>not</b> re-flood those nodes — so the
    /// decks have to be aligned here, before the peers are notified and their maps are initialised.
    /// Mirrors <see cref="SyncPeersFromLeader"/> but writes position/rotation only (velocities are
    /// zero at link time).
    /// </summary>
    private void AlignPeersToLeader(Dictionary<int, EntityUid> gridsByDepth)
    {
        if (!gridsByDepth.TryGetValue(0, out var leaderUid) ||
            !TryComp<TransformComponent>(leaderUid, out var leaderXform))
        {
            return;
        }

        foreach (var (depth, peerUid) in gridsByDepth)
        {
            if (depth == 0 || !TryComp<TransformComponent>(peerUid, out var peerXform))
                continue;

            // Peers sharing the leader's map are the FTL-fallback overlap case; leave them be for
            // the same reason SyncPeersFromLeader skips them.
            if (peerXform.MapUid == leaderXform.MapUid)
                continue;

            if (peerXform.LocalPosition.EqualsApprox(leaderXform.LocalPosition) &&
                peerXform.LocalRotation.EqualsApprox(leaderXform.LocalRotation))
            {
                continue;
            }

            _transform.SetLocalPositionRotation(peerUid, leaderXform.LocalPosition, leaderXform.LocalRotation, peerXform);
        }
    }

    /// <summary>
    /// Walks <paramref name="existing"/>'s <see cref="CEZLinkedGridComponent.PeerGrids"/> and
    /// removes the corresponding depth entry from each peer's own PeerGrids dictionary. Used
    /// at component shutdown and when a grid is about to be relinked to a different network,
    /// so the old network's grids stop trying to drive sync into a now-foreign body.
    /// </summary>
    private void UnlinkFromPriorNetwork(CEZLinkedGridComponent existing)
    {
        foreach (var (_, peerUid) in existing.PeerGrids)
        {
            if (!TryComp<CEZLinkedGridComponent>(peerUid, out var peerLinked))
                continue;

            if (peerLinked.PeerGrids.Remove(existing.Depth))
                Dirty(peerUid, peerLinked);

            var ev = new CEMultizLinkedGridPeersChangedEvent();
            RaiseLocalEvent(peerUid, ref ev);
        }
    }

    private void OnLinkedGridRemoved(Entity<CEZLinkedGridComponent> ent, ref ComponentRemove args)
    {
        UnlinkFromPriorNetwork(ent.Comp);

        // Drop the cached last-synced-velocity entry so EntityUid recycling can't leave us
        // matching peers against a velocity from a long-dead network. Only the depth-0 grid
        // owns the entry (it's the only one that writes it during sync), so cleanup is keyed
        // off depth-0 removal.
        if (ent.Comp.Depth == 0)
            _lastSyncedVelocity.Remove(ent.Comp.ZNetwork);
    }

    private void RaiseLinkedGridPeersChanged(IEnumerable<EntityUid> gridUids)
    {
        foreach (var gridUid in gridUids)
        {
            var ev = new CEMultizLinkedGridPeersChangedEvent();
            RaiseLocalEvent(gridUid, ref ev);
        }
    }

    /// <summary>
    /// Synchronises position, rotation, linear and angular velocity across the grids of a
    /// linked Z-network so a multi-level structure moves as one rigid body.
    /// <para>
    /// Each tick proceeds in two passes per leader (depth-0 grid):
    /// </para>
    /// <list type="number">
    /// <item><b>Pass 1 (peer → leader):</b> only runs if the structure had non-trivial velocity
    /// last frame. Scans peers for "I was held back": any peer whose current linear or angular
    /// velocity falls below half of what sync wrote into it last frame, measured in the same
    /// direction. That signature only occurs when physics has actively constrained the peer on
    /// its own map — a collision with a static body or a joint pulling it back. The leader is
    /// then snapped to that peer's blocked transform and adopts its (slowed) velocity, so the
    /// whole network respects obstacles the leader's own map can't see. This is intentionally
    /// the only path that writes into the leader.</item>
    /// <item><b>Pass 2 (leader → peers):</b> unconditionally writes the leader's current
    /// position, rotation, linear and angular velocity into every peer. Pass 1 may have just
    /// rewritten the leader; either way every peer ends this tick agreeing with the leader,
    /// which prevents peers from accumulating independent momentum from per-frame physics
    /// (drag, residual velocity from the previous tick, etc.).</item>
    /// </list>
    /// </summary>
    private void UpdateGridSync(float frameTime)
    {
        if (_gridSyncing)
            return;

        _gridSyncing = true;
        try
        {
            var query = EntityQueryEnumerator<CEZLinkedGridComponent, TransformComponent, PhysicsComponent>();
            while (query.MoveNext(out var uid, out var linked, out var xform, out var body))
            {
                // Only process leaders (depth 0).
                if (linked.Depth != 0)
                    continue;

                if (linked.PeerGrids.Count == 0)
                    continue;

                // What every peer was told to move at last frame — exactly the values we wrote
                // into them at the end of the previous UpdateGridSync pass. Compared against
                // each peer's current values below, this isolates "physics decelerated me"
                // (collision or joint) from "I haven't been re-synced after the leader's thrust
                // changed" (one-frame acceleration lag).
                _lastSyncedVelocity.TryGetValue(linked.ZNetwork, out var lastSynced);
                var lastLinearMagSq = lastSynced.Linear.LengthSquared();
                var lastAngularMag = MathF.Abs(lastSynced.Angular);
                var shouldDetectBlockage =
                    lastLinearMagSq > MinLinearVelocityForDetectionSq ||
                    lastAngularMag > MinAngularVelocityForDetection;

                if (shouldDetectBlockage)
                    TryPropagatePeerBlockageToLeader(uid, linked, xform, body, lastSynced);

                SyncPeersFromLeader(uid, linked, xform, body);

                // Record what we just wrote into peers so the next tick's blockage check has a
                // baseline that exactly matches what every peer should be moving at right now.
                _lastSyncedVelocity[linked.ZNetwork] = (body.LinearVelocity, body.AngularVelocity);
            }
        }
        finally
        {
            _gridSyncing = false;
        }
    }

    /// <summary>
    /// Pass 1. Looks for any peer whose physics state shows it was held back since the previous
    /// sync, and reverse-syncs the leader to match if one is found. Iteration order is dictionary
    /// order — if multiple peers are simultaneously blocked at different positions, an arbitrary
    /// one wins. In practice they should be at near-identical positions because they all came
    /// from the same prior-frame sync, so the choice doesn't visually matter.
    /// </summary>
    private void TryPropagatePeerBlockageToLeader(
        EntityUid leaderUid,
        CEZLinkedGridComponent linked,
        TransformComponent leaderXform,
        PhysicsComponent leaderBody,
        (Vector2 Linear, float Angular) lastSynced)
    {
        var lastLinearMagSq = lastSynced.Linear.LengthSquared();
        var lastAngularMag = MathF.Abs(lastSynced.Angular);

        foreach (var (_, peerUid) in linked.PeerGrids)
        {
            if (!TryResolveSyncablePeer(peerUid, linked, leaderXform, out var peerXform, out var peerBody))
                continue;

            // peerBody is guaranteed non-null here only if we want to evaluate physics; the
            // blockage check is meaningless without a body to read velocities from.
            if (peerBody is null)
                continue;

            var linearBlocked = lastLinearMagSq > MinLinearVelocityForDetectionSq &&
                Vector2.Dot(peerBody.LinearVelocity, lastSynced.Linear)
                    < lastLinearMagSq * PeerDecelerationThreshold;

            // Sign-aware angular comparison. peer.angVel * last.angVel ≥ last.angVel² / 2 means
            // the peer is still spinning at least half as fast as we told it to, in the same
            // direction. Anything below — including an outright reversal — counts as blocked.
            // A reversed peer angVel is fairly rare (would require an elastic angular bounce or
            // an external torque on the peer) but treating that as blockage is the safe default.
            var angularBlocked = lastAngularMag > MinAngularVelocityForDetection &&
                peerBody.AngularVelocity * lastSynced.Angular
                    < lastSynced.Angular * lastSynced.Angular * PeerDecelerationThreshold;

            if (!linearBlocked && !angularBlocked)
                continue;

            _transform.SetLocalPositionRotation(leaderUid, peerXform.LocalPosition, peerXform.LocalRotation, leaderXform);
            _physics.SetLinearVelocity(leaderUid, peerBody.LinearVelocity, body: leaderBody);
            _physics.SetAngularVelocity(leaderUid, peerBody.AngularVelocity, body: leaderBody);
            return;
        }
    }

    /// <summary>
    /// Pass 2. Pushes the (possibly corrected) leader's transform and velocity into every peer
    /// that's eligible for sync.
    /// </summary>
    private void SyncPeersFromLeader(
        EntityUid leaderUid,
        CEZLinkedGridComponent linked,
        TransformComponent leaderXform,
        PhysicsComponent leaderBody)
    {
        foreach (var (_, peerUid) in linked.PeerGrids)
        {
            if (!TryResolveSyncablePeer(peerUid, linked, leaderXform, out var peerXform, out var peerBody))
                continue;

            var transformMismatch =
                !peerXform.LocalPosition.EqualsApprox(leaderXform.LocalPosition) ||
                !peerXform.LocalRotation.EqualsApprox(leaderXform.LocalRotation);

            if (transformMismatch)
                _transform.SetLocalPositionRotation(peerUid, leaderXform.LocalPosition, leaderXform.LocalRotation, peerXform);

            if (peerBody is null)
                continue;

            if (peerBody.LinearVelocity != leaderBody.LinearVelocity)
                _physics.SetLinearVelocity(peerUid, leaderBody.LinearVelocity, body: peerBody);

            if (peerBody.AngularVelocity != leaderBody.AngularVelocity)
                _physics.SetAngularVelocity(peerUid, leaderBody.AngularVelocity, body: peerBody);
        }
    }

    /// <summary>
    /// Shared eligibility check for both sync passes. A peer is "syncable" when:
    /// it still has a TransformComponent; it agrees on the network it belongs to (the leader's
    /// PeerGrids can hold stale entries pointing at grids that have since been relinked
    /// elsewhere — writing into one of those would teleport an unrelated body); and it is on a
    /// different map than the leader (peers on the leader's own map are typically there because
    /// of FTL fallback when the destination z-network was shorter than the shuttle, and
    /// teleporting overlapping dynamic bodies onto each other every frame corrupts Box2D's
    /// contact graph).
    /// </summary>
    private bool TryResolveSyncablePeer(
        EntityUid peerUid,
        CEZLinkedGridComponent leaderLinked,
        TransformComponent leaderXform,
        [NotNullWhen(true)] out TransformComponent? peerXform,
        out PhysicsComponent? peerBody)
    {
        peerBody = null;

        if (!TryComp(peerUid, out peerXform))
            return false;

        if (!TryComp<CEZLinkedGridComponent>(peerUid, out var peerLinked) ||
            peerLinked.ZNetwork != leaderLinked.ZNetwork)
            return false;

        if (peerXform.MapUid == leaderXform.MapUid)
            return false;

        TryComp(peerUid, out peerBody);
        return true;
    }

}
