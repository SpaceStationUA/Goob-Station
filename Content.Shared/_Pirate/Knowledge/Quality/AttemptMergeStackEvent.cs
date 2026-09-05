// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Stacks;

/// <summary>
/// Allows per-stack metadata to reject a merge before counts change.
/// </summary>
[ByRefEvent]
public record struct AttemptMergeStackEvent(EntityUid OtherStack, bool Cancelled = false);
