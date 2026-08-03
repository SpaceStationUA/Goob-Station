/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.ZLevels.Mapping.Prototypes;

[Prototype("zMap")]
public sealed partial class CEZLevelMapPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Resource paths for the map files that make up this z-network, ordered from bottom (depth 0) to top.
    /// Each entry is loaded into its own map entity and linked into the resulting CEZLevelsNetworkComponent.
    /// </summary>
    [DataField]
    public List<ResPath> Maps { get; private set; } = new();

    /// <summary>
    /// Shared components for all zLevels maps
    /// </summary>
    [DataField]
    public ComponentRegistry Components { get; private set; } = new();
}
