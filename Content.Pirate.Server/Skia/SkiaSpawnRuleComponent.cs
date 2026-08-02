// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;

namespace Content.Pirate.Server.Skia;

[RegisterComponent]
public sealed partial class SkiaSpawnRuleComponent : Component
{
    [ViewVariables]
    public List<MapCoordinates>? Coordinates;
}
