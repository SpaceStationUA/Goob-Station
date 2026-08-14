using Content.Shared._DV.Psionics.Events;
using Content.Shared.Inventory;

namespace Content.Pirate.Shared.Psionics;

public sealed class PiratePsionicInventoryRelaySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, PsionicPowerUseAttemptEvent>(OnPowerUseAttempt);
        SubscribeLocalEvent<InventoryComponent, TargetedByPsionicPowerEvent>(OnTargetedByPsionicPower);
        SubscribeLocalEvent<InventoryComponent, PsionicRollAttemptEvent>(OnRollAttempt);
        SubscribeLocalEvent<InventoryComponent, NoosphericFryEvent>(OnNoosphericFry);
        SubscribeLocalEvent<InventoryComponent, DispelledEvent>(OnDispelled);
    }

    private void OnPowerUseAttempt(Entity<InventoryComponent> inventory, ref PsionicPowerUseAttemptEvent args)
    {
        _inventory.RelayEvent(inventory, ref args);
    }

    private void OnTargetedByPsionicPower(Entity<InventoryComponent> inventory, ref TargetedByPsionicPowerEvent args)
    {
        _inventory.RelayEvent(inventory, ref args);
    }

    private void OnRollAttempt(Entity<InventoryComponent> inventory, ref PsionicRollAttemptEvent args)
    {
        _inventory.RelayEvent(inventory, ref args);
    }

    private void OnNoosphericFry(Entity<InventoryComponent> inventory, ref NoosphericFryEvent args)
    {
        args.Target = inventory.Owner;
        _inventory.RelayEvent(inventory, ref args);
    }

    private void OnDispelled(Entity<InventoryComponent> inventory, ref DispelledEvent args)
    {
        _inventory.RelayEvent(inventory, ref args);
    }
}
