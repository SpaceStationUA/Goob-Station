using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using System.Collections.Generic;
namespace Content.Shared._Pirate.Weapons.Melee.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class TimedDeflectBlockComponent : Component
{
    [DataField]
    public float DeflectWindow = 0.5f;

    [DataField]
    public float BlockStaminaDamageFraction = 0.075f;

    [DataField]
    public SoundSpecifier DeflectSound = new SoundCollectionSpecifier("PirateBladeDeflects");

    [DataField]
    public SoundSpecifier BlockSound = new SoundCollectionSpecifier("PirateBladeBlocks");

    [DataField]
    public float PowerGainOnDeflect = 8f;

    [DataField]
    public float PowerLossOnMeleeHit = 4f;

    [DataField]
    public float PowerDecayDelay = 30f;

    [DataField]
    public float PowerDecayPerSecond = 0.5f;

    [DataField]
    public float MinPower;

    [DataField]
    public float MaxPower = 100f;

    [DataField]
    public float PowerPerLevel = 15f;

    [DataField]
    public int MaxLevel = 6;

    /// <summary>
    /// Bonus damage added per power level, supports multiple types.
    /// e.g. { "Slash": 1.5, "Holy": 1.5 } adds 1.5 Slash and 1.5 Holy damage per level.
    /// </summary>
    [DataField]
    public Dictionary<string, float> BonusDamagePerLevel = new() { ["Slash"] = 3f };

    [DataField]
    public float BlockStaminaDamageReductionPerLevel = 0.005f;

    [DataField]
    public float BlockStaminaMultiplier = 2;

    [DataField]
    public float DeflectStaminaMultiplier = 1.2f;

    [DataField]
    public float StaminaReferenceDamage = 12f;

    [DataField]
    public float DeflectWindowBonusPerLevel = 0.09f;

    [DataField]
    public float DeflectLagCompensationMultiplier = 1.5f;

    [DataField]
    public float MaxDeflectLagCompensation = 0.75f;

    [DataField]
    public float BlockActivationLagCompensationMultiplier = 0.5f;

    [DataField]
    public float MaxBlockActivationLagCompensation = 0.2f;

    [DataField]
    public bool DeflectToSource;

    [DataField]
    public float BackflipChance = 0.15f;

    [DataField]
    public string BaseVisualState = "dormant";

    [DataField]
    public string LevelVisualStatePrefix = "level-";

    [AutoNetworkedField]
    public float CurrentPower;

    [AutoNetworkedField]
    public TimeSpan DeflectWindowStart = TimeSpan.Zero;

    [AutoNetworkedField]
    public TimeSpan DeflectWindowEnd = TimeSpan.Zero;

    public TimeSpan LastDeflectTime = TimeSpan.Zero;

    /// <summary>
    /// Scales the effective time counted toward <see cref="PowerDecayDelay"/> while sheathed.
    /// 0.25 = the delay window is 4× larger (sheathed time counts at 25%).
    /// </summary>
    [DataField]
    public float SheathWindowMultiplier = 0.25f;

    /// <summary>
    /// Scales <see cref="PowerDecayPerSecond"/> while the weapon is inside a
    /// <see cref="SheathPreservesEnergyComponent"/> container.
    /// 0.5 = decay is 2× slower than normal.
    /// </summary>
    [DataField]
    public float SheathDecayMultiplier = 0.5f;

    // Runtime sheath-tracking — not networked, server only.
    public bool IsSheathed;
    public TimeSpan SheathStartTime;
}
