using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Silicons.IPC;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedScreenSaverSystem))]
[AutoGenerateComponentState(true)]
public sealed partial class ScreenSaverComponent : Component
{
    [DataField("action", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionId = "ActionScreenSaver";

    [DataField("actionEntity"), AutoNetworkedField]
    public EntityUid? ActionEntity;
    
    [DataField("currentScreen"), AutoNetworkedField]
    public string? CurrentScreen;

    [DataField("deathScreen"), AutoNetworkedField]
    public string DeathScreen = "ScreenBsod";
}
