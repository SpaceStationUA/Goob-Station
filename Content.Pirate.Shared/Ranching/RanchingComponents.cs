// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.Inventory;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Nutrition.Components;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.Ranching;

[RegisterComponent]
public sealed partial class HappinessComponent : Component
{
    [DataField]
    public float Current = 20f;

    [DataField]
    public float Minimum = -1000f;

    [DataField]
    public float Maximum = 1000f;

    [DataField]
    public float RegenerationRate = -0.1f;

    [DataField]
    public float HappinessIncrease = 1f;

    [DataField]
    public float DamageDecrease = -10f;

    [DataField]
    public TimeSpan NextUpdate;
}

[RegisterComponent]
public sealed partial class AddComponentOnHappyComponent : Component
{
    [DataField]
    public float HappinessRequired = 777f;

    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

[RegisterComponent]
public sealed partial class ReplaceOnUnhappyComponent : Component
{
    [DataField]
    public float HappinessRequired = -777f;

    [DataField(required: true)]
    public EntProtoId Ent;
}

[RegisterComponent]
public sealed partial class UnhappyWhenCrowdedComponent : Component
{
    [DataField]
    public int MinEntities = 6;

    [DataField]
    public float Range = 5f;

    [DataField]
    public float HappinessToDecrease = -5f;

    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag;

    [DataField]
    public TimeSpan UpdateFrequency = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan NextUpdate;
}

[RegisterComponent]
public sealed partial class FavoriteFoodComponent : Component
{
    [DataField]
    public HashSet<ProtoId<TagPrototype>> Tag = new();

    [DataField]
    public int Amount = 30;
}

[RegisterComponent]
public sealed partial class MostRecentlyEatenFoodTagsComponent : Component
{
    [DataField]
    public HashSet<ProtoId<TagPrototype>> Tag = new();
}

[RegisterComponent]
public sealed partial class VomitCounterComponent : Component
{
    [DataField]
    public int TimesVomited;

    [DataField]
    public int NeededVomits = 1;
}

[RegisterComponent]
public sealed partial class VomitedEnoughMarkerComponent : Component;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RanchingEggLayerComponent : Component
{
    [DataField]
    public EntProtoId? EggSpawn;

    [DataField]
    public SoundSpecifier EggLaySound = new SoundPathSpecifier("/Audio/Effects/pop.ogg");

    [DataField]
    public float EggLayCooldownMin = 15f;

    [DataField]
    public float EggLayCooldownMax = 40f;

    [DataField]
    public float HungerUsage = 10f;

    [DataField]
    public HungerThreshold HungerThresholdRequired = HungerThreshold.Okay;

    [DataField]
    public bool HungerRequired = true;

    [DataField, AutoPausedField]
    public TimeSpan NextGrowth;

    [DataField]
    public string Solution = "bloodstream";
}

[RegisterComponent]
public sealed partial class EggFertilizationTargetComponent : Component;

[RegisterComponent]
public sealed partial class EggFertilizerComponent : Component
{
    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(15);

    [DataField]
    public EntProtoId? SpecialReplacement;

    [DataField]
    public EntProtoId? SpecialReplacementRequiredEgg;

    [DataField]
    public Dictionary<ProtoId<TagPrototype>, EntProtoId> SpecialReplacementsByFoodTag = new();
}

[RegisterComponent]
public sealed partial class RanchingHatchableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Entity;

    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(60);
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveRanchingHatchComponent : Component
{
    [DataField, AutoPausedField]
    public TimeSpan HatchAt;
}

[RegisterComponent]
public sealed partial class EggIncubatorComponent : Component
{
    [DataField]
    public string Slot = "egg-slot";
}

[Serializable, NetSerializable]
public enum EggIncubatorVisuals : byte
{
    Egg,
}

[RegisterComponent]
public sealed partial class RaptorEggComponent : Component;

[RegisterComponent]
public sealed partial class RanchingTriggerOnFullyEatenComponent : Component;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class AnimalAgeingComponent : Component
{
    [DataField]
    public int AdultHoodYear = 15;

    [DataField]
    public int SeniorHoodYear = 30;

    [DataField]
    public int DeathYear = 35;

    [DataField]
    public int YearsOld;

    [DataField]
    public float AgeTimeMin = 15f;

    [DataField]
    public float AgeTimeMax = 30f;

    [DataField]
    public int YearsPerUpdate = 1;

