// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Store;

/// <summary>
/// Sends a store's current catalogue to the actor browsing it.
/// </summary>
[Serializable, NetSerializable]
public sealed class StoreCatalogMessage(BoundUserInterfaceState state) : BoundUserInterfaceMessage
{
    public readonly BoundUserInterfaceState State = state;
}
