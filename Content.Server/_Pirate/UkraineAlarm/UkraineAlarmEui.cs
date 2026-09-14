using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared._Pirate.UkraineAlarm;

namespace Content.Server._Pirate.UkraineAlarm;

public sealed class UkraineAlarmRegionSelectEui : BaseEui
{
    private readonly UkraineAlarmSystem _system;
    private readonly UkraineAlarmRegion[] _regions;

    public UkraineAlarmRegionSelectEui(UkraineAlarmSystem system, UkraineAlarmRegion[] regions)
    {
        _system = system;
        _regions = regions;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new UkraineAlarmRegionSelectEuiState(_regions);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is UkraineAlarmRegionSelectEuiMsg.SelectRegion select)
        {
            _system.SetPlayerRegion(Player, select.RegionId);
            Manager.CloseEui(this);
        }
    }
}


