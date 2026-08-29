// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// One independently levelled skill, stored as an entity inside a knowledge container.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), EntityCategory("Knowledge")]
public sealed partial class KnowledgeComponent : Component
{
    [DataField(required: true)]
    public ProtoId<KnowledgeCategoryPrototype> Category;

    [DataField(required: true), AutoNetworkedField]
    public int LearnedLevel;

    [DataField, AutoNetworkedField]
    public int Experience;

    [DataField]
    public int ExperienceCost = 1;

    [DataField]
    public bool Unremoveable;

    [DataField]
    public bool Hidden;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public SpriteSpecifier? Sprite;

    [DataField, AutoNetworkedField]
    public int TemporaryLevel;

    [ViewVariables]
    public int NetLevel => Math.Clamp(LearnedLevel + TemporaryLevel, 0, 100);

    [DataField]
    public int BonusExperience;

    [DataField]
    public TimeSpan TimeToNextExperience;

    [DataField]
    public TimeSpan TimeBetweenExperience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cumulative character-point costs by mastery. Null makes a skill unavailable at round start.
    /// </summary>
    [DataField(required: true)]
    public int[]? Costs;

    /// <summary>
    /// Complex knowledge can only be granted by a profile, job, item, or other explicit source.
    /// </summary>
    [DataField]
    public bool Complex;
}

/// <summary>
/// Points from a mob to the brain or other entity that physically stores its knowledge.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KnowledgeHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? KnowledgeEntity;
}

/// <summary>
/// Owns the hidden knowledge entities, normally on a brain.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class KnowledgeContainerComponent : Component
{
    public const string ContainerId = "knowledge";

    [ViewVariables]
    public Container? Container;

    [DataField, AutoNetworkedField]
    public EntityUid? Holder;

    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> Knowledge = new();
}
