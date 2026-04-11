using Robust.Shared.GameObjects;

namespace Content.Shared._Pirate.Weapons.Ranged.Events;

[ByRefEvent]
public record struct HitScanBlockAttemptEvent(
    EntityUid? Shooter,
    EntityUid SourceItem,
    EntityUid Target,
    bool Cancelled = false);
