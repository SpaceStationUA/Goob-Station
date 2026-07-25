using System.Numerics;
using Content.Client.Gameplay;
using Content.Goobstation.Common.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly IConfigurationManager _predictedTargetConfiguration = default!;
    [Dependency] private readonly EntityLookupSystem _predictedTargetLookup = default!;

    private readonly HashSet<Entity<RequireProjectileTargetComponent>> _predictedProneTargets = new();

    /// <summary>
    /// Sprite alpha clicking can miss a prone mob even when the cursor is inside the
    /// small world-space hit zone used by projectile collision. Mirror that hit zone
    /// when no clickable damageable entity was found so the server receives the
    /// explicit entity the player aimed at.
    /// </summary>
    private EntityUid? GetPredictedProjectileTarget(GameplayStateBase screen, MapCoordinates mousePosition)
    {
        if (screen.GetDamageableClickedEntity(mousePosition) is { } clicked)
            return clicked;

        var hitZone = _predictedTargetConfiguration.GetCVar(GoobCVars.CrawlHitzoneSize);
        _predictedProneTargets.Clear();
        _predictedTargetLookup.GetEntitiesInRange(
            mousePosition,
            hitZone,
            _predictedProneTargets,
            LookupFlags.Dynamic);

        EntityUid? closest = null;
        var closestDistanceSquared = hitZone * hitZone;
        foreach (var candidate in _predictedProneTargets)
        {
            if (!candidate.Comp.Active ||
                candidate.Owner == _player.LocalEntity ||
                !HasComp<DamageableComponent>(candidate.Owner) ||
                TerminatingOrDeleted(candidate.Owner))
            {
                continue;
            }

            var candidatePosition = TransformSystem.GetMapCoordinates(candidate.Owner);
            if (candidatePosition.MapId != mousePosition.MapId)
                continue;

            var distanceSquared = Vector2.DistanceSquared(candidatePosition.Position, mousePosition.Position);
            if (distanceSquared > closestDistanceSquared)
                continue;

            closest = candidate.Owner;
            closestDistanceSquared = distanceSquared;
        }

        return closest;
    }
}
