// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eui;
using Content.Shared._Pirate.Revolution;
using Content.Shared.Eui;

namespace Content.Client._Pirate.Revolution;

public sealed class PirateRevolutionOrderEui : BaseEui
{
    private readonly PirateRevolutionOrderWindow _window = new();

    public PirateRevolutionOrderEui()
    {
        _window.OnRequest += SendMessage;
        _window.OnClose += RequestClose;
    }

    public override void Opened()
    {
        var availableHeight = _window.UserInterfaceManager.WindowRoot.Height;
        if (availableHeight > 0)
            _window.SetHeight = Math.Clamp(availableHeight - 12, _window.MinHeight, _window.SetHeight);

        _window.OpenCentered();
        SendMessage(new RevOrderReadHistory(0));
    }

    public override void Closed()
    {
        _window.OnRequest -= SendMessage;
        _window.OnClose -= RequestClose;
        _window.Close();
        _window.Dispose();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is PirateRevolutionOrderEuiState orders)
            _window.SetState(orders);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is RevOrderActivityState activity)
            _window.SetActivity(activity);
    }

    private void RequestClose() => SendMessage(new CloseEuiMessage());
}
