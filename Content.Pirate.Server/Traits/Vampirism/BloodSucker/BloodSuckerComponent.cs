using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Traits.Vampirism.Components

{
    [RegisterComponent]
    public sealed partial class BloodSuckerComponent : Component
    {
        /// <summary>
        /// How much to succ each time we succ.
        /// </summary>
        [DataField("unitsToSucc")]
        public float UnitsToSucc = 5f;

        /// <summary>
        /// The time (in seconds) that it takes to succ an entity.
        /// </summary>
        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan Delay = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Maximum distance at which a bite can be started and continues to be valid.
        /// </summary>
        [DataField]
        public float BiteRange = 2f;

        /// <summary>
        /// How far the bloodsucker may move while drinking without interrupting the do-after.
        /// </summary>
        [DataField]
        public float MovementThreshold = 0.01f;

        /// <summary>
        /// Damage healed per unit of blood, split evenly between the damage types of each group.
        /// Values are positive amounts of damage to heal.
        /// </summary>
        [DataField]
        public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> HealGroups = new()
        {
            { "Brute", FixedPoint2.New(0.6f) },
            { "Burn", FixedPoint2.New(0.25f) },
        };

        /// <summary>
        /// Blood volume restored per unit of blood sucked.
        /// </summary>
        [DataField]
        public FixedPoint2 BloodRestorePerUnit = FixedPoint2.New(0.05f);

        /// <summary>
        /// Bleeding reduced per unit of blood sucked.
        /// </summary>
        [DataField]
        public float BleedReductionPerUnit = 0.05f;

        /// <summary>
        /// Hunger restored per unit of blood sucked.
        /// </summary>
        [DataField]
        public float HungerPerUnit = 6f;

        /// <summary>
        /// Thirst restored per unit of blood sucked.
        /// </summary>
        [DataField]
        public float ThirstPerUnit = 2f;

        /// <summary>
        /// Seconds of drunkenness the bloodsucker gains per unit of blood taken from a drunk victim.
        /// The victim's drunk status effect is copied over; no ethanol is injected into the blood.
        /// </summary>
        [DataField]
        public float DrunkPerUnit = 2f;

        /// <summary>
        /// Piercing damage dealt to the victim on a successful sip.
        /// </summary>
        [DataField]
        public FixedPoint2 PierceDamage = FixedPoint2.New(1);

        // ***INJECT WHEN SUCC***

        /// <summary>
        /// Whether to inject chems into a chemstream when we suck something.
        /// </summary>
        [DataField("injectWhenSucc")]
        public bool InjectWhenSucc = false;

        /// <summary>
        /// How many units of our injected chem to inject.
        /// </summary>
        [DataField("unitsToInject")]
        public float UnitsToInject = 5;

        /// <summary>
        /// Which reagent to inject.
        /// </summary>
        [DataField("injectReagent")]
        public string InjectReagent = "";

        /// <summary>
        /// Whether we need to web the thing up first...
        /// </summary>
        [DataField("webRequired")]
        public bool WebRequired = false;
    }
}
