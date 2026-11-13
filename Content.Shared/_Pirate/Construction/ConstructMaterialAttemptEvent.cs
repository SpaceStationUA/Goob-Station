// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Construction;

/// <summary>Raised on the builder before accepting materials for a construction graph step.</summary>
[ByRefEvent]
public record struct ConstructMaterialAttemptEvent(string Graph, string Node, EntityUid Material, bool Cancelled = false);
