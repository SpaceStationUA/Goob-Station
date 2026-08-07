// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Shitmed.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Damage;

public sealed class SoulDamageRegenerationSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> SoulDamageType = "Soul";
    private readonly HashSet<EntityUid> _storedSoulHealing = new();

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, ComponentStartup>(OnDamageableStartup);
        SubscribeLocalEvent<DamageableComponent, ComponentShutdown>(OnDamageableShutdown);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SoulDamageRegenerationComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var regeneration, out var damageable))
        {
            if (IsAttachedBodyPart(uid))
            {
                RemCompDeferred<SoulDamageRegenerationComponent>(uid);
                continue;
            }

            if (currentTime < regeneration.NextHeal)
                continue;

            regeneration.NextHeal = currentTime + regeneration.HealInterval;

            if (!damageable.Damage.DamageDict.TryGetValue(SoulDamageType.Id, out var soulDamage) ||
                soulDamage <= FixedPoint2.Zero)
            {
                RemCompDeferred<SoulDamageRegenerationComponent>(uid);
                continue;
            }

            if (regeneration.HealAmount <= FixedPoint2.Zero)
                continue;

            var amount = FixedPoint2.Min(soulDamage, regeneration.HealAmount);
            RegenerateSoulDamage(uid, damageable, amount);
        }
    }

    private void RegenerateSoulDamage(EntityUid uid, DamageableComponent damageable, FixedPoint2 amount)
    {
        if (!TryComp<BodyComponent>(uid, out var body) || body.BodyType != BodyType.Complex)
        {
            HealSoulDamage(uid, damageable, amount);
            return;
        }

        var remaining = amount;
        foreach (var part in _body.GetBodyChildren(uid))
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            if (!TryComp<DamageableComponent>(part.Id, out var partDamageable) ||
                !partDamageable.Damage.DamageDict.TryGetValue(SoulDamageType.Id, out var partSoulDamage) ||
                partSoulDamage <= FixedPoint2.Zero)
            {
                continue;
            }

            var partAmount = FixedPoint2.Min(partSoulDamage, remaining);
            HealSoulDamage(part.Id, partDamageable, partAmount);
            remaining -= partAmount;
        }

        if (remaining > FixedPoint2.Zero)
            HealStoredSoulDamage(uid, damageable, remaining);
    }

    private void HealSoulDamage(EntityUid uid, DamageableComponent damageable, FixedPoint2 amount)
    {
        var healing = new DamageSpecifier(_prototypes.Index(SoulDamageType), -amount);
        _damageable.TryChangeDamage(
            uid,
            healing,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            canMiss: false,
            ignoreBlockers: true);
    }

    private void HealStoredSoulDamage(EntityUid uid, DamageableComponent damageable, FixedPoint2 amount)
    {
        if (!damageable.Damage.DamageDict.TryGetValue(SoulDamageType.Id, out var soulDamage) ||
            soulDamage <= FixedPoint2.Zero)
        {
            return;
        }

        var updatedDamage = new DamageSpecifier(damageable.Damage);
        updatedDamage.DamageDict[SoulDamageType.Id] = soulDamage - FixedPoint2.Min(soulDamage, amount);

        _storedSoulHealing.Add(uid);
        try
        {
            _damageable.SetDamage(uid, damageable, updatedDamage);
        }
        finally
        {
            _storedSoulHealing.Remove(uid);
        }
    }

    private void OnDamageChanged(Entity<DamageableComponent> entity, ref DamageChangedEvent args)
    {
        if (IsAttachedBodyPart(entity))
            return;

        if (!args.Damageable.Damage.DamageDict.TryGetValue(SoulDamageType.Id, out var soulDamage) ||
            soulDamage <= FixedPoint2.Zero)
        {
            RemCompDeferred<SoulDamageRegenerationComponent>(entity);
            return;
        }

        var hasNewSoulDamage = !_storedSoulHealing.Contains(entity.Owner) &&
            (args.DamageDelta == null ||
                args.DamageDelta.DamageDict.TryGetValue(SoulDamageType.Id, out var soulDelta) &&
                soulDelta > FixedPoint2.Zero);

        var alreadyTracked = EnsureComp<SoulDamageRegenerationComponent>(entity, out var regeneration);
        if (alreadyTracked && !hasNewSoulDamage)
            return;

        regeneration.NextHeal = _timing.CurTime + regeneration.RecoveryDelay;
    }

    private void OnDamageableStartup(Entity<DamageableComponent> entity, ref ComponentStartup args)
    {
        if (IsAttachedBodyPart(entity) ||
            !entity.Comp.Damage.DamageDict.TryGetValue(SoulDamageType.Id, out var soulDamage) ||
            soulDamage <= FixedPoint2.Zero)
        {
            return;
        }

        EnsureComp<SoulDamageRegenerationComponent>(entity, out var regeneration);
        regeneration.NextHeal = _timing.CurTime + regeneration.RecoveryDelay;
    }

    private void OnDamageableShutdown(Entity<DamageableComponent> entity, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(entity))
            RemCompDeferred<SoulDamageRegenerationComponent>(entity);
    }

    private bool IsAttachedBodyPart(EntityUid uid)
        => TryComp<BodyPartComponent>(uid, out var bodyPart) && bodyPart.Body != null;
}
