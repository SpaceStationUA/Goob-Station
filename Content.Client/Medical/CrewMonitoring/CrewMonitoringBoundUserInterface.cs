using Content.Client._Pirate.Medical.CrewMonitoring;
using Content.Client.PDA;
using Content.Shared._Pirate.ZLevels.Monitoring;
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

        _menu = this.CreateWindow<CrewMonitoringWindow>();

        // Pirate Start - New Monitor: PdaBorderColor / UiVisuals theme
        if (EntMan.TryGetComponent<PdaBorderColorComponent>(Owner, out var border))
        {
            _menu.BorderColor = border.BorderColor;
        }

        if (EntMan.TryGetComponent<CrewMonitoringUiVisualsComponent>(Owner, out var visuals))
            _menu.ApplyScreenTheme(visuals.ThemeColor);
        // Pirate End

        // Pirate Start - New Monitor: BUI callbacks
        _menu.OnAlertMutedChanged = muted => SendMessage(new CrewMonitoringSetAlertMutedMessage(muted));
        _menu.OnAlertVolumeChanged = volume => SendMessage(new CrewMonitoringSetAlertVolumeMessage(volume));
        _menu.OnSelectServer = server => SendMessage(new CrewMonitoringSelectServerMessage(server));
        _menu.OnScanStarted = () => SendMessage(new CrewMonitoringScanStartMessage());
        _menu.OnScanComplete = () => SendMessage(new CrewMonitoringScanCompleteMessage());
        _menu.OnRescan = () => SendMessage(new CrewMonitoringRescanMessage());
        _menu.OnResetSensors = () => SendMessage(new CrewMonitoringResetSensorsMessage());
        _menu.OnZLevelSelected += (grid, depth) => SendMessage(new CEZMonitoringConsoleLevelSelectedMessage(EntMan.GetNetEntity(grid), depth)); // Pirate: multiz
        // Pirate End
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        switch (state)
        {
            case CrewMonitoringState st:
                EntMan.TryGetComponent<TransformComponent>(Owner, out var xform);
                // Pirate Start - New Monitor: pass full BUI state (was Sensors list + bool)
                _menu?.ShowSensors(st, Owner, xform?.Coordinates);
                // Pirate End
                break;
        }
    }
}
