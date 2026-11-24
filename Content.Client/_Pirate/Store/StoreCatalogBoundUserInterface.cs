// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._Pirate.Store;

public abstract class StoreCatalogBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected sealed override void Open()
    {
        base.Open();
        OpenStoreWindow();
        Update();
    }

    protected abstract void OpenStoreWindow();

    public sealed override void Update()
    {
        base.Update();

        if (EntMan.System<StoreCatalogSystem>().GetCatalog(Owner) is { } catalog)
            UpdateState(catalog);
    }
}
