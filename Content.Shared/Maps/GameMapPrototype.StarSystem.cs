// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared.Maps;

public sealed partial class GameMapPrototype
{
    /// <summary>
    ///     If true (default), a procedurally generated star system will be rendered behind this map
    ///     and the station will be placed in orbit of one of the planets.
    ///     Disable on non-space maps (e.g. planet-orbit maps like Glavier) with `generateStarSystem: false`.
    /// </summary>
    [DataField("generateStarSystem")]
    public bool GenerateStarSystem = true;
}
