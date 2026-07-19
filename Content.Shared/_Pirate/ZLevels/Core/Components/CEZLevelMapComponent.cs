/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.ZLevels.Core.Components;

/// <summary>
/// Automatically added to the map when it appears in zLevelNetwork.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class CEZLevelMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Depth;

    /// <summary>
    /// Network this map belongs to. Cached here so map→network lookups are O(1)
    /// instead of enumerating every <see cref="CEZLevelsNetworkComponent"/>.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid NetworkUid;

    /// <summary>
    /// Cached direct neighbour above (depth + 1). Null if no such map exists.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapAbove;

    /// <summary>
    /// Cached direct neighbour below (depth - 1). Null if no such map exists.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? MapBelow;
}
