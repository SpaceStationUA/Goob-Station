// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;


namespace Content.Pirate.Shared.Genetics.Interaction;

/// <summary>
/// Increases an entity's interaction range when using the entity-entity InRangeUnobstructed check.
/// Body compatible, each part adds its reach to the user.
/// </summary>
/// <remarks>
/// Won't work for anything that manually passes coords instead of using the entity-entity check.
/// </remarks>
[RegisterComponent, NetworkedComponent, Access(typeof(ExtraReachSystem))]
[AutoGenerateComponentState]
public sealed partial class ExtraReachComponent : Component
{
    /// <summary>
    /// Number added to reach.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Bonus;
}
