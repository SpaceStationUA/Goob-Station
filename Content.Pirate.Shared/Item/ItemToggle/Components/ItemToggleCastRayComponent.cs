// SPDX-FileCopyrightText: 2025 MarkerWicker <markerWicker@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Item.ItemToggle.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemToggleCastRayComponent : Component
{
    /// <summary>
    /// Component types which receive an <see cref="ItemToggleRayHitEvent"/> when intersected by the ray.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry RaiseEventAt = default!;

    /// <summary>
    /// Maximum ray length in meters.
    /// </summary>
    [DataField]
    public float RayLength = 4f;

    /// <summary>
    /// Angular offset from the item's facing direction.
    /// </summary>
    [DataField]
    public double RayOffsetDegrees = -90;
}

/// <summary>
/// Raised on an entity matched and intersected by an item toggle ray.
/// </summary>
[ByRefEvent]
public readonly record struct ItemToggleRayHitEvent;
