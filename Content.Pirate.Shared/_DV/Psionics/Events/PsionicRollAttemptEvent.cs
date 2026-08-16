using Content.Shared.Inventory;

namespace Content.Shared._DV.Psionics.Events;

/// <summary>
/// Raised on an entity before it can receive a rolled psionic power.
/// </summary>
[ByRefEvent]
public record struct PsionicRollAttemptEvent() : IInventoryRelayEvent
{
    public bool CanRoll = true;

    SlotFlags IInventoryRelayEvent.TargetSlots => SlotFlags.WITHOUT_POCKET;
}
