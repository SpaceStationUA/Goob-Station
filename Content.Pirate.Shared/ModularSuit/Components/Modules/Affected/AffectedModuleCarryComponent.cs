using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.ModularSuit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AffectedModuleCarryComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public float Multiplier = 0.33f;
}
