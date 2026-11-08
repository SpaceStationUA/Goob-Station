using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.BountyHunter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BountyHunterMirrorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Used;
}
