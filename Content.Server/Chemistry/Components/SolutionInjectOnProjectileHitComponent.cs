// SPDX-License-Identifier: MIT

namespace Content.Server.Chemistry.Components;

/// <summary>
/// Used for projectile entities that should try to inject a
/// contained solution into a target when they hit it.
/// Targets can be excluded through <see cref="BaseSolutionInjectOnEventComponent.TargetBlacklist"/>.
/// </summary>
// Pirate - target blacklist support.
[RegisterComponent]
public sealed partial class SolutionInjectOnProjectileHitComponent : BaseSolutionInjectOnEventComponent { }
