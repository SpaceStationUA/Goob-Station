using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent, NetworkedComponent]
public sealed partial class ModularSuitActionHolderComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<EntProtoId, EntityUid> ModuleActions = new();
}
