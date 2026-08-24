// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Base knowledge and customization point limit for a species.
/// </summary>
[Prototype]
public sealed partial class KnowledgeProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [IncludeDataField]
    public KnowledgeProfile Profile;

    [DataField(required: true)]
    public int PointsLimit;
}

/// <summary>
/// Display group used by the character editor and knowledge window.
/// </summary>
[Prototype]
public sealed partial class KnowledgeCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;
}

/// <summary>
/// Explicit list of skill entity prototypes. Keeping this list explicit avoids scanning every
/// entity prototype when the skill system initializes or prototypes are reloaded.
/// </summary>
[Prototype]
public sealed partial class KnowledgeCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public HashSet<EntProtoId> Entries = new();
}
