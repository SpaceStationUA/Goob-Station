using Content.Client.Eui;
using Content.Shared._Pirate.UkraineAlarm;
using Content.Shared.Eui;

namespace Content.Client._Pirate.UkraineAlarm;

public sealed class UkraineAlarmRegionSelectEui : BaseEui
{
    private UkraineAlarmRegionSelectWindow? _window;
    private UkraineAlarmRegion[] _regions = [];

    public override void Opened()
    {
        _window = new UkraineAlarmRegionSelectWindow();
        _window.RegionSelected += regionId => SendMessage(new UkraineAlarmRegionSelectEuiMsg.SelectRegion(regionId));
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
        _window.SetRegions(_regions);
    }

    public override void Closed()
    {
        _window?.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not UkraineAlarmRegionSelectEuiState selectState)
            return;

        _regions = selectState.Regions;
        _window?.SetRegions(_regions);
    }
}
