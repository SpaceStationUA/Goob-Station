using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Yautja.Components;

/// <summary>
/// Marks Yautja weapons that instantly break resin structures on melee hit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaResinBreakerComponent : Component;
