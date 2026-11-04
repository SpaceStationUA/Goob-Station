using Content.Shared._Pirate;
using Content.Shared._Pirate.Origin;
using Content.Shared._Pirate.Origin.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Content.Shared.PDA;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Origin.Systems;

public class SharedPassportSystem : EntitySystem
{
    private const string PIDChars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _sharedTransformSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PassportComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PassportComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, PassportComponent component, ExaminedEvent args)
    {
        var profile = component.OwnerProfile;
        if (!args.IsInDetailsRange || component.IsClosed || profile == null)
            return;

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(profile.Species, out var species))
            return;

        args.PushMarkup($"Ім’я: {profile.Name}", 50);
        args.PushMarkup($"Раса: {Loc.GetString(species.Name)}", 49);
        args.PushMarkup($"Стать: {profile.Gender}", 48);
        args.PushMarkup($"Зріст: {MathF.Round(profile.Height * species.AverageHeight)} см", 47);
        args.PushMarkup($"Ширина: {MathF.Round(profile.Width * species.AverageWidth)} см", 46);
        args.PushMarkup($"Дата народження: {PirateStationCalendar.CurrentYear - profile.Age}", 45);

        args.PushMarkup(
            $"PID: {GenerateIdentityString(profile.Name
            + profile.Height
            + profile.Age
            + profile.Width
            + profile.FlavorText)}",
            44);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev) =>
        SpawnPassportForPlayer(ev.Mob, ev.Profile, ev.JobId);

    public void SpawnPassportForPlayer(EntityUid mob, HumanoidCharacterProfile profile, string? jobId)
    {
        if (Deleted(mob) || !Exists(mob))
            return;

        if (jobId != null && _prototypeManager.TryIndex<JobPrototype>(
                jobId, out var jobPrototype))
        {
            if (!jobPrototype.CanHavePassport)
            {
                return;
            }
        }

        if (!_prototypeManager.TryIndex(profile.Citizenship, out CitizenshipPrototype? citizenshipPrototype)
            || !_prototypeManager.TryIndex(citizenshipPrototype.PassportPrototype, out EntityPrototype? entityPrototype))
            return;

        var coordinates = _sharedTransformSystem.GetMapCoordinates(mob);
        var passportEntity = EntityManager.SpawnEntity(entityPrototype.ID, coordinates);
        var passportComponent = EntityManager.GetComponent<PassportComponent>(passportEntity);

        UpdatePassportProfile(new(passportEntity, passportComponent), profile);

        if (_inventory.TryGetSlotEntity(mob, "id", out var pdaEntity)
            && EntityManager.TryGetComponent<PdaComponent>(pdaEntity, out var pda)
            && _itemSlots.TryInsert(pdaEntity.Value, pda.PassportSlot, passportEntity, user: null))
            return;

        if (_inventory.TryGetSlotEntity(mob, "back", out var backpack)
            && EntityManager.TryGetComponent<StorageComponent>(backpack, out var storage)
            && EntityManager.TryGetComponent<ItemComponent>(passportEntity, out var passportItem)
            && _storage.CanInsert(backpack.Value, passportEntity, out _, storage, passportItem))
            _storage.Insert(backpack.Value, passportEntity, out _, playSound: false);
    }

    public void UpdatePassportProfile(Entity<PassportComponent> passport, HumanoidCharacterProfile profile)
    {
        passport.Comp.OwnerProfile = profile;
    }

    private void OnUseInHand(Entity<PassportComponent> passport, ref UseInHandEvent evt)
    {
        if (evt.Handled || !_timing.IsFirstTimePredicted)
            return;

        evt.Handled = true;
        passport.Comp.IsClosed = !passport.Comp.IsClosed;
        Dirty(passport);

        var passportEvent = new PassportToggleEvent();
        RaiseLocalEvent(passport, ref passportEvent);
    }

    private static string GenerateIdentityString(string seed)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in seed)
            {
                hash ^= character;
                hash *= 16777619;
            }

            var random = new System.Random((int) hash);

            char[] result = new char[17];

            int j = 0;
            for (int i = 0; i < 15; i++)
            {
                if (i == 5 || i == 10)
                {
                    result[j++] = '-';
                }
                result[j++] = PIDChars[random.Next(PIDChars.Length)];
            }

            return new string(result);
        }
    }

    [ByRefEvent]
    public sealed class PassportToggleEvent : HandledEntityEventArgs {}
}
