// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Lets a dead Skia reform once its damage has fallen below the fatal threshold.
/// </summary>
public sealed class SharedSkiaResurrectWhenAbleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SkiaResurrectWhenAbleComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SkiaResurrectWhenAbleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_mobState.IsDead(uid))
            {
                comp.ResurrectAt = null;
                continue;
            }

            if (!comp.CanResurrect)
            {
                comp.ResurrectAt = null;
                continue;
            }

            if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out var threshold))
                continue;

            if (!TryComp<DamageableComponent>(uid, out var damageable))
                continue;

            var positiveDamage = DamageSpecifier.GetPositive(damageable.Damage);
            if (positiveDamage.GetTotal() >= threshold)
            {
                comp.ResurrectAt = null;
                continue;
            }

            if (comp.ResurrectAt is not { } resurrectTime)
            {
                comp.ResurrectAt = _timing.CurTime + TimeSpan.FromSeconds(comp.TimeToResurrect);
                continue;
            }

            if (_timing.CurTime < resurrectTime)
                continue;

            _mobState.ChangeMobState(uid, MobState.Alive);
            comp.ResurrectAt = null;
        }
    }

    private void OnExamined(Entity<SkiaResurrectWhenAbleComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || entity.Comp.ResurrectAt is null || entity.Comp.ResurrectDesc is not { } desc)
            return;

        args.PushMarkup(Loc.GetString(desc));
    }
}
