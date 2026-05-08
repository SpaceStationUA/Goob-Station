using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Traits.Trainability
{
    /// <summary> 
    /// Represents a single unit of training progress to be processed. 
    /// </summary> 
    [DataDefinition]
    public sealed partial class TechnicalStrain
    {
        [DataField("damage")]
        public DamageSpecifier Damage { get; set; } = new();

        [DataField("defense")]
        public FixedPoint2 Defense { get; set; } = new FixedPoint2();

        [DataField("stamina")]
        public float Stamina { get; set; }

        [DataField("effectiveness")]
        public float effectivenes;
    }

    /// <summary> 
    /// Tracks and processes physical training progress for an entity. 
    /// </summary> 
    [RegisterComponent, NetworkedComponent]
    public sealed partial class TrainabilityComponent : Component
    {
        #region Technical
        [DataField("technicalTrainingEfficiency"), ViewVariables(VVAccess.ReadWrite)]
        public float TechnicalTrainingEfficiency = 1.5f;

        [DataField("strains")]
        public List<TechnicalStrain> TechnicalStrains = new();

//Damage
        [DataField("damageBonus")]
        public DamageSpecifier DamageBonus = new();

        [DataField("maxDamageBonus"), ViewVariables(VVAccess.ReadWrite)]
        public float MaxDamageBonus = 5;

        [DataField("damageRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 DamageRisingSpeed = 0.02f;

//Defense
        [DataField("defenseRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 DefenseRisingSpeed = 0.02f;

        [DataField("defenseBonus")]
        public FixedPoint2 DefenseBonus = new();

        [DataField("maxDefenseBonus"), ViewVariables(VVAccess.ReadWrite)]
        public float MaxDefenseBonus = 5f;

//Stamina and Sprint
        [DataField("staminaRisingSpeed"), ViewVariables(VVAccess.ReadWrite)]
        public float StaminaRisingSpeed = 0.2f;

        [DataField("maxStamina")]
        public float MaxStaminaBonus = 200;

        [DataField("staminaBonus")]
        public float StaminaBonus = 0;

        [DataField("sprintInterval"), ViewVariables(VVAccess.ReadWrite)]
        public float SprintInterval = 2;

        public float SprintTimer;

        public float CurrentStaminaBonus = 0;
        #endregion

        #region Physical
        [DataField("physicalTrainingEfficiency"), ViewVariables(VVAccess.ReadWrite)]
        public float PhysicalTrainingEfficiency = 0.01f;

        [DataField("physicalStrain")]
        public List<float> PhysicalStrains = new List<float>();

        [DataField("hungerCost"), ViewVariables(VVAccess.ReadWrite)]
        public float ProteinsCost = 1f;

        [DataField("muscleMass"), ViewVariables(VVAccess.ReadWrite)]
        public float MuscleMass = 0f;

        [DataField("maxMuscleMass"), ViewVariables(VVAccess.ReadWrite)]
        public float MaxMuscleMass = 1f;

//Push-Ups
        [DataField("pushUpsEfficiency"), ViewVariables(VVAccess.ReadWrite)]
        public float PushUpsEfficiency = 0.1f;

        [DataField("pushUpWindow")]
        public float PushUpWindow = 0.2f;

        public TimeSpan LastStandTime;
        #endregion

        #region Rest
        [DataField("timeForRest"), ViewVariables(VVAccess.ReadWrite)]
        public float TimeForRest = 90f;

        public TimeSpan EndRestTime;
        public bool IsResting;
        public TimeSpan NextStrainTime;
        #endregion

        #region Strain
        [DataField("maxStrainsNumber"), ViewVariables(VVAccess.ReadWrite)]
        public float MaxStrainsNumber = 150;

        [DataField("strainsApplyingDelay"), ViewVariables(VVAccess.ReadWrite)]
        public float StrainsApplyingDelay = 0.1f;
        #endregion
    }
}
