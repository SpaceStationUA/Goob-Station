// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Knowledge;

[RegisterComponent, NetworkedComponent]
public sealed partial class AimSpeedKnowledgeComponent : Component
{
    [DataField(required: true)]
    public SkillCurve Curve = default!;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class BlockFractionKnowledgeComponent : Component
{
    [DataField(required: true)]
    public SkillCurve Curve = default!;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class InjectTimeKnowledgeComponent : Component
{
    [DataField(required: true)]
    public SkillCurve Curve = default!;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class ThrowInsertKnowledgeComponent : Component
{
    [DataField(required: true)]
    public SkillCurve Curve = default!;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExperienceOnCookingComponent : Component
{
    [DataField]
    public int Scale = 100;

    [DataField]
    public HashSet<EntProtoId> Cooked = new();

    [DataField, AutoNetworkedField]
    public int Limit;
}
