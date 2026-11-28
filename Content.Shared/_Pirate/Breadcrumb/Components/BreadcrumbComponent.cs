// SPDX-FileCopyrightText: 2017-2026 Space Wizards Federation
// SPDX-FileCopyrightText: 2024-2026 Starlight
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Breadcrumb.Components;

/// <summary>
/// A breadcrumb dropping is like a pebble on the floor that glows a certain color.
/// It is dropped by a <see cref="BreadcrumbDropperComponent"/>. See that type for more info.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BreadcrumbComponent : Component
{
    /// <summary>
    /// The dropper that made this dropping.
    /// </summary>
    [AutoNetworkedField] public EntityUid Dropper = EntityUid.Invalid;

    /// <summary>
    /// The color of this dropping.
    /// </summary>
    [DataField, AutoNetworkedField] public Color Color = Color.White;
}

[Serializable, NetSerializable]
public enum BreadcrumbVisualLayers
{
    UnlitColor,
    LitColor
}

[Serializable, NetSerializable]
public enum BreadcrumbVisualData
{
    Color
}
