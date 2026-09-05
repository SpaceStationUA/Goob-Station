// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Grants skills once when the owning entity initializes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KnowledgeGrantComponent : Component
{
    [DataField(required: true), AlwaysPushInheritance]
    public Dictionary<EntProtoId, int> Skills = new();
}

/// <summary>
/// Teaches skills or experience when the owning item is used in hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KnowledgeGrantOnUseComponent : Component
{
    [DataField, AlwaysPushInheritance]
    public Dictionary<EntProtoId, int> Skills = new();

    [DataField, AlwaysPushInheritance]
    public Dictionary<EntProtoId, int> Experience = new();

    [DataField]
    public bool GrantEverything;

    [DataField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(5);

    [DataField]
    public bool Instant = true;

    [DataField]
    public bool SingleUse = true;

    [DataField]
    public EntProtoId Ash = "Ash";
}

/// <summary>
/// Removes mutually exclusive skills when this skill is learned.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KnowledgeConflictComponent : Component
{
    [DataField(required: true)]
    public HashSet<EntProtoId> Conflicts = new();
}
