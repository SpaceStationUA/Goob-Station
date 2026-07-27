// SPDX-FileCopyrightText: 2026 Pirate
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.ModularSuit;

/// <summary>
/// Enables the structural overlay used by meson MOD-suit visors.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MesonVisionComponent : Component;
