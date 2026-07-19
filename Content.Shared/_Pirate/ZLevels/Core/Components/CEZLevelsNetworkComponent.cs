/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Tracker that tracks all maps added to the zLevel network. Usually, entity in Nullspace,
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CESharedZLevelsSystem))]
public sealed partial class CEZLevelsNetworkComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid?> ZLevels = new();

    /// <summary>
    /// Reverse-lookup table for <see cref="ZLevels"/>. O(1) map→depth instead of scanning values.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntityUid, int> ZLevelByEntity = new();

    /// <summary>
    /// Maps stored densely from <see cref="SortedMin"/> to <see cref="SortedMax"/>.
    /// Gaps are represented by <see cref="EntityUid.Invalid"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> SortedZLevels = new();

    [ViewVariables, AutoNetworkedField]
    public int SortedMin;

    [ViewVariables, AutoNetworkedField]
    public int SortedMax;

    /// <summary>
    /// Shared components for all zLevels maps
    /// </summary>
    [DataField(serverOnly: true)]
    public ComponentRegistry Components = new();
}
