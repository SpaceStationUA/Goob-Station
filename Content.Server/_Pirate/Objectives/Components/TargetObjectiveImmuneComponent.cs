// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Pirate.Objectives.Components;

/// <summary>
/// Prevents an entity or mind from being selected by target objectives that honor this marker.
/// </summary>
[RegisterComponent]
public sealed partial class TargetObjectiveImmuneComponent : Component;
