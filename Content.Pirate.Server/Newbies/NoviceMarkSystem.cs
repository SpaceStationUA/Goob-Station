using Content.Server.Players.PlayTimeTracking;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Newbies;

public sealed class NoviceMarkSystem : EntitySystem
{
    private const string NeckSlot = "neck";

    private static readonly EntProtoId NoviceMarkPrototype = "ClothingNeckNoviceMark";

    private static readonly TimeSpan NovicePlaytimeThreshold = TimeSpan.FromHours(24);

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (_playTimeTracking.GetOverallPlaytime(args.Player) >= NovicePlaytimeThreshold)
            return;

        if (_inventory.TryGetSlotEntity(args.Mob, NeckSlot, out var neckItem))
        {
            if (TryComp<MetaDataComponent>(neckItem, out var meta) &&
                meta.EntityPrototype?.ID == NoviceMarkPrototype.Id)
                return;

            _inventory.SpawnItemOnEntity(args.Mob, NoviceMarkPrototype);
            return;
        }

        if (_inventory.SpawnItemInSlot(args.Mob, NeckSlot, NoviceMarkPrototype, silent: true, force: true))
            return;

        _inventory.SpawnItemOnEntity(args.Mob, NoviceMarkPrototype);
    }
}
