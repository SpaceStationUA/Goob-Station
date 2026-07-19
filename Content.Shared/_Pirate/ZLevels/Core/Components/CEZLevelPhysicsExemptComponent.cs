using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Exempts an entity from automatic z-level physics.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZLevelPhysicsExemptComponent : Component;
