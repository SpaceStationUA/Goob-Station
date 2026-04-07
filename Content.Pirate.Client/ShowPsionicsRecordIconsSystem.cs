/// <summary>
/// Pirate psionics HUD icons, adapted from the criminal records HUD system.
/// Shows psionics record icons from ID cards in inventory.
/// </summary>

using Content.Shared.Access.Components;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Psionics.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed class ShowPsionicsRecordIconsSystem : EquipmentHudSystem<ShowPsionicsRecordIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to entities with inventory to check their ID cards
        SubscribeLocalEvent<InventoryComponent, GetStatusIconsEvent>(OnInventoryGetStatusIconsEvent);
    }

    private void OnInventoryGetStatusIconsEvent(EntityUid uid, InventoryComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        // Try to find ID card in inventory
        if (!_inventory.TryGetSlotEntity(uid, "id", out var idUid))
            return;

        // Check if it's a PDA with ID inside
        if (TryComp<PdaComponent>(idUid, out var pda) && pda.ContainedId is { } containedId)
        {
            idUid = containedId;
        }

        // Check if the ID card has a psionics record component
        if (!TryComp<PsionicsRecordComponent>(idUid, out var record))
            return;

        // Add the icon from the ID card
        if (_prototype.TryIndex(record.StatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}