    [DataField]
    public AnimalAgeState CurrentAgeState = AnimalAgeState.Baby;

    [DataField, AutoPausedField]
    public TimeSpan NextAgeTime;
}

[RegisterComponent]
public sealed partial class AgelessComponent : Component;

[Serializable, NetSerializable]
public enum AnimalAgeState : byte
{
    Baby,
    Adult,
    Senior,
}

[RegisterComponent]
public sealed partial class SpawnEntityOnAgeUpComponent : Component
{
    [DataField]
    public List<EntProtoId> EntToSpawn = new();

    [DataField]
    public AnimalAgeState AgeToChangeAt = AnimalAgeState.Adult;
}

[RegisterComponent]
public sealed partial class SpawnEntityOnOldAgeDeathComponent : Component
{
    [DataField]
    public EntProtoId HappyDeathEnt;

    [DataField]
    public EntProtoId SadDeathEnt;

    [DataField]
    public float HappinessRequired = 30f;

    [DataField]
    public float UnHappinessRequired;
}

[RegisterComponent]
public sealed partial class TameableComponent : Component
{
    [DataField]
    public int MinPetsRequired = 10;

    [DataField]
    public int MaxPetsRequired = 20;

    [DataField]
    public int PetsRequired;

    [DataField]
    public int Pets;

    [DataField]
    public bool ClearFactions = true;

    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "RaptorTamed";
}

[RegisterComponent]
public sealed partial class PolymorphOnItemsGivenComponent : Component
{
    [DataField]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public List<EntProtoId> ReplacementEntities = new();

    [DataField]
    public int Amount;
}

[RegisterComponent]
public sealed partial class ReplaceOnItemEquippedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Ent;

    [DataField]
    public HashSet<ProtoId<TagPrototype>> RequiredTags = new();

    [DataField]
    public SlotFlags Slots = SlotFlags.MASK;
}

[RegisterComponent]
public sealed partial class PlateableChickenComponent : Component;

[RegisterComponent]
public sealed partial class PlateableChickenOreComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

[RegisterComponent]
public sealed partial class ChickenChestComponent : Component;

[RegisterComponent]
public sealed partial class DealDamageOnPulledComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            ["Blunt"] = 30,
        },
    };
}

[RegisterComponent]
public sealed partial class BounceableComponent : Component
{
    [DataField]
    public int TimesBounced;

    [DataField]
    public int BouncesRequired = 9;

    [DataField]
    public TimeSpan GraceTime = TimeSpan.FromSeconds(2);

    [DataField]
    public EntityEffect[] Effects = Array.Empty<EntityEffect>();

    [DataField(required: true)]
    public EntProtoId EntityToSpawn;

    [DataField]
    public TimeSpan NextValidBounceTime;

    [DataField]
    public SoundSpecifier BounceSound = new SoundPathSpecifier("/Audio/Effects/Footsteps/bounce.ogg");
}

[RegisterComponent]
public sealed partial class ChangeDamageModiferSetStatusEffectComponent : Component
{
    [DataField]
    public ProtoId<DamageModifierSetPrototype> DamageModifierSet = "DevilDealNegative";

    [DataField]
    public ProtoId<DamageModifierSetPrototype>? OriginalDamageModifierSet;

    [DataField]
    public bool GoToOriginalOnRemove = true;
}

[RegisterComponent]
public sealed partial class ShrunkStatusEffectComponent : Component
{
    [DataField]
    public Vector2 OriginalSize = Vector2.One;
}

[RegisterComponent]
public sealed partial class StatusEffectEffectsComponent : Component
{
    [DataField(required: true)]
    public EntityEffect[] Effects = Array.Empty<EntityEffect>();

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate;
}

[RegisterComponent]
public sealed partial class StatusEffectEffectsApplyComponent : Component
{
    [DataField]
    public EntityEffect[]? EffectsOnApply;

    [DataField]
    public EntityEffect[]? EffectsOnRemoval;
}

[RegisterComponent]
public sealed partial class TemporaryActionGrantEffectComponent : Component
{
    [DataField]
    public List<EntityUid> Actions = new();

    [DataField(required: true)]
    public List<EntProtoId> ActionPrototypes = new();
}

[RegisterComponent]
public sealed partial class AddShaderStatusEffectComponent : Component
{
    [DataField(required: true)]
    public string Shader = string.Empty;
}
