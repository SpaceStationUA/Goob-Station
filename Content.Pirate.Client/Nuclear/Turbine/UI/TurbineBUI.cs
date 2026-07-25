// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Nuclear.Monitor;
using Content.Pirate.Shared.Nuclear.Turbine;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Nuclear.Turbine.UI;

/// <summary>
/// Initializes a <see cref="TurbineWindow"/>.
/// </summary>
public sealed partial class TurbineBUI(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private TurbineWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TurbineWindow>();
        _window.SetEntity(Owner, EntMan.HasComponent<NuclearMonitorComponent>(Owner));

        _window.OnChangeFlowRate += val => SendPredictedMessage(new TurbineChangeFlowRateMessage(val));
        _window.OnChangeStatorLoad += val => SendPredictedMessage(new TurbineChangeStatorLoadMessage(val));
        _window.OnEmergencyShutdown += () => SendPredictedMessage(new TurbineChangeFlowRateMessage(0f));

        if (State is TurbineBuiState state)
            _window.Update(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is TurbineBuiState cast)
            _window?.Update(cast);
    }
}
