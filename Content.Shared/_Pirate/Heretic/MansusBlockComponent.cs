using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Heretic.Components;

/// <summary>
/// Used to prevent using mansus grasp ability when this component is on gloves.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MansusBlockComponent : Component
{
}
