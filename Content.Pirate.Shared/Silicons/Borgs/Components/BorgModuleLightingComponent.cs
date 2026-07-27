using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Silicons.Borgs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BorgModuleLightingComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color LightColor = Color.White;

    [DataField, AutoNetworkedField]
    public bool DiscoMode;

    [DataField, AutoNetworkedField]
    public float CycleRate = 0.1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BorgLightingInstalledComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ModuleEntity;

    [DataField, AutoNetworkedField]
    public Color CurrentColor = Color.White;

    [DataField, AutoNetworkedField]
    public bool DiscoMode;

    [DataField, AutoNetworkedField]
    public float CycleRate;
}
