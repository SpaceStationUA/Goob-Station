using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Visuals.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PirateClientTextureVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public PirateClientTextureVisionMode Mode = PirateClientTextureVisionMode.Xeno;

    [DataField, AutoNetworkedField]
    public float Range = 10f;
}
