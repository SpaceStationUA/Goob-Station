// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Store;

/// <summary>
/// Current catalogues for this player's open stores, keyed by the store entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StoreCatalogViewerComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [AutoNetworkedField]
    public Dictionary<NetEntity, BoundUserInterfaceState> Catalogs = new();
}
