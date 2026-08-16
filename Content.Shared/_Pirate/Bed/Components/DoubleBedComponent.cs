using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared._Pirate.Bed.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DoubleBedComponent : Component
{
    [ViewVariables]
    public Vector2? PendingBuckleOffset;

    [DataField, AutoNetworkedField]
    public Vector2 LeftOffset = new(0f, -0.15f);

    [DataField, AutoNetworkedField]
    public Vector2 RightOffset = new(0f, 0.25f);

    [DataField, AutoNetworkedField]
    public Vector2 LeftBedsheetOffset = new(0f, 0.5f);

    [DataField, AutoNetworkedField]
    public Vector2 RightBedsheetOffset = new(0f, 0f);
}
