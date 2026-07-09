using System.Numerics;
using Content.Shared.Damage;

namespace Content.Shared._Pirate.Weapons.Melee.Events;

/// <summary>
///     Pirate port of Starlight's post-melee-hit event, raised after damage is applied.
/// </summary>
public sealed class AfterMeleeHitEvent : HandledEntityEventArgs
{
    /// <summary>
    ///     The amount of damage dealt by the melee hit after local modifiers and resistances.
    /// </summary>
    public readonly DamageSpecifier DealedDamage;

    /// <summary>
    ///     A list containing every hit entity. Can be zero.
    /// </summary>
    public IReadOnlyList<EntityUid> HitEntities;

    /// <summary>
    ///     The user who attacked with the melee weapon.
    /// </summary>
    public readonly EntityUid User;

    /// <summary>
    ///     The melee weapon used.
    /// </summary>
    public readonly EntityUid Weapon;

    /// <summary>
    ///     The direction of the attack. If null, it was a click-attack.
    /// </summary>
    public readonly Vector2? Direction;

    /// <summary>
    ///     Check this before doing hit-only work; examining melee weapons may set it false.
    /// </summary>
    public bool IsHit = true;

    public AfterMeleeHitEvent(List<EntityUid> hitEntities, EntityUid user, EntityUid weapon, DamageSpecifier dealedDamage, Vector2? direction)
    {
        HitEntities = hitEntities;
        User = user;
        Weapon = weapon;
        DealedDamage = dealedDamage;
        Direction = direction;
    }
}
