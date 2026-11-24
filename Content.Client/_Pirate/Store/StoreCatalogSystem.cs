// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Store;
using Content.Shared.Store;

namespace Content.Client._Pirate.Store;

public sealed class StoreCatalogSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StoreCatalogViewerComponent, AfterAutoHandleStateEvent>(OnCatalogsChanged);
    }

    private void OnCatalogsChanged(Entity<StoreCatalogViewerComponent> viewer, ref AfterAutoHandleStateEvent args)
    {
        foreach (var store in viewer.Comp.Catalogs.Keys)
        {
            if (TryGetEntity(store, out var uid) &&
                _ui.TryGetOpenUi(uid.Value, StoreUiKey.Key, out var bui) &&
                bui is StoreCatalogBoundUserInterface { IsOpened: true } menu)
                menu.Update();
        }
    }
}
