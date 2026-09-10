using Content.Shared._White.Xenomorphs.Acid;
using Content.Shared._White.Xenomorphs.Acid.Components;
using Content.Shared.Damage;

namespace Content.Server._White.Xenomorphs.Acid;

public sealed class XenomorphAcidSystem : SharedXenomorphAcidSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        var time = Timing.CurTime;

        var acidCorrodingQuery = EntityQueryEnumerator<AcidCorrodingComponent, TransformComponent>();
        while (acidCorrodingQuery.MoveNext(out var uid, out var acidCorroding, out _))
        {
            if (time >= acidCorroding.NextDamageAt)
            {
                _damageable.TryChangeDamage(uid, acidCorroding.DamagePerSecond);
                acidCorroding.NextDamageAt = time + TimeSpan.FromSeconds(1);
            }

            if (time <= acidCorroding.AcidExpiresAt)
                continue;

            FinishDissolve(uid, acidCorroding);
        }
    }

    private void FinishDissolve(EntityUid target, AcidCorrodingComponent corroding)
    {
        if (!TerminatingOrDeleted(corroding.Acid))
            QueueDel(corroding.Acid);

        if (!corroding.DissolveToAsh)
        {
            RemCompDeferred<AcidCorrodingComponent>(target);
            return;
        }

        var coords = _transform.GetMoverCoordinates(target);
        RemComp<AcidCorrodingComponent>(target);
        QueueDel(target);
        Spawn(corroding.AshPrototype, coords);
    }
}
