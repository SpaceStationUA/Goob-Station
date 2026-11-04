using Content.Shared.Preferences;
using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Origin.Components;

[RegisterComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), NetworkedComponent]
public sealed partial class PassportComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsClosed = true;

    [ViewVariables]
    public HumanoidCharacterProfile OwnerProfile = null!;
}
