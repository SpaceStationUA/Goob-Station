// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared.VendingMachines;

namespace Content.Server._Pirate.Security.Vending;

/// <summary>
/// Raised before a vending machine checks stock or takes payment for a requested item.
/// </summary>
public sealed class VendingMachineVendAttemptEvent(
    EntityUid user,
    InventoryType inventoryType,
    string itemId) : CancellableEntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly InventoryType InventoryType = inventoryType;
    public readonly string ItemId = itemId;
    public string? DenialMessage;
}
