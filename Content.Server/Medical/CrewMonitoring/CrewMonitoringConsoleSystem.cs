// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Medical.CrewMonitoring; // Pirate: departmental handheld monitors.
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using Content.Shared._Pirate.ZLevels.Monitoring; // Pirate: multiz
using Content.Goobstation.Shared.CrewMonitoring;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.PowerCell;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Medical.CrewMonitoring;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Pinpointer;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components; // Pirate: multiz
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.CrewMonitoring;

public sealed class CrewMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<CrewMonitoringConsoleComponent, CEZMonitoringConsoleLevelSelectedMessage>(OnZLevelSelected); // Pirate: multiz
    }

    private void OnRemove(EntityUid uid, CrewMonitoringConsoleComponent component, ComponentRemove args)
    {
        component.ConnectedSensors.Clear();
    }

    private void OnPacketReceived(EntityUid uid, CrewMonitoringConsoleComponent component, DeviceNetworkPacketEvent args)
    {
        var payload = args.Data;

        // Check command
        if (!payload.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;

        if (command != DeviceNetworkConstants.CmdUpdatedState)
            return;

        if (!payload.TryGetValue(SuitSensorConstants.NET_STATUS_COLLECTION, out Dictionary<string, SuitSensorStatus>? sensorStatus))
            return;
        component.ConnectedSensors = sensorStatus;

        UpdateUserInterface(uid, component);
    }

    private void OnUIOpened(EntityUid uid, CrewMonitoringConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (!_cell.TryUseActivatableCharge(uid))
            return;

        UpdateUserInterface(uid, component);
    }

    #region Pirate: multiz
    private void OnZLevelSelected(EntityUid uid, CrewMonitoringConsoleComponent component, CEZMonitoringConsoleLevelSelectedMessage args)
    {
        var targetGrid = GetEntity(args.Grid);
        if (targetGrid == null)
            return;

        var xform = Transform(uid);
        if (xform.GridUid == null || !IsValidZMonitoringGrid(xform.GridUid.Value, targetGrid.Value))
            return;

        // Guard against a stale/malformed payload that resolves to a non-grid entity; mutating
        // an arbitrary entity with NavMapComponent would corrupt unrelated state.
        if (!HasComp<MapGridComponent>(targetGrid.Value))
            return;

        EnsureComp<NavMapComponent>(targetGrid.Value);
    }

    private bool IsValidZMonitoringGrid(EntityUid sourceGrid, EntityUid targetGrid)
    {
        if (sourceGrid == targetGrid)
            return true;

        // Both sides must carry the linked-grid marker AND point at a real network — two
        // unlinked grids both holding default ZNetwork would otherwise compare equal.
        return TryComp<CEZLinkedGridComponent>(sourceGrid, out var sourceLinked) &&
               TryComp<CEZLinkedGridComponent>(targetGrid, out var targetLinked) &&
               sourceLinked.ZNetwork.IsValid() &&
               sourceLinked.ZNetwork == targetLinked.ZNetwork;
    }
    #endregion

    private void UpdateUserInterface(EntityUid uid, CrewMonitoringConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_uiSystem.IsUiOpen(uid, CrewMonitoringUIKey.Key))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        var xform = Transform(uid);

        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        // Update all sensors info
        // GoobStation - Start
        var isCommandOnly = HasComp<CrewMonitorScanningComponent>(uid);

        // Pirate: BrigBuddy-style monitors expose only explicitly configured departments.
        HashSet<string>? departmentNames = null;
        if (TryComp<CrewMonitoringDepartmentFilterComponent>(uid, out var departmentFilter))
        {
            departmentNames = new HashSet<string>();
            foreach (var departmentId in departmentFilter.ShownDepartments)
            {
                if (_prototype.TryIndex(departmentId, out DepartmentPrototype? department))
                    departmentNames.Add(Loc.GetString(department.Name));
            }
        }

        var filteredSensors = component.ConnectedSensors
            .Where(pair => isCommandOnly
                ? pair.Value.IsCommandTracker
                : !pair.Value.IsCommandTracker)
            .Where(pair => departmentNames == null ||
                pair.Value.JobDepartments.Any(departmentNames.Contains))
            .Select(pair => pair.Value)
            .ToList();
        _uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key, new CrewMonitoringState(filteredSensors));
        // GoobStation - End
        //var allSensors = component.ConnectedSensors.Values.ToList();
        //_uiSystem.SetUiState(uid, CrewMonitoringUIKey.Key, new CrewMonitoringState(allSensors));
    }
}
