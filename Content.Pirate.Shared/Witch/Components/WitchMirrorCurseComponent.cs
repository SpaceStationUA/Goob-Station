using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WitchMirrorCurseComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 8f;
}
