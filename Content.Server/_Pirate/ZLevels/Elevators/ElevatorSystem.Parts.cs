using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Elevators;
using Content.Shared._Pirate.ZLevels.Elevators.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Robust.Shared.Map.Components;

namespace Content.Server._Pirate.ZLevels.Elevators;

// Shaft doors + floor indicators.
public sealed partial class ElevatorSystem
{
    [Dependency] private readonly SharedDoorSystem _doors = default!;

    /// <summary>Closes every shaft door of this elevator (called before travel).</summary>
    private void CloseAllDoors(string elevatorId)
    {
        var query = AllEntityQuery<ElevatorDoorComponent, DoorComponent>();
        while (query.MoveNext(out var uid, out var door, out var doorComp))
        {
            if (door.ElevatorId != elevatorId)
                continue;
            _doors.TryClose(uid, doorComp);
        }
    }

    private void OnDoorAutoClose(EntityUid uid, ElevatorDoorComponent comp, BeforeDoorAutoCloseEvent args)
    {
        args.Cancel();
    }

    /// <summary>Opens the shaft door on the cab's arrival deck; keeps the rest shut.</summary>
    private void OpenDoorOnDeck(string elevatorId, int deckDepth)
    {
        var query = AllEntityQuery<ElevatorDoorComponent, DoorComponent>();
        while (query.MoveNext(out var uid, out var door, out var doorComp))
        {
            if (door.ElevatorId != elevatorId)
                continue;

            if (TryGetEntityDeckDepth(uid, out var depth) && depth == deckDepth)
                _doors.TryOpen(uid, doorComp);
            else
                _doors.TryClose(uid, doorComp);
        }
    }

    private void OnIndicatorExamine(Entity<ElevatorIndicatorComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetController(ent.Comp.ElevatorId, out var controller) || !controller.Value.Comp.Initialized)
            return;

        var comp = controller.Value.Comp;
        var floor = DisplayFloor(comp, comp.CurrentDepth);
        var status = !comp.Moving
            ? Loc.GetString("elevator-status-idle")
            : comp.TargetDepth > comp.CurrentDepth
                ? Loc.GetString("elevator-status-going-up")
                : Loc.GetString("elevator-status-going-down");
        args.PushText(Loc.GetString("elevator-indicator-examine", ("floor", floor), ("status", status)));
    }

    /// <summary>Pushes floor + direction to every indicator of this elevator.</summary>
    private void UpdateIndicators(string elevatorId, int floorNumber, ElevatorDirection direction)
    {
        var query = AllEntityQuery<ElevatorIndicatorComponent>();
        while (query.MoveNext(out var uid, out var indicator))
        {
            if (indicator.ElevatorId != elevatorId)
                continue;

            // The client visualizer turns Floor into the digit sprite, and Direction into the arrow.
            _appearance.SetData(uid, ElevatorIndicatorVisuals.Floor, floorNumber);
            _appearance.SetData(uid, ElevatorIndicatorVisuals.Direction, direction);
        }
    }

    /// <summary>Resolves the z-network depth of the deck an entity sits on.</summary>
    private bool TryGetEntityDeckDepth(EntityUid uid, out int depth)
    {
        depth = 0;
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid)
            return false;
        if (!TryComp<CEZLinkedGridComponent>(gridUid, out var linked))
            return false;
        depth = linked.Depth;
        return true;
    }

    /// <summary>1-based display floor number for a depth (lowest served deck = 1).</summary>
    private int DisplayFloor(ElevatorControllerComponent comp, int depth)
    {
        var index = comp.ServedDepths.IndexOf(depth);
        return index >= 0 ? index + 1 : depth;
    }
}
