using System.Numerics;

namespace Content.Shared.Damage.Events;

/// <summary>
///     Pirate compatibility event for Starlight stamina-melee modifiers.
/// </summary>
public sealed class StaminaMeleeHitEvent : EntityEventArgs
{
    public readonly EntityUid User;
    public readonly EntityUid Weapon;
    public readonly EntityUid Target;
    public readonly Vector2? Direction;

    public float Multiplier = 1f;

    public StaminaMeleeHitEvent(EntityUid user, EntityUid weapon, EntityUid target, Vector2? direction)
    {
        User = user;
        Weapon = weapon;
        Target = target;
        Direction = direction;
    }
}
