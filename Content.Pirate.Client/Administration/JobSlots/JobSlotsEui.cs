// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eui;
using Content.Pirate.Shared.Administration.JobSlots;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Pirate.Client.Administration.JobSlots;

[UsedImplicitly]
public sealed class JobSlotsEui : BaseEui
{
    private readonly JobSlotsWindow _window;
    private bool _closing;

    public JobSlotsEui()
    {
        _window = new JobSlotsWindow();
        _window.AdjustmentRequested += (station, job, amount) =>
            SendMessage(new AdjustJobSlotsEuiMessage(station, job, amount));
        _window.RefreshRequested += () => SendMessage(new RefreshJobSlotsEuiMessage());
        _window.OnClose += () =>
        {
            if (!_closing)
                SendMessage(new CloseEuiMessage());
        };
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _closing = true;
        _window.Close();
        _closing = false;
        base.Closed();
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is JobSlotsEuiState jobSlotsState)
            _window.UpdateState(jobSlotsState.Stations);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is JobSlotsUpdateEuiMessage update)
            _window.ShowResult(update.Result, update.Job, update.Slots);
    }
}
