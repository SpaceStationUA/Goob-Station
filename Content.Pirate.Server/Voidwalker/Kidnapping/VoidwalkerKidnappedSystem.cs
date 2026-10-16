using Content.Server.Respawn;
using Content.Shared.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Voidwalker.Kidnapping;

public sealed class VoidwalkerKidnappedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SpecialRespawnSystem _respawn = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const int MaxTeleportAttempts = 100;
    private static readonly TimeSpan ReturnRetryInterval = TimeSpan.FromSeconds(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var kidnappedQuery = EntityQueryEnumerator<VoidwalkerKidnappedComponent>();
        while (kidnappedQuery.MoveNext(out var uid, out var kidnapped))
        {
            if (_timing.CurTime < kidnapped.ExitVoidTime)
                continue;

            if (_mind.TryGetMind(uid, out _, out var mind))
                mind.PreventGhosting = false;

            if (TryTeleportToRandomPartOfStation(uid, kidnapped.OriginalMap))
            {
                RemCompDeferred(uid, kidnapped);
                continue;
            }

            // Salvage and other non-station maps still need a return destination.
            if (kidnapped.OriginalCoordinates.IsValid(EntityManager)
                && !TerminatingOrDeleted(kidnapped.OriginalCoordinates.EntityId))
            {
                _transform.SetCoordinates(uid, kidnapped.OriginalCoordinates);
                OnReturned(uid);
                RemCompDeferred(uid, kidnapped);
                continue;
            }

            kidnapped.ExitVoidTime = _timing.CurTime + ReturnRetryInterval;
        }
    }

    public bool TryTeleportToRandomPartOfStation(EntityUid uid, EntityUid originalMap)
    {
        if (!TryComp<TransformComponent>(originalMap, out var originalMapXform)
            || TerminatingOrDeleted(originalMap))
            return false;

        var destinationMap = originalMapXform.MapID;

        if (_station.GetStationInMap(destinationMap) is not { } station)
            return false;

        if (!TryComp<StationDataComponent>(station, out var stationData))
            return false;

        var entityGridUid = _station.GetLargestGrid((station, stationData));

        if (entityGridUid is null)
            return false;

        // Pirate: check the destination atmosphere, rather than the victim's current void map.
        if (!_respawn.TryFindRandomTile(entityGridUid.Value, originalMap, MaxTeleportAttempts, out var randomPos))
            return false;

        _transform.SetCoordinates(uid, randomPos);
        OnReturned(uid);
        return true;
    }

    private void OnReturned(EntityUid uid)
    {
        _stun.KnockdownOrStun(uid, TimeSpan.FromSeconds(5), true); // whatever, go my magic number
        _popup.PopupEntity(Loc.GetString("voidwalker-kidnap-return"), uid, uid);
    }
}
