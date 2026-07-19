// SPDX-FileCopyrightText: 2026 kotobdev <59124164+kotobdev@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Avali.Components;

/// <summary>
/// Marks an item as a feather with owner-specific colors.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FeatherComponent : Component;

/// <summary>
/// Appearance data used to color feathers and their blood overlay.
/// </summary>
[Serializable, NetSerializable]
public enum FeatherVisuals : byte
{
    FeatherColor,
    BloodColor,
}
