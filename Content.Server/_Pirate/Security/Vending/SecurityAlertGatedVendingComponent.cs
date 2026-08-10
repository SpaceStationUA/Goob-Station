// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Security.Vending;

/// <summary>
/// Restricts selected vending products to configured station alert levels.
/// </summary>
[RegisterComponent]
public sealed partial class SecurityAlertGatedVendingComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> GatedItems = [];

    [DataField(required: true)]
    public HashSet<string> AllowedAlertLevels = [];
}
