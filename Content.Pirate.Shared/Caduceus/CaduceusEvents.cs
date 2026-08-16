// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Pirate.Shared.Caduceus;

/// <summary>
///     Raised when the Caduceus' "swap" action is used: instantly shifts into a random other form.
/// </summary>
public sealed partial class CaduceusSwapActionEvent : InstantActionEvent;

/// <summary>
///     Raised when the Caduceus' "hold" action is used: doubles the remaining hits for the current
///     form (capped at 2x its max) and grants +1 KARMIC CONSEQUENCE.
/// </summary>
public sealed partial class CaduceusHoldActionEvent : InstantActionEvent;
