// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.EntityEffects;
using Content.Shared.Materials;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Durability;

/// <summary>
/// Allows an item to wear down through use and be repaired.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(DurabilitySystem))]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class DurabilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public DurabilityState DurabilityState = DurabilityState.Pristine;

    [DataField]
    public SortedDictionary<FixedPoint2, DurabilityState> DurabilityThresholds = [];

    [DataField, AutoNetworkedField]
    public FixedPoint2 DurabilityScale = 1;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Damage;

    [DataField, AutoNetworkedField]
    public float DamageProbability = 0.35f;

    [DataField]
    public SortedDictionary<DurabilityState, HashSet<LocId>> DamagePopups = [];

    [DataField]
    public EntityEffect[]? OnBreakEffects;

    [DataField, AutoNetworkedField]
    public bool DeleteOnDestroyed = true;

    [DataField]
    public LocId? DestroyedSwingAttemptPopup = new("durability-attempt-melee-destroyed");

    [DataField, AutoNetworkedField]
    public FixedPoint2 MinDamageRoll = 3;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxDamageRoll = 6;

    [DataField]
    public SortedDictionary<DurabilityState, float> DurabilityModifiers = new()
    {
        [DurabilityState.Worn] = 0.9f,
        [DurabilityState.Damaged] = 0.7f,
        [DurabilityState.Broken] = 0.45f,
    };

    [DataField, AutoNetworkedField]
    public Dictionary<DurabilityState, float> CustomDurabilityModifiers = [];

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<MaterialPrototype>, Vector2> RepairMaterials = [];

    [DataField, AutoNetworkedField]
    public ProtoId<ToolQualityPrototype>? RepairTool;

    [DataField, AutoNetworkedField]
    public Vector2 ToolRepairAmount;

    [DataField, AutoNetworkedField]
    public float FuelCost;

    [DataField, AutoNetworkedField]
    public TimeSpan RepairDoAfter;

    [DataField, AutoNetworkedField]
    public bool Repairable = true;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxRepairBonus;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CustomDurabilityModifierComponent : Component
{
    [DataField(required: true)]
    public Dictionary<DurabilityState, Vector2> MaxDurabilityStateModifiers = [];
}

[Serializable, NetSerializable]
public enum DurabilityState : sbyte
{
    Reinforced = -1,
    Pristine = 0,
    Worn = 1,
    Damaged = 2,
    Broken = 3,
    Destroyed = 4,
}
