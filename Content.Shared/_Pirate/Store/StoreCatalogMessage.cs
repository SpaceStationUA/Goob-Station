// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Store;

/// <summary>
/// Snapshot of one open store for its viewer. A null state closes that subscription.
/// </summary>
[Serializable, NetSerializable]
public sealed class StoreCatalogMessage(NetEntity viewer, NetEntity store, BoundUserInterfaceState? state) : EntityEventArgs
{
    public readonly NetEntity Viewer = viewer;
    public readonly NetEntity Store = store;
    public readonly BoundUserInterfaceState? State = state;
}
