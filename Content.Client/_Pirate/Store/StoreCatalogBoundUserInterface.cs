// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Store;
using Content.Shared.Store;

namespace Content.Client._Pirate.Store;

public abstract class StoreCatalogBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();
        SendMessage(new StoreRequestUpdateInterfaceMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is StoreCatalogMessage catalog)
            UpdateState(catalog.State);
    }
}
