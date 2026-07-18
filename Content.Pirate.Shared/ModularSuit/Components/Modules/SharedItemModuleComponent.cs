using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.ModularSuit;

[NetworkedComponent]
public abstract partial class SharedItemModuleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Module;
}
