// SPDX-FileCopyrightText: 2025 Ark <189933909+ark1368@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 tonotom1 <tonotom@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ArmorPlate;

/// <summary>
/// Component for armor plates that can be inserted into compatible clothing.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ArmorPlateItemComponent : Component
{
    /// <summary>
    /// Maximum durability of this plate before destruction. Should match the destruction threshold.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public int MaxDurability = 100;

    /// <summary>
    /// Walk speed modifier applied when this plate is active in worn clothing.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float WalkSpeedModifier = 1.0f;

    /// <summary>
    /// Sprint speed modifier applied when this plate is active in worn clothing.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float SprintSpeedModifier = 1.0f;

    /// <summary>
    /// Multiplier applied when converting absorbed damage to stamina damage.
    /// </summary>
    [DataField]
    public float StaminaDamageMultiplier = 1.0f;

    /// <summary>
    /// How much damage dealt to the plate is multiplied, by damage type.
    /// </summary>
    [DataField("damageMultipliers")]
    public Dictionary<string, float> DamageMultipliers = new();

    /// <summary>
    /// Absorption effect of the plate, by damage type. Negative values increase incoming damage.
    /// </summary>
    [DataField("absorptionRatios")]
    public Dictionary<string, float> AbsorptionRatios = new();
}
