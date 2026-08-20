using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Bed.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BedsheetCoverComponent : Component
{
    [AutoNetworkedField]
    public bool Covered;
}
