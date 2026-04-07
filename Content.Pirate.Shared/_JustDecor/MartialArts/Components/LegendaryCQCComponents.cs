using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared._JustDecor.MartialArts.Components;

/// <summary>
/// Grants Legendary CQC knowledge from entities such as manuals.
/// </summary>
[RegisterComponent]
public sealed partial class GrantLegendaryCQCComponent : GrantMartialArtKnowledgeComponent
{
    [DataField]
    public override MartialArtsForms MartialArtsForm { get; set; } = MartialArtsForms.LegendaryCloseQuartersCombat;

    public override LocId? LearnMessage { get; set; } = "legendary-cqc-success-learned";
}

/// <summary>
/// Stores per-ability cooldowns for Legendary CQC.
/// </summary>
[RegisterComponent]
public sealed partial class LegendaryCQCCooldownsComponent : Component
{
    [DataField]
    public Dictionary<string, TimeSpan> CooldownTimers = new();
}

/// <summary>
/// Tracks the temporary state attached to a Legendary CQC user.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LegendaryCQCKnowledgeComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public int CombosPerformed = 0;

    [DataField]
    public TimeSpan LastComboTime;

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public bool CombatMode = false;

    [DataField]
    public TimeSpan CombatModeDuration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CombatModeEndTime;

    [DataField]
    public TimeSpan LastMoveTime;

    [DataField]
    public float MinCounterVelocitySquared = 0.25f;

    [DataField]
    public TimeSpan CounterStanceDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan CounterStanceDuration = TimeSpan.FromSeconds(5);

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public int CurrentComboChain = 0;

    [DataField]
    public int MaxComboChain = 3;

    [DataField]
    public TimeSpan ComboChainTimeout = TimeSpan.FromSeconds(2);

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float HasteMeter = 0f;

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float MaxHasteBonus = 0.8f;

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float HasteDecayRate = 0.1f;

    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public float? OriginalAttackRate;
}

/// <summary>
/// Temporary speed buff gained from the rush combo.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LegendaryCQCRushBuffComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.3f;
}

/// <summary>
/// Temporary counter window that reduces incoming damage.
/// </summary>
[RegisterComponent]
public sealed partial class LegendaryCQCCounterBuffComponent : Component
{
    [DataField]
    public TimeSpan EndTime;

    [DataField]
    public float DamageReduction = 0.5f;

    [DataField]
    public float ReflectDamage = 0.2f;

    [DataField]
    public SoundSpecifier? CounterSound;
}

/// <summary>
/// Marks an entity that is currently being choked.
/// </summary>
[RegisterComponent]
public sealed partial class InChokeholdComponent : Component
{
    [DataField]
    public EntityUid Choker;

    [DataField]
    public float OxygenDamageRate = 2f;

    [DataField]
    public TimeSpan LastDamageTick;
}
