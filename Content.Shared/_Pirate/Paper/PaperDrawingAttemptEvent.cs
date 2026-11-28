// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Paper;

/// <summary>
/// Pirate: raised on a sheet of paper when a crayon is used on it, before the writing UI
/// would open. A handler that turns the sheet into a drawing surface sets <see cref="Handled"/>,
/// which consumes the interaction instead of starting to write.
/// </summary>
[ByRefEvent]
public record struct PaperDrawingAttemptEvent(EntityUid Used, EntityUid User, bool Handled = false);
