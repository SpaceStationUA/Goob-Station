// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Klovnstation14.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Lawnmower;

[RegisterComponent, NetworkedComponent]
public sealed partial class KsLawnmowerComponent : Component
{
    /// <summary>
    ///     The prototype whose name and description an emagged mower takes.
    /// </summary>
    [DataField]
    public EntProtoId EmaggedPrototype = "KsLawnmowerEmagged";
}

[Serializable, NetSerializable]
public enum KsLawnmowerVisuals : byte
{
    SafetyDisabled,
    Damaged,
}
