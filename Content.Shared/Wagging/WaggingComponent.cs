// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Wagging;

/// <summary>
/// An emoting wag for markings.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WaggingComponent : Component
{
    public const string DefaultSuffix = "Animated"; // Pirate: slime morph

    [DataField]
    public EntProtoId Action = "ActionToggleWagging";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Suffix to add to get the animated marking.
    /// </summary>
    public string Suffix = DefaultSuffix; // Pirate: slime morph

    /// <summary>
    /// Is the entity currently wagging.
    /// </summary>
    [DataField]
    public bool Wagging = false;
}
