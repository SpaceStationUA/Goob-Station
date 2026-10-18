// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;
using Content.Pirate.Shared.Security.Tackle;

namespace Content.Pirate.Shared.Security.Tackle;

public sealed class TackleInventorySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, TackleEvent>(_inventory.RelayEvent);
    }
}
