using Content.Server.Shuttles.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Pirate.Server.Shipyard;

public sealed class MapDeleterShuttleSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    private readonly Dictionary<EntityUid, PendingShuttle> _pending = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapDeleterShuttleComponent, FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<MapDeleterShuttleComponent, EntityTerminatingEvent>(OnShuttleTerminating);
    }

    private void OnFTLCompleted(Entity<MapDeleterShuttleComponent> ent, ref FTLCompletedEvent args)
    {
        if (!ent.Comp.Enabled ||
            !_pending.TryGetValue(ent.Owner, out var pending) ||
            !pending.Armed)
        {
            return;
        }

        _pending.Remove(ent.Owner);
        ent.Comp.Enabled = false;
        RemComp<MapDeleterShuttleComponent>(ent);

        if (args.MapUid != pending.ExpectedMap)
        {
            pending.Failure?.Invoke();
            return;
        }

        pending.Completion?.Invoke();
        DeleteOwnedMap(pending.SourceMap);
    }

    private void OnShuttleTerminating(Entity<MapDeleterShuttleComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.Enabled || !_pending.Remove(ent.Owner, out var pending))
            return;

        ent.Comp.Enabled = false;
        RemComp<MapDeleterShuttleComponent>(ent);
        pending.Termination?.Invoke();
    }

    public void SetCallbacks(EntityUid shuttle, Action failure, Action completion, Action termination)
    {
        if (_pending.TryGetValue(shuttle, out var pending))
        {
            _pending[shuttle] = pending with
            {
                Failure = failure,
                Completion = completion,
                Termination = termination
            };
        }
    }

    public ArmStatus Arm(EntityUid shuttle)
    {
        if (!_pending.TryGetValue(shuttle, out var pending))
        {
            // Completion and termination remove the marker and pending entry before invoking callbacks.
            // Therefore an enabled marker without an entry is the recoverable missing state, while
            // an absent or disabled marker means those callbacks already consumed the state.
            return TryComp<MapDeleterShuttleComponent>(shuttle, out var missingMarker) && missingMarker.Enabled
                ? ArmStatus.Missing
                : ArmStatus.AlreadyHandled;
        }

        if (!TryComp<MapDeleterShuttleComponent>(shuttle, out var marker) || !marker.Enabled)
            return ArmStatus.Missing;

        _pending[shuttle] = pending with { Armed = true };
        return ArmStatus.Armed;
    }
    public bool RestoreAndArm(EntityUid shuttle, EntityUid sourceMap, EntityUid expectedMap, Action failure,
        Action completion, Action termination)
    {
        if (!TryComp<MapDeleterShuttleComponent>(shuttle, out var marker) ||
            !marker.Enabled ||
            !expectedMap.IsValid() ||
            marker.ExpectedMap != expectedMap ||
            _pending.ContainsKey(shuttle))
        {
            return false;
        }

        _pending[shuttle] = new PendingShuttle(sourceMap, expectedMap, failure, completion, termination, Armed: true);
        return true;
    }


    public bool IsPending(EntityUid shuttle)
    {
        return _pending.ContainsKey(shuttle);
    }

    public bool IsArmed(EntityUid shuttle)
    {
        return _pending.TryGetValue(shuttle, out var pending) && pending.Armed;
    }

    public void Disable(EntityUid shuttle)
    {
        _pending.Remove(shuttle);
        RemComp<MapDeleterShuttleComponent>(shuttle);
    }
    public void Enable(EntityUid shuttle, EntityUid sourceMap, EntityUid expectedMap)
    {
        if (!expectedMap.IsValid())
            return;

        var comp = EnsureComp<MapDeleterShuttleComponent>(shuttle);
        comp.Enabled = true;
        comp.ExpectedMap = expectedMap;
        _pending[shuttle] = new PendingShuttle(sourceMap, expectedMap);
    }

    public bool DeleteOwnedMap(EntityUid sourceMap)
    {
        if (TerminatingOrDeleted(sourceMap) ||
            !TryComp<TransformComponent>(sourceMap, out var transform) ||
            transform.MapID == MapId.Nullspace)
        {
            return false;
        }

        var mapId = transform.MapID;
        if (!_map.MapExists(mapId) || _map.GetMap(mapId) != sourceMap)
            return false;

        _map.DeleteMap(mapId);
        return true;
    }

    public enum ArmStatus
    {
        Armed,
        AlreadyHandled,
        Missing
    }

    private readonly record struct PendingShuttle(
        EntityUid SourceMap,
        EntityUid ExpectedMap,
        Action? Failure = null,
        Action? Completion = null,
        Action? Termination = null,
        bool Armed = false);
}
