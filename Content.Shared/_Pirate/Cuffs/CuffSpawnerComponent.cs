// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Cuffs;

/// <summary>
/// Allows a security bot to create and apply cuffs when arresting a target.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CuffSpawnerComponent : Component
{
    [DataField]
    public EntProtoId HandcuffId = "Handcuffs";
}
