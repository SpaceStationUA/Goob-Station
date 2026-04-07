using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Pirate.Shared._JustDecor.Missions.Components;
using Content.Shared._Lavaland.MobPhases;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Server._JustDecor.Missions.Systems;

public sealed class MissionBossSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobPhasesSystem _phases = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BossShieldComponent, ComponentInit>(OnShieldInit);
        SubscribeLocalEvent<BossShieldComponent, DamageModifyEvent>(OnShieldDamageModify);
        SubscribeLocalEvent<MissionBossComponent, DamageChangedEvent>(OnBossDamageChanged);
        SubscribeLocalEvent<MissionBossComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<MissionBossComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnShieldInit(Entity<BossShieldComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.MaxShieldHp <= 0f)
            ent.Comp.MaxShieldHp = ent.Comp.ShieldHp;
    }

    private void OnShieldDamageModify(Entity<BossShieldComponent> ent, ref DamageModifyEvent args)
    {
        if (!ent.Comp.Enabled || ent.Comp.ShieldHp <= 0f)
            return;

        var total = args.Damage.GetTotal();
        if (total <= FixedPoint2.Zero)
            return;

        var shieldAvailable = FixedPoint2.New(ent.Comp.ShieldHp);
        var absorb = FixedPoint2.Min(total, shieldAvailable);
        var remaining = total - absorb;

        if (remaining <= FixedPoint2.Zero)
        {
            args.Damage *= 0f;
        }
        else
        {
            var factor = remaining.Float() / total.Float();
            args.Damage *= factor;
        }

        ent.Comp.ShieldHp -= absorb.Float();
        if (ent.Comp.ShieldHp <= 0f)
        {
            ent.Comp.ShieldHp = 0f;
            ent.Comp.Enabled = false;
        }
    }

    private void OnBossDamageChanged(Entity<MissionBossComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp<MobPhasesComponent>(ent, out var phases) ||
            !TryComp<DamageableComponent>(ent, out var damageable))
            return;

        _phases.UpdatePhases((ent.Owner, phases, damageable));

        var phase = phases.CurrentPhase;
        if (phase == ent.Comp.LastProcessedPhase)
            return;

        ent.Comp.LastProcessedPhase = phase;

        if (TryComp<BossShieldComponent>(ent, out var shield))
        {
            var shouldEnable = phase == ent.Comp.ShieldPhase;
            if (shield.Enabled != shouldEnable)
            {
                shield.Enabled = shouldEnable;
                if (shouldEnable && shield.ResetOnEnable)
                    shield.ShieldHp = shield.MaxShieldHp;
            }
        }

        if (TryComp<BossPhaseActionsComponent>(ent, out var actions))
        {
            if (phase == ent.Comp.ReinforcementPhase)
            {
                SpawnPhaseActions(ent.Owner, actions.ReinforcementPrototypes, actions.ReinforcementCount, actions.SpawnRadius);
                SpawnPhaseActions(ent.Owner, actions.TurretPrototypes, actions.TurretCount, actions.SpawnRadius);
            }

            if (phase == ent.Comp.BerserkPhase)
            {
                SpawnPhaseActions(ent.Owner, actions.BerserkReinforcements, actions.BerserkReinforcementCount, actions.SpawnRadius);
                SpawnPhaseActions(ent.Owner, actions.BerserkTurrets, actions.BerserkTurretCount, actions.SpawnRadius);
            }
        }

        if (phase == ent.Comp.BerserkPhase)
        {
            EnsureComp<BossRegenerationComponent>(ent);
            ent.Comp.BerserkActive = true;
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
        }
        else if (ent.Comp.BerserkActive)
        {
            ent.Comp.BerserkActive = false;
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
        }
    }

    private void OnRefreshMovespeed(EntityUid uid, MissionBossComponent comp, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!comp.BerserkActive)
            return;

        args.ModifySpeed(comp.BerserkSpeedMultiplier, comp.BerserkSpeedMultiplier);
    }

    private void OnGetMeleeDamage(EntityUid uid, MissionBossComponent comp, ref GetMeleeDamageEvent args)
    {
        if (!comp.BerserkActive)
            return;

        args.Damage *= comp.BerserkDamageMultiplier;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BossRegenerationComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var regen, out var damageable))
        {
            regen.Accumulator += frameTime;
            if (regen.Accumulator < regen.TickInterval)
                continue;

            var ticks = (int) (regen.Accumulator / regen.TickInterval);
            regen.Accumulator -= ticks * regen.TickInterval;

            var healAmount = regen.HealPerSecond * regen.TickInterval * ticks;
            if (healAmount <= 0f)
                continue;

            var total = damageable.TotalDamage.Float();
            if (total <= 0f)
                continue;

            var spec = new DamageSpecifier();
            foreach (var (type, amount) in damageable.Damage.DamageDict)
            {
                if (amount <= FixedPoint2.Zero)
                    continue;

                var portion = (amount.Float() / total) * healAmount;
                if (portion <= 0f)
                    continue;

                spec.DamageDict[type] = FixedPoint2.New(-portion);
            }

            if (!spec.Empty)
                _damageable.TryChangeDamage(uid, spec, interruptsDoAfters: false);
        }
    }

    private void SpawnPhaseActions(EntityUid boss, List<EntProtoId> prototypes, int count, float radius)
    {
        if (count <= 0 || prototypes.Count == 0)
            return;

        var coords = Transform(boss).Coordinates;

        for (var i = 0; i < count; i++)
        {
            var xOffset = _random.NextFloat(-radius, radius);
            var yOffset = _random.NextFloat(-radius, radius);
            var spawnCoords = coords.Offset(new Vector2(xOffset, yOffset));
            Spawn(_random.Pick(prototypes), spawnCoords);
        }
    }
}
