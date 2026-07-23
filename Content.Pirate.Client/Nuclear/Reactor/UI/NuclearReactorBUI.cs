// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Nuclear.Monitor;
using Content.Pirate.Shared.Nuclear.Reactor;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Nuclear.Reactor.UI;

/// <summary>
/// Initializes a <see cref="NuclearReactorWindow"/> and updates it when new server messages are received.
/// </summary>
public sealed class NuclearReactorBUI(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private NuclearReactorWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<NuclearReactorWindow>();
        _window.SetEntity(Owner, EntMan.HasComponent<NuclearMonitorComponent>(Owner));

        _window.OnSwapPart += pos => SendPredictedMessage(new ReactorSwapPartMessage(pos));
        _window.OnEjectItem += () => SendPredictedMessage(new ReactorEjectItemMessage());
        _window.OnAdjustControlRods += change => SendPredictedMessage(new ReactorAdjustControlRodsMessage(change));
        _window.OnEmergencyShutdown += () => SendPredictedMessage(new ReactorAdjustControlRodsMessage(2f));

        if (State is NuclearReactorBuiState state)
            _window.Update(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is NuclearReactorBuiState cast)
            _window?.Update(cast);
    }

}
