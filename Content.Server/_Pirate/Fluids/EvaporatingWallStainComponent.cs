// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.Fluids;

/// <summary>Wall stain with water or cleaner to evaporate, or an empty stain awaiting deletion.</summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class EvaporatingWallStainComponent : Component;
