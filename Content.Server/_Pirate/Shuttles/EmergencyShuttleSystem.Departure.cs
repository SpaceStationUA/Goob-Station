// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    private void MarkEvacuationDeparture(EntityUid shuttle, EntityUid destination)
    {
        if (TryComp<FTLComponent>(_shuttle.ResolveFTLShuttle(shuttle), out var ftl) &&
            ftl.State == FTLState.Starting &&
            _transformSystem.GetMapId(ftl.TargetCoordinates) == Transform(destination).MapID)
            ftl.EmergencyDeparture = true;
    }

    private bool MayEvacuate()
    {
        var ev = new EmergencyShuttleDepartureAttemptEvent();
        RaiseLocalEvent(ref ev);
        return !ev.Cancelled;
    }
}

[ByRefEvent]
public record struct EmergencyShuttleDepartureAttemptEvent(bool Cancelled = false);
