using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent, NetworkedComponent]
public sealed partial class ModularSuitCarrierComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public string? CurrentSlot = default!;
}
