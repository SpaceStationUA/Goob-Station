using Content.Server.Shuttles.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Shipyard;

public sealed class MapDeleterShuttleSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    private readonly Dictionary<EntityUid, Action?> _failureCallbacks = new();
    private readonly Dictionary<EntityUid, Action?> _terminationCallbacks = new();
    private readonly Dictionary<EntityUid, Action?> _completionCallbacks = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapDeleterShuttleComponent, FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<MapDeleterShuttleComponent, EntityTerminatingEvent>(OnShuttleTerminating);
    }

    private void OnFTLCompleted(Entity<MapDeleterShuttleComponent> ent, ref FTLCompletedEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        var sourceMap = ent.Comp.SourceMap;
        var success = args.MapUid == ent.Comp.ExpectedMap;
        var failureCallback = TakeCallback(_failureCallbacks, ent.Owner);
        TakeCallback(_terminationCallbacks, ent.Owner);
        var completionCallback = TakeCallback(_completionCallbacks, ent.Owner);
        ent.Comp.Enabled = false;
        RemComp<MapDeleterShuttleComponent>(ent);

        if (success)
        {
            completionCallback?.Invoke();
            DeleteOwnedMap(sourceMap);
            return;
        }

        failureCallback?.Invoke();
    }

    private void OnShuttleTerminating(Entity<MapDeleterShuttleComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.Enabled)
            return;

        TakeCallback(_failureCallbacks, ent.Owner);
        var terminationCallback = TakeCallback(_terminationCallbacks, ent.Owner);
        TakeCallback(_completionCallbacks, ent.Owner);
        ent.Comp.Enabled = false;
        RemComp<MapDeleterShuttleComponent>(ent);

        terminationCallback?.Invoke();
    }

    private static Action? TakeCallback(Dictionary<EntityUid, Action?> callbacks, EntityUid shuttle)
    {
        if (!callbacks.Remove(shuttle, out var callback))
            return null;

        return callback;
    }

    public void SetFailureCallback(EntityUid shuttle, Action callback)
    {
        _failureCallbacks[shuttle] = callback;
    }

    public void SetTerminationCallback(EntityUid shuttle, Action callback)
    {
        _terminationCallbacks[shuttle] = callback;
    }

    public void SetCompletionCallback(EntityUid shuttle, Action callback)
    {
        _completionCallbacks[shuttle] = callback;
    }

    public void Disable(EntityUid shuttle)
    {
        _failureCallbacks.Remove(shuttle);
        _terminationCallbacks.Remove(shuttle);
        _completionCallbacks.Remove(shuttle);
        RemComp<MapDeleterShuttleComponent>(shuttle);
    }

    public void Enable(EntityUid shuttle, EntityUid sourceMap)
    {
        var comp = EnsureComp<MapDeleterShuttleComponent>(shuttle);
        comp.Enabled = true;
        comp.SourceMap = sourceMap;
    }

    public void SetExpectedMap(EntityUid shuttle, EntityUid expectedMap)
    {
        EnsureComp<MapDeleterShuttleComponent>(shuttle).ExpectedMap = expectedMap;
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
}
