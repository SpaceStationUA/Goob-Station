using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Light.Components;
// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Pirate.Shared.Genetics.Damage;

/// <summary>
/// Modifies incoming damage from any source.
/// Does nothing to healing :)
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(FragileSystem))]
public sealed partial class FragileComponent : Component
{
    /// <summary>
    /// Modifier applied to incoming damage.
    /// </summary>
    [DataField]
    public float Modifier = 20000f;
}
