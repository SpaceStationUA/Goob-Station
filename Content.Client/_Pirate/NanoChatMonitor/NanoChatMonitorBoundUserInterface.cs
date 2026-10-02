// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.NanoChatMonitor;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.NanoChatMonitor;

public sealed class NanoChatMonitorBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private NanoChatMonitorWindow? _window;

    public NanoChatMonitorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<NanoChatMonitorWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.OnRequestPage += (key, start, latest, requestId) =>
            SendMessage(new NanoChatMonitorRequestPageMessage(key, start, latest, requestId));
        _window.OnRequestAttachment += id =>
            SendMessage(new NanoChatMonitorRequestAttachmentMessage(id));
        _window.OnPrintPhoto += id =>
            SendMessage(new NanoChatMonitorPrintPhotoMessage(id));
        _window.OnPrintLog += key =>
            SendMessage(new NanoChatMonitorPrintLogMessage(key));
        _window.OnDeleteLog += key =>
            SendMessage(new NanoChatMonitorDeleteLogMessage(key));

        if (State is NanoChatMonitorUiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is NanoChatMonitorUiState cast)
            _window?.UpdateState(cast);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        switch (message)
        {
            case NanoChatMonitorPageMessage page:
                _window?.HandlePage(page);
                break;
            case NanoChatMonitorAttachmentMessage attachment:
                _window?.HandleAttachment(attachment);
                break;
        }
    }
}
