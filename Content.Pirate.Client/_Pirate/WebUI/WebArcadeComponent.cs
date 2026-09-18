// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Marker for in-world arcade machines running the Pirate WebArcade
///     games (CEF pages served from res://). Client-only: the attaching
///     system ensures it on arcade-machine prototypes (ID contains
///     "Arcade"), the server never sees it.
/// </summary>
[RegisterComponent]
public sealed partial class WebArcadeComponent : Component
{
}
