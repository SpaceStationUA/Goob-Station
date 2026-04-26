using Content.Shared.Damage;
using Robust.Shared.GameObjects;

namespace Content.Shared._Pirate.Weapons.Ranged.Events;

[ByRefEvent]
public record struct HitScanBlockAttemptEvent(
    EntityUid? Shooter,
    EntityUid SourceItem,
    EntityUid Target,
    DamageSpecifier? Damage = null,
    bool Cancelled = false);
