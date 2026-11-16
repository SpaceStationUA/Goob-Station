// Pirate: ported from ProjectOmu/OmuStation@94c6cb84bac71924f9e114c811a8d1a0ac859a3f.
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Xelthia;

/// <summary>
/// Stores the Xelthia arm-regrowth action and its cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XelthiaComponent : Component
{
    [DataField]
    public TimeSpan UseDelay { get; set; } = TimeSpan.FromMinutes(5);

    [DataField]
    public EntityUid? XelthiaRegenerateAction;
}
