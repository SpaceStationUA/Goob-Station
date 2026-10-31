// Pirate: Starlight frame lifecycle with native profile loadout storage.
using System.Linq;
using Content.Shared._Pirate.StarlightSpecies.Neocyte;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Polymorph;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Pirate.StarlightSpecies.Neocyte;

/// <summary>
/// Ensures every Neocyte has a frame while preserving frames selected by normal and antagonist loadouts.
/// </summary>
public sealed partial class NeocyteSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<EntityUid> _pendingFrameChecks = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NeocyteComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NeocyteComponent, Content.Shared.GameTicking.PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<NeocyteComponent, StartingGearEquippedEvent>(OnStartingGearEquipped,
            after: [typeof(LoadoutSystem)]);
        SubscribeLocalEvent<NeocyteComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<NeocyteComponent, PolymorphedEvent>(OnNeocytePolymorphed);
        SubscribeLocalEvent<NeocyteFrameMemoryComponent, PolymorphedEvent>(OnFrameMemoryPolymorphed);
        SubscribeLocalEvent<NeocyteComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingFrameChecks.Count == 0)
            return;

        var pendingFrameChecks = _pendingFrameChecks.ToArray();
        _pendingFrameChecks.Clear();

        foreach (var uid in pendingFrameChecks)
        {
            if (Deleted(uid) || !TryComp(uid, out NeocyteComponent? component))
                continue;

            EnsureFrame((uid, component));
        }
    }

    private void OnMapInit(Entity<NeocyteComponent> entity, ref MapInitEvent args) =>
        // Station, antag, and admin spawning apply their loadouts after MapInit. Waiting until Update lets those
        // explicit choices win while still covering entities spawned directly from their prototype.
        _pendingFrameChecks.Add(entity);

    private void OnStartingGearEquipped(Entity<NeocyteComponent> entity, ref StartingGearEquippedEvent args)
    {
        _pendingFrameChecks.Add(entity); // Pirate: wait until the selected species frame is available after spawning.
    }

    private void OnDidEquip(Entity<NeocyteComponent> entity, ref DidEquipEvent args)
    {
        if (args.Slot != entity.Comp.FrameSlot)
            return;

        RememberFrame(entity.Comp, args.Equipment);
    }

    private void OnNeocytePolymorphed(Entity<NeocyteComponent> entity, ref PolymorphedEvent args)
    {
        // On a normal revert, the memory component represents the frame from before the polymorph.
        if (args.IsRevert && HasComp<NeocyteFrameMemoryComponent>(entity))
            return;

        if (_inventory.TryGetSlotEntity(entity, entity.Comp.FrameSlot, out var equippedFrame))
            RememberFrame(entity.Comp, equippedFrame.Value);

        CarryFrameMemory(args.NewEntity, entity.Comp.LastFramePrototype);
    }

    private void OnFrameMemoryPolymorphed(
        Entity<NeocyteFrameMemoryComponent> entity,
        ref PolymorphedEvent args)
    {
        // While transforming away, a current Neocyte form's actually equipped frame is the one that should be remembered, not the memory component's frame.
        if (!args.IsRevert && HasComp<NeocyteComponent>(entity))
            return;

        CarryFrameMemory(args.NewEntity, entity.Comp.FramePrototype);
    }

    private void CarryFrameMemory(EntityUid target, EntProtoId? framePrototype)
    {
        if (framePrototype is not { } frame)
            return;

        EnsureComp<NeocyteFrameMemoryComponent>(target).FramePrototype = frame;

        if (!TryComp(target, out NeocyteComponent? neocyte))
            return;

        neocyte.LastFramePrototype = frame;
        EnsureFrame((target, neocyte));
    }

    private void OnShutdown(Entity<NeocyteComponent> entity, ref ComponentShutdown args) => _pendingFrameChecks.Remove(entity);

    private void EnsureFrame(Entity<NeocyteComponent> entity)
    {
        if (_inventory.TryGetSlotEntity(entity, entity.Comp.FrameSlot, out var equippedFrame))
        {
            RememberFrame(entity.Comp, equippedFrame.Value);
            return;
        }

        if (entity.Comp.LastFramePrototype is { } lastFrame &&
            TryEquipFrame(entity, lastFrame))
        {
            return;
        }

        if (!_prototypeManager.TryIndex(entity.Comp.FrameLoadoutGroup, out var frameGroup))
        {
            Log.Error($"Neocyte {ToPrettyString(entity)} has an invalid frame loadout group: {entity.Comp.FrameLoadoutGroup}");
            return;
        }

        var validFrames = new List<string>();
        foreach (var loadoutId in frameGroup.Loadouts)
        {
            if (_prototypeManager.TryIndex(loadoutId, out var loadout) &&
                TryGetGearForSlot(loadout, entity.Comp.FrameSlot, out var framePrototype))
                validFrames.Add(framePrototype);
        }

        if (validFrames.Count == 0)
        {
            Log.Error($"Neocyte frame loadout group {frameGroup.ID} has no loadouts for slot {entity.Comp.FrameSlot}");
            return;
        }

        var selectedFrame = _random.Pick(validFrames);
        if (!TryEquipFrame(entity, selectedFrame))
        {
            Log.Error($"Failed to equip fallback Neocyte frame {selectedFrame} to {ToPrettyString(entity)}");
        }
    }

    private bool TryEquipFrame(Entity<NeocyteComponent> entity, string framePrototype)
    {
        if (!_inventory.SpawnItemInSlot(
                entity,
                entity.Comp.FrameSlot,
                framePrototype,
                silent: true,
                force: true))
        {
            return false;
        }

        entity.Comp.LastFramePrototype = framePrototype;
        return true;
    }

    private void RememberFrame(NeocyteComponent component, EntityUid frame)
    {
        if (MetaData(frame).EntityPrototype is { } prototype)
            component.LastFramePrototype = prototype.ID;
    }

    private void OnSpawnComplete(Entity<NeocyteComponent> ent, ref Content.Shared.GameTicking.PlayerSpawnCompleteEvent args)
    {
        if (_inventory.TryGetSlotEntity(ent, ent.Comp.FrameSlot, out _))
            return; // Explicit antagonist or job equipment takes precedence.
        if (args.Profile.Loadouts.TryGetValue("NeocyteLoadout", out var loadout) &&
            loadout.SelectedLoadouts.TryGetValue(ent.Comp.FrameLoadoutGroup, out var choices) &&
            choices.Count > 0 &&
            _prototypeManager.TryIndex(ent.Comp.FrameLoadoutGroup, out var group) &&
            group.Loadouts.Contains(choices[0].Prototype) &&
            _prototypeManager.TryIndex(choices[0].Prototype, out var selected) &&
            TryGetGearForSlot(selected, ent.Comp.FrameSlot, out var frame))
            ent.Comp.LastFramePrototype = frame;
        else if (_prototypeManager.TryIndex(ent.Comp.FrameLoadoutGroup, out var defaults) && defaults.Loadouts.Count > 0 &&
                 _prototypeManager.TryIndex(defaults.Loadouts[0], out var defaultLoadout) &&
                 TryGetGearForSlot(defaultLoadout, ent.Comp.FrameSlot, out var defaultFrame))
            ent.Comp.LastFramePrototype = defaultFrame;
        _pendingFrameChecks.Add(ent);
    }

    private bool TryGetGearForSlot(LoadoutPrototype loadout, string slot, out string gear)
    {
        if (loadout.Equipment.TryGetValue(slot, out var equipment))
        {
            gear = equipment;
            return true;
        }
        if (_prototypeManager.Resolve(loadout.StartingGear, out StartingGearPrototype? startingGear) &&
            startingGear.Equipment.TryGetValue(slot, out equipment))
        {
            gear = equipment;
            return true;
        }
        gear = string.Empty;
        return false;
    }
}
