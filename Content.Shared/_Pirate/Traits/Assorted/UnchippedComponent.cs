using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Traits.Assorted;

/// <summary>
/// Prevents brain skill chips from being installed in this character.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnchippedComponent : Component
{
}
