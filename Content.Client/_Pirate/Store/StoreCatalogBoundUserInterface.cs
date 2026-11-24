// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Store;

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

        if (EntMan.TryGetComponent<StoreCatalogViewerComponent>(PlayerManager.LocalEntity, out var viewer) &&
            viewer.Catalogs.TryGetValue(EntMan.GetNetEntity(Owner), out var catalog))
            UpdateState(catalog);
    }
}
