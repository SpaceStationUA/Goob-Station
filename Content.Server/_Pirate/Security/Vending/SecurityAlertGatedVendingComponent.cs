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

    [DataField]
    public List<EntProtoId> ElevatedAlertItems = [];

    [DataField]
    public HashSet<string> ElevatedAlertLevels = [];

    [DataField]
    public List<SecurityAlertVendingGate> Gates = [];
}

[DataDefinition]
public sealed partial class SecurityAlertVendingGate
{
    [DataField(required: true)]
    public List<EntProtoId> Items = [];

    [DataField(required: true)]
    public HashSet<string> Levels = [];

    [DataField(required: true)]
    public LocId DenialMessage;
}
