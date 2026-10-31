using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server._Pirate.StarlightSpecies;

/// <summary>Grant the source elf grimoire alongside the native job-specific survival kit.</summary>
public sealed class ElfCantripLoadoutSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize() => SubscribeLocalEvent<HumanoidAppearanceComponent, PlayerSpawnCompleteEvent>(OnSpawn);

    private void OnSpawn(Entity<HumanoidAppearanceComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (ent.Comp.Species.Id is not ("Elf" or "NeoElf"))
            return;
        var book = SpawnAtPosition("BookOfCantrips", Transform(ent).Coordinates);
        if (_inventory.TryGetSlotEntity(ent, "back", out var backpack) && _storage.Insert(backpack.Value, book, out _))
            return;
        _hands.TryPickupAnyHand(ent, book);
    }
}
