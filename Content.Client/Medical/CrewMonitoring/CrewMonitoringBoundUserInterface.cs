// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.ListeningPost; // Pirate: listening post long-range monitor
using Content.Shared._Pirate.ZLevels.Monitoring; // Pirate: multiz
using Content.Shared.Medical.CrewMonitoring;
using Robust.Client.UserInterface;

namespace Content.Client.Medical.CrewMonitoring;

public sealed class CrewMonitoringBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CrewMonitoringWindow? _menu;

    public CrewMonitoringBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        EntityUid? gridUid = null;
        var stationName = string.Empty;

        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
        {
            gridUid = xform.GridUid;

            if (EntMan.HasComponent<LongRangeCrewMonitorComponent>(Owner))
            {
                gridUid = EntMan.System<LongRangeCrewMonitorSystem>().FindLargestStationGridInMap(xform.MapID) ?? xform.GridUid;
            }

            if (EntMan.TryGetComponent<MetaDataComponent>(gridUid, out var metaData))
            {
                stationName = metaData.EntityName;
            }
        }

        _menu = this.CreateWindow<CrewMonitoringWindow>();
        _menu.SendZLevelSelectedMessageAction += SendZLevelSelectedMessage; // Pirate: multiz
        _menu.Set(stationName, gridUid);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case CrewMonitoringState st:
                EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
                _menu?.ShowSensors(st.Sensors, Owner, xform?.Coordinates);
                break;
        }
    }

    #region Pirate: multiz
    private void SendZLevelSelectedMessage(NetEntity? grid, int depth)
    {
        SendMessage(new CEZMonitoringConsoleLevelSelectedMessage(grid, depth));
    }
    #endregion
}
