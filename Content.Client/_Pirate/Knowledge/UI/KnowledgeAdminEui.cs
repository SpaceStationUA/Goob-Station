// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eui;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Pirate.Knowledge.UI;

[UsedImplicitly]
public sealed class KnowledgeAdminEui : BaseEui
{
    private readonly KnowledgeAdminWindow _window = new();

    public KnowledgeAdminEui()
    {
        _window.OnClose += OnWindowClosed;
        _window.ApplyRequested += OnApplyRequested;
        _window.RefreshRequested += OnRefreshRequested;
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.OnClose -= OnWindowClosed;
        _window.ApplyRequested -= OnApplyRequested;
        _window.RefreshRequested -= OnRefreshRequested;
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        _window.SetState((KnowledgeAdminEuiState) state);
    }

    private void OnWindowClosed()
        => SendMessage(new CloseEuiMessage());

    private void OnApplyRequested(Dictionary<string, KnowledgeAdminEdit> changes)
        => SendMessage(new KnowledgeAdminEuiMsg.Apply(changes));

    private void OnRefreshRequested()
        => SendMessage(new KnowledgeAdminEuiMsg.Refresh());
}
