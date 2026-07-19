using Content.Shared.Chemistry.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Pirate.Fluids;

public sealed class SpilledOnEvent(
    EntityUid source,
    Solution solution,
    SlotFlags targetSlots = SpilledOnEvent.DefaultTargetSlots,
    bool stainHeldItems = false) : EntityEventArgs, IInventoryRelayEvent
{
    public const SlotFlags DefaultTargetSlots =
        SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.GLOVES | SlotFlags.HEAD | SlotFlags.MASK;

    public EntityUid Source = source;
    public Solution Solution = solution;
    public bool StainHeldItems = stainHeldItems;
    public bool Handled;
    public SlotFlags TargetSlots { get; } = targetSlots;
}
