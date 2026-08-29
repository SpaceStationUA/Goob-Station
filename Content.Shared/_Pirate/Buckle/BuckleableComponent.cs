// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Buckle;

/// <summary>
/// Marks a non-item entity as a valid drag-and-drop source.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BuckleableComponent : Component;
