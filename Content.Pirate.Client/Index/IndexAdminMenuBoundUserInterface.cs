// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Index;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Index;

[UsedImplicitly]
public sealed class IndexAdminMenuBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private IndexAdminMenuWindow? _window;

    public IndexAdminMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<IndexAdminMenuWindow>();

        _window.OnSendPrescription += text => SendMessage(new IndexAdminSendPrescriptionMessage(text));
        _window.OnAddKarma += amount => SendMessage(new IndexAdminAddKarmaMessage(amount));
        _window.OnRemoveKarma += amount => SendMessage(new IndexAdminRemoveKarmaMessage(amount));
        _window.OnGuaranteeFpoon += enabled => SendMessage(new IndexAdminGuaranteeFpoonMessage(enabled));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not IndexAdminBoundUserInterfaceState adminState)
            return;

        _window?.UpdateState(adminState);
    }
}
