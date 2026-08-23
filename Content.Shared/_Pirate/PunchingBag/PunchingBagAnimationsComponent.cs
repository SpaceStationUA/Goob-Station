using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.PunchingBag;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PunchingBagAnimationsComponent : Component
{
    [DataField, AutoNetworkedField]
    public string AnimationState = "swinging";
}

