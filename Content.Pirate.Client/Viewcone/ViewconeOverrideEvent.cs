// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

namespace Content.Pirate.Client.Viewcone;

/// <summary>
/// Used for dynamic situations where you should intuitively always show the occludable, like if you're pulling it.
/// </summary>
[ByRefEvent]
public record struct ViewconeOverrideEvent(bool Override = false);
