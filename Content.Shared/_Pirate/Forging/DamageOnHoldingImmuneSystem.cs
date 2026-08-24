// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.Shared._Pirate.Forging;

/// <summary>
/// Relays a hot-metal protection check only to equipped gloves when damage is attempted.
/// </summary>
public sealed class DamageOnHoldingImmuneSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InventoryComponent, DamageOnHoldingAttemptEvent>(RelayToGloves);
        SubscribeLocalEvent<DamageOnHoldingImmuneComponent, DamageOnHoldingAttemptEvent>(OnDamageAttempt);
    }

    private void RelayToGloves(Entity<InventoryComponent> ent, ref DamageOnHoldingAttemptEvent args)
    {
        var slots = new InventorySystem.InventorySlotEnumerator(ent, SlotFlags.GLOVES);
        while (!args.Cancelled && slots.NextItem(out var item))
            RaiseLocalEvent(item, ref args);
    }

    private void OnDamageAttempt(Entity<DamageOnHoldingImmuneComponent> ent, ref DamageOnHoldingAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
