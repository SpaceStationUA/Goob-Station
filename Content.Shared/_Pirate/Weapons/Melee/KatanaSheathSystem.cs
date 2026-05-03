using Content.Shared.Containers.ItemSlots;
using Content.Shared._Pirate.Weapons.Melee.Components;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Weapons.Melee;

public sealed class KatanaSheathSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatanaSheathComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KatanaSheathComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<KatanaSheathComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnMapInit(Entity<KatanaSheathComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnItemInserted(Entity<KatanaSheathComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateAppearance(ent);
    }

    private void OnItemRemoved(Entity<KatanaSheathComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<KatanaSheathComponent> ent)
    {
        if (!_itemSlots.TryGetSlot(ent, ent.Comp.Slot, out var slot) || slot.Item is not { } stored)
        {
            ClearAppearance(ent);
            return;
        }

        ResPath sprite;
        string inventoryState, beltState, backpackState;

        if (TryComp<KatanaSheathHandleComponent>(stored, out var handle))
        {
            sprite = handle.Sprite;
            inventoryState = handle.InventoryState;
            beltState = handle.BeltState;
            backpackState = handle.BackpackState;
        }
        else if (ent.Comp.FallbackSprite is { } fallbackSprite &&
                 ent.Comp.FallbackInventoryState is { } fallbackInventory &&
                 ent.Comp.FallbackBeltState is { } fallbackBelt &&
                 ent.Comp.FallbackBackpackState is { } fallbackBackpack)
        {
            sprite = fallbackSprite;
            inventoryState = fallbackInventory;
            beltState = fallbackBelt;
            backpackState = fallbackBackpack;
        }
        else
        {
            ClearAppearance(ent);
            return;
        }

        _appearance.SetData(ent, KatanaSheathVisuals.InventoryHandle, CreateLayer(sprite, inventoryState));
        _appearance.SetData(ent, KatanaSheathVisuals.BeltHandle, CreateLayer(sprite, beltState));
        _appearance.SetData(ent, KatanaSheathVisuals.BackpackHandle, CreateLayer(sprite, backpackState));
    }

    private void ClearAppearance(Entity<KatanaSheathComponent> ent)
    {
        _appearance.RemoveData(ent, KatanaSheathVisuals.InventoryHandle);
        _appearance.RemoveData(ent, KatanaSheathVisuals.BeltHandle);
        _appearance.RemoveData(ent, KatanaSheathVisuals.BackpackHandle);
    }

    private static PrototypeLayerData CreateLayer(ResPath sprite, string state)
    {
        return new PrototypeLayerData
        {
            RsiPath = sprite.ToString(),
            State = state,
        };
    }
}
