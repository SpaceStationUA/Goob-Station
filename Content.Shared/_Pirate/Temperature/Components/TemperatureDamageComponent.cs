// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared.Temperature.Components;

/// <summary>
/// Pirate - compatibility component for Starlight prototypes that split temperature damage
/// thresholds out of TemperatureComponent.
/// </summary>
[RegisterComponent]
public sealed partial class TemperatureDamageComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HeatDamageThreshold = 360f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ColdDamageThreshold = 260f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float? ParentHeatDamageThreshold;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float? ParentColdDamageThreshold;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier ColdDamage = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier HeatDamage = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 DamageCap = FixedPoint2.New(8);

    [DataField]
    public bool TakingDamage;

    [DataField]
    public ProtoId<AlertPrototype> HotAlert = "Hot";

    [DataField]
    public ProtoId<AlertPrototype> ColdAlert = "Cold";

    [DataField]
    public bool DisableAlerts;
}
