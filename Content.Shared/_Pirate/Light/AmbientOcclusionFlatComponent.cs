// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Light;

// Directional structures use Mobs depth for sorting, but need fixture bounds for shadows.
[RegisterComponent]
public sealed partial class AmbientOcclusionFlatComponent : Component;
