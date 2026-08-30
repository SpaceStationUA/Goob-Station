// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Knowledge;

/// <summary>
/// Tracks the employer's contribution to TemporaryLevel on the physical knowledge entity.
/// This travels with the skill and prevents repeated application from stacking the bonus.
/// Only the resulting KnowledgeComponent.TemporaryLevel needs to be networked.
/// </summary>
[RegisterComponent]
public sealed partial class EmployerKnowledgeBonusComponent : Component
{
    [DataField]
    public int Mastery;

    [DataField]
    public int Level;
}
