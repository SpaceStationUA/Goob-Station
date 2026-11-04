// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Genetics;

/// <summary>
/// Grants the local components for a genetics status without replacing innate traits.
/// </summary>
[RegisterComponent]
public sealed partial class GeneticsComponentStatusEffectComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    /// <summary>
    /// Only components introduced by this status are removed when it ends.
    /// </summary>
    [DataField]
    public ComponentRegistry Added = new();
}
