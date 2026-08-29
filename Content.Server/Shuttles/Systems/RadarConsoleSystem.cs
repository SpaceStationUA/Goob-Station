// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.Shuttles.BUIStates; // Pirate - replay memory optimization.
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<RadarConsoleComponent>(RadarConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnRadarUIOpened);
            subs.Event<BoundUIClosedEvent>(OnRadarUIClosed);
        });
    }

    private void OnRadarUIOpened(EntityUid uid, RadarConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateState(uid, component);
    }

    private void OnRadarUIClosed(EntityUid uid, RadarConsoleComponent component, BoundUIClosedEvent args)
    {
        // Pirate: do not retain a large radar state in replay/PVS after the last viewer closes the UI.
        if (!_uiSystem.IsUiOpen(uid, RadarConsoleUiKey.Key))
            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, null);
    }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        // Pirate: BUI state is only useful while a console is open; avoid retaining it in replays otherwise.
        if (!_uiSystem.IsUiOpen(uid, RadarConsoleUiKey.Key))
            return;

        var xform = Transform(uid);
        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (component.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var dockingPortStates = _console.GetDockingPortStates();

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(uid, coordinates.Value, angle.Value);
            }
            else
            {
                state = _console.GetNavState(uid);
            }

            state.RotateWithEntity = !component.FollowEntity;

            _uiSystem.SetUiState(uid,
                RadarConsoleUiKey.Key,
                new NavBoundUserInterfaceState(state, dockingPortStates));
        }
    }
}
