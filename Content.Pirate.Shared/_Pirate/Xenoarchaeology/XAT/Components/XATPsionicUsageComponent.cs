using Content.Shared._Pirate.Xenoarchaeology.XAT;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Xenoarchaeology.XAT.Components;

/// <summary>
/// Xeno artifact trigger component that fires when a psionic power is used near the artifact node.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(XATPsionicUsageSystem)), AutoGenerateComponentState]
public sealed partial class XATPsionicUsageComponent : Component
{
    /// <summary>
    /// Radius, in which a psionic power usage counts as "nearby" and triggers the node.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 5;
}
