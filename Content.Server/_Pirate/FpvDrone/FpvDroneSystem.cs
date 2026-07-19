using Content.Server.SurveillanceCamera;
using Content.Shared._Pirate.FpvDrone;
using Content.Shared._Pirate.RemoteDrone;

namespace Content.Server._Pirate.FpvDrone;

public sealed class FpvDroneSystem : SharedFpvDroneSystem
{
    [Dependency] private readonly SurveillanceCameraMonitorSystem _surveillanceMonitorSystem = default!;

    protected override void UpdateFpvSurveillance(Entity<FpvDroneComponent> entity)
    {
        base.UpdateFpvSurveillance(entity);
        if (TerminatingOrDeleted(entity.Owner) ||
            !TryComp<RemoteDroneComponent>(entity.Owner, out var remoteDroneComponent))
            return;

        if (remoteDroneComponent.LinkedControllerUid is not { } controllerUid ||
            TerminatingOrDeleted(controllerUid) ||
            !TryComp<SurveillanceCameraMonitorComponent>(controllerUid, out var controllerSurveillanceMonitorComponent))
        {
            return;
        }

        _surveillanceMonitorSystem.TrySwitchCameraByUid(controllerUid, entity.Owner, monitor: controllerSurveillanceMonitorComponent);
        _surveillanceMonitorSystem.UpdateUserInterface(controllerUid, controllerSurveillanceMonitorComponent);
    }

    protected override void OnDroneDisabled(EntityUid uid)
    {
        base.OnDroneDisabled(uid);
        if (TerminatingOrDeleted(uid))
            return;

        _surveillanceMonitorSystem.DisconnectCamera(uid, true);
    }
}
