using Content.Shared.Doors.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Doors;

[RegisterComponent, NetworkedComponent]
public sealed partial class AirlockIlluminationComponent : Component
{
    [DataField]
    public string OpenUnlitState = "open_unlit";

    [DataField]
    public string ClosedUnlitState = "closed_unlit";
}
