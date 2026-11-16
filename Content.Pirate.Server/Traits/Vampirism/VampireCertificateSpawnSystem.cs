using Content.Server._Pirate.Traits;
using Content.Pirate.Server.Traits.Vampirism.Components;
using Content.Shared._Pirate.Origin.Systems;
using Content.Shared._Pirate.Vampirism;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Traits.Vampirism;

/// <summary>
/// Issues the official vampire certificate to anyone who spawns with the vampirism trait.
/// </summary>
public sealed class VampireCertificateSpawnSystem : EntitySystem
{
    private static readonly EntProtoId Certificate = "VampireCertificate";

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Run after the trait system has added the vampirism components, so we get the profile too.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(TraitSystem)]);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<VampirismComponent>(ev.Mob))
            return;

        IssueCertificate(ev.Mob, ev.Profile);
    }

    /// <summary>
    /// Creates a vampire certificate for the given mob and puts it in their backpack.
    /// </summary>
    public EntityUid? IssueCertificate(EntityUid mob, HumanoidCharacterProfile profile)
    {
        if (TerminatingOrDeleted(mob))
            return null;

        var card = Spawn(Certificate, _transform.GetMapCoordinates(mob));

        var certificate = EnsureComp<VampireCertificateComponent>(card);
        certificate.OwnerName = profile.Name;
        certificate.OwnerProfile = profile;
        certificate.PassportId = SharedPassportSystem.ComputePassportId(profile);
        Dirty(card, certificate);

        if (_inventory.TryGetSlotEntity(mob, "back", out var backpack)
            && TryComp<StorageComponent>(backpack, out var storage)
            && TryComp<ItemComponent>(card, out var item)
            && _storage.CanInsert(backpack.Value, card, out _, storage, item))
            _storage.Insert(backpack.Value, card, out _, playSound: false);

        return card;
    }
}
