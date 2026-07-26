// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Content.Shared.Inventory;

namespace Content.Pirate.Shared.Viewcone.Components;

/// <summary>
/// Worn clothing with this component suppresses footstep viewcone effects while equipped.
/// </summary>
[RegisterComponent]
public sealed partial class ViewconeSilentFootstepsComponent : Component;

/// <summary>
/// Cancels the viewcone footstep effect when the wearer has silent footstep clothing equipped.
/// </summary>
public sealed class ViewconeSilentFootstepsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Pirate: core InventorySystem.InitializeRelay cannot reference this event (it lives in the
        // Pirate module), so the FEET relay is registered here instead.
        SubscribeLocalEvent<InventoryComponent, CanSpawnFootstepsEvent>(OnRelay);
        SubscribeLocalEvent<ViewconeSilentFootstepsComponent, InventoryRelayedEvent<CanSpawnFootstepsEvent>>(OnAttempt);
    }

    private void OnRelay(EntityUid uid, InventoryComponent component, ref CanSpawnFootstepsEvent args)
    {
        _inventory.RelayEvent((uid, component), ref args);
    }

    // Cancel the footstep viewcone effect since this clothing makes us silent
    private void OnAttempt(Entity<ViewconeSilentFootstepsComponent> ent, ref InventoryRelayedEvent<CanSpawnFootstepsEvent> args)
    {
        args.Args.Cancelled = true;
    }
}
