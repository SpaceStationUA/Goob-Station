using Content.Server.Antag;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server._Pirate.StarlightSpecies;

/// <summary>Grant the source elf grimoire once per body, after job or antagonist equipment.</summary>
public sealed class ElfCantripLoadoutSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HumanoidAppearanceComponent, PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnSpawn(Entity<HumanoidAppearanceComponent> ent, ref PlayerSpawnCompleteEvent args)
        => GrantGrimoire(ent);

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(args.EntityUid, out var appearance))
            GrantGrimoire((args.EntityUid, appearance));
    }

    private void GrantGrimoire(Entity<HumanoidAppearanceComponent> ent)
    {
        if (ent.Comp.Species.Id is not ("Elf" or "NeoElf") || HasComp<ElfCantripLoadoutGrantedComponent>(ent))
            return;
        var book = SpawnAtPosition("BookOfCantrips", Transform(ent).Coordinates);
        EnsureComp<ElfCantripLoadoutGrantedComponent>(ent);
        if (_inventory.TryGetSlotEntity(ent, "back", out var backpack) && _storage.Insert(backpack.Value, book, out _))
            return;
        _hands.TryPickupAnyHand(ent, book);
    }
}
