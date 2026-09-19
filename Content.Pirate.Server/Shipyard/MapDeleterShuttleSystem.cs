using Content.Server.Shuttles.Events;
using Robust.Shared.Map;

namespace Content.Pirate.Server.Shipyard;

public sealed class MapDeleterShuttleSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapDeleterShuttleComponent, FTLStartedEvent>(OnFTLStarted);
    }

    private void OnFTLStarted(Entity<MapDeleterShuttleComponent> ent, ref FTLStartedEvent args)
    {
        if (ent.Comp.Enabled && args.FromMapUid is { } fromMap && fromMap == ent.Comp.SourceMap)
            DeleteOwnedMap(ent.Comp.SourceMap);

        RemComp<MapDeleterShuttleComponent>(ent);
    }

    public void Enable(EntityUid shuttle, EntityUid sourceMap)
    {
        var comp = EnsureComp<MapDeleterShuttleComponent>(shuttle);
        comp.Enabled = true;
        comp.SourceMap = sourceMap;
    }

    public bool DeleteOwnedMap(EntityUid sourceMap)
    {
        if (!TryComp<TransformComponent>(sourceMap, out var transform) || transform.MapID == MapId.Nullspace)
            return false;

        var mapId = transform.MapID;
        if (!_map.MapExists(mapId) || _map.GetMap(mapId) != sourceMap)
            return false;

        _map.DeleteMap(mapId);
        return true;
    }
}
