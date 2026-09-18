// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Marker for in-world TV sets that offer the Pirate TV verbs
///     ("open the viewer", "pick over the Browser"). Client-only: the
///     attaching system ensures it on television prototypes, the server
///     never sees it.
/// </summary>
[RegisterComponent]
public sealed partial class WebTvComponent : Component
{
}
