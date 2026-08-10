// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Skia;

public sealed class SkiaLightLevelHealthSystem : SharedSkiaLightLevelHealthSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SkiaLightReactiveSystem _lightReactive = default!;

    private TimeSpan _nextUpdate = TimeSpan.MinValue;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<SkiaLightLevelHealthComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobState.IsDead(uid) && !comp.HealWhenDead)
                continue;

            var lightLevel = _lightReactive.GetLightLevel(uid, forceUpdate: true);
            var currentThreshold = CurrentThreshold(lightLevel, comp);
            if (currentThreshold != comp.CurrentThreshold)
            {
                comp.CurrentThreshold = currentThreshold;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }

            if (TryComp<SkiaResurrectWhenAbleComponent>(uid, out var resurrect))
            {
                // Dead Skia can only begin or continue recovery while standing in darkness.
                var canResurrect = !_mobState.IsDead(uid) || currentThreshold == -1;
                if (resurrect.CanResurrect != canResurrect || (!canResurrect && resurrect.ResurrectAt is not null))
                {
                    resurrect.CanResurrect = canResurrect;
                    resurrect.ResurrectAt = canResurrect ? resurrect.ResurrectAt : null;
                    Dirty(uid, resurrect);
                }
            }

            if (currentThreshold == 0)
                continue;

            var damage = currentThreshold == -1 ? comp.DarkDamage : comp.LightDamage;
            if (damage.AnyPositive() && _mobState.IsDead(uid))
                continue;

            TryDealDamage(new Entity<SkiaLightLevelHealthComponent>(uid, comp), damage);
        }
    }
}
