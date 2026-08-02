// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

[RegisterComponent, NetworkedComponent]
public sealed partial class SkiaBreakLightsOnSpawnComponent : Component
{
    [DataField]
    public float Radius = 10f;

    [DataField]
    public bool LineOfSight;
}
