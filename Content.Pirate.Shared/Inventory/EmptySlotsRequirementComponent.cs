using Content.Shared.Inventory;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Inventory;

[Access(typeof(EmptySlotsRequirementSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmptySlotsRequirementComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist = default!;
}
