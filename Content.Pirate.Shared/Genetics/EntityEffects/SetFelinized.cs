// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;

namespace Content.Pirate.Shared.Genetics.EntityEffects;

/// <summary>
/// Pirate: adapts Trauma's external cat organs to local body slots and humanoid markings.
/// </summary>
public sealed partial class SetFelinized : EntityEffectBase<SetFelinized>
{
    [DataField]
    public bool Revert;
}
