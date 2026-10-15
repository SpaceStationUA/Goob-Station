using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.IPC;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedScreenSaverSystem))]
[AutoGenerateComponentState(true)]
public sealed partial class ScreenSaverComponent : Component
{
    [DataField("action")]
    public EntProtoId ActionId = "ActionScreenSaver";

    [DataField("actionEntity"), AutoNetworkedField]
    public EntityUid? ActionEntity;
    
    [DataField("currentScreen"), AutoNetworkedField]
    public string? CurrentScreen;

    [DataField("deathScreen"), AutoNetworkedField]
    public string DeathScreen = "ScreenBsod";
}
