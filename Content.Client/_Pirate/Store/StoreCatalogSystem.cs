// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Store;
using Content.Shared.Store;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.Store;

public sealed class StoreCatalogSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    // A store can arrive before its window or before the client attaches to the viewer.
    private readonly Dictionary<NetEntity, Dictionary<NetEntity, BoundUserInterfaceState>> _catalogs = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<StoreCatalogMessage>(OnCatalog);
        SubscribeNetworkEvent<StoreCatalogsClosedMessage>(msg => _catalogs.Remove(msg.Viewer));
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
    }

    public BoundUserInterfaceState? GetCatalog(EntityUid store)
    {
        if (_player.LocalEntity is not { } viewer || !_catalogs.TryGetValue(GetNetEntity(viewer), out var stores))
            return null;

        return stores.GetValueOrDefault(GetNetEntity(store));
    }

    private void OnCatalog(StoreCatalogMessage message)
    {
        if (message.State is not { } state)
        {
            if (_catalogs.TryGetValue(message.Viewer, out var stores))
                stores.Remove(message.Store);
            return;
        }

        _catalogs.GetOrNew(message.Viewer)[message.Store] = state;
        if (GetNetEntity(_player.LocalEntity) == message.Viewer &&
            TryGetEntity(message.Store, out var uid) &&
            _ui.TryGetOpenUi(uid.Value, StoreUiKey.Key, out var bui) &&
            bui is StoreCatalogBoundUserInterface { IsOpened: true } menu)
            menu.Update();
    }

    private void OnDetached(LocalPlayerDetachedEvent ev)
    {
        if (TryGetNetEntity(ev.Entity, out var viewer))
            _catalogs.Remove(viewer.Value);
    }
}
