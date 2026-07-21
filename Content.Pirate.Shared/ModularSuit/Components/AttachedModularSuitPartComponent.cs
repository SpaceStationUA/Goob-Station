using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.ModularSuit;

[Access(typeof(SharedModularSuitSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AttachedModularSuitPartComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? Suit;
}
