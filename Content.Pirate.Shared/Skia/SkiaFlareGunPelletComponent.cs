// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Marker used by Skia's scream to destroy active flare pellets.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkiaFlareGunPelletComponent : Component;
