using Content.Server._Pirate.Power.EntitySystems;
using Content.Shared._Pirate.BladeServer;

namespace Content.Server._Pirate.BladeServer;

/// <summary>
/// This system extends <see cref="SharedBladeServerSystem"/> with server-only power interactions.
/// </summary>
public sealed partial class BladeServerSystem : SharedBladeServerSystem
{
    [Dependency] private readonly InnerCableSystem _innerCable = default!;

    protected override void SetSlotPower(Entity<BladeServerRackComponent> entity, BladeSlot slot, bool powered)
    {
        base.SetSlotPower(entity, slot, powered);

        if (slot.Slot.ContainerSlot?.ID is not { } containerId)
            return;

        _innerCable.SetInnerProviderContainerConnectable(entity.Owner, containerId, powered);
    }
}
