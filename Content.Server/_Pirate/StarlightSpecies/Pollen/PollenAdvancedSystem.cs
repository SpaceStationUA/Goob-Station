// Pirate: ported from ss14Starlight/space-station-14.
using Content.Server._Pirate.StarlightSpecies.Pollen.Components;
using Content.Shared._Pirate.StarlightSpecies.Pollen.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared._Pirate.StarlightSpecies.Pollen;

namespace Content.Server._Pirate.StarlightSpecies.Pollen;

public sealed partial class PollenAdvancedSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private static readonly EntProtoId _advancedPollenCloud = "PollenAdvancedPollenCloud";
    private static readonly ProtoId<DamageGroupPrototype> _bruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroup = "Burn";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PollenSpeedBuffComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var perkQuery = EntityQueryEnumerator<PollenAdvancedComponent>();
        while (perkQuery.MoveNext(out var uid, out var perk))
        {
            if (perk.NextSpawn == TimeSpan.Zero)
                perk.NextSpawn = now + RollSpawnDelay(perk);

            if (now < perk.NextSpawn)
                continue;

            var delay = RollSpawnDelay(perk);
            perk.NextSpawn = now + delay;

            var deviation = (float)((delay - TimeSpan.FromSeconds(perk.SpawnInterval)).TotalSeconds);
            var lifetime = Math.Max(0.1f, perk.PollenLifetime + (deviation * perk.LifetimeDeviationMultiplier));

            var offset = RandomPollenOffset();
            var cloud = SpawnAtPosition(_advancedPollenCloud, Transform(uid).Coordinates.Offset(offset));
            var cloudComp = Comp<PollenAdvancedPollenComponent>(cloud);
            cloudComp.ExpiresAt = now + TimeSpan.FromSeconds(lifetime);
        }

        var cloudQuery = EntityQueryEnumerator<PollenAdvancedPollenComponent, TransformComponent>();
        while (cloudQuery.MoveNext(out var cloudUid, out var cloud, out var cloudXform))
        {
            if (cloud.Fading)
            {
                if (now >= cloud.FadeEndsAt)
                    QueueDel(cloudUid);

                continue;
            }

            if (cloud.ActivatesAt == TimeSpan.Zero)
                cloud.ActivatesAt = now + cloud.ActivationDelay;

            if (cloud.ExpiresAt == TimeSpan.Zero)
                cloud.ExpiresAt = now + cloud.Lifetime;

            // Do not interact with or naturally expire during the activation delay.
            if (now < cloud.ActivatesAt)
                continue;

            if (now >= cloud.ExpiresAt)
            {
                BeginFade(cloudUid, cloud);
                continue;
            }

            if (now < cloud.NextCheck)
                continue;

            cloud.NextCheck = now + TimeSpan.FromSeconds(1);
            CheckAdvancedPollenInteraction(cloudUid, cloud, cloudXform);
        }

        var speedBuffQuery = EntityQueryEnumerator<PollenSpeedBuffComponent>();
        while (speedBuffQuery.MoveNext(out var buffUid, out var buff))
        {
            if (now < buff.ExpiresAt)
                continue;

            RemComp<PollenSpeedBuffComponent>(buffUid);
            _movementSpeed.RefreshMovementSpeedModifiers(buffUid);
        }
    }

    private void OnRefreshSpeed(EntityUid uid, PollenSpeedBuffComponent component, RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(component.WalkModifier, component.SprintModifier);

    private TimeSpan RollSpawnDelay(PollenAdvancedComponent perk)
    {
        var variance = perk.SpawnInterval * perk.SpawnIntervalDeviation;
        var seconds = perk.SpawnInterval + _random.NextFloat(-variance, variance);
        return TimeSpan.FromSeconds(Math.Max(0.1f, seconds));
    }

    private Vector2 RandomPollenOffset()
    {
        var angle = _random.NextFloat(0f, MathF.Tau);
        var distance = MathF.Sqrt(_random.NextFloat()) * 0.5f;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }

    private void CheckAdvancedPollenInteraction(EntityUid cloudUid, PollenAdvancedPollenComponent cloud, TransformComponent cloudXform)
    {
        var nearby = _lookup.GetEntitiesInRange(cloudXform.Coordinates, cloud.CheckRange);

        EntityUid? diona = null;
        EntityUid? minded = null;

        foreach (var candidate in nearby)
        {
            if (candidate == cloudUid)
                continue;

            if (diona == null && HasComp<PollenCollectorComponent>(candidate))
            {
                diona = candidate;
                continue;
            }

            // Pirate has no Starlight medical-record pollen allergy; retain collector and mind interactions.

            // Observer ghosts should not trigger player-facing mind messages.
            if (minded == null && !HasComp<GhostComponent>(candidate) && _mind.TryGetMind(candidate, out _, out _))
                minded = candidate;
        }

        if (diona is { } dionaUid && _random.Prob(cloud.DionaChance))
        {
            ApplyAdvancedPollenEffect(dionaUid, cloud);
            BeginFade(cloudUid, cloud);
            return;
        }

        if (minded is { } mindedUid && _random.Prob(cloud.MindChance))
        {
            _popup.PopupEntity(Loc.GetString("pollen-advanced-pollen-mind-message"), mindedUid, mindedUid, PopupType.Small);
            BeginFade(cloudUid, cloud);
        }
    }

    private void ApplyAdvancedPollenEffect(EntityUid diona, PollenAdvancedPollenComponent cloud)
    {
        switch (_random.Next(3))
        {
            case 0:
                ApplyHealBuff(diona, cloud);
                break;
            case 1:
                ApplySpeedBuff(diona, cloud);
                break;
            default:
                break;
        }
    }

    private void ApplyHealBuff(EntityUid diona, PollenAdvancedPollenComponent cloud)
    {
        var heal = new DamageSpecifier(_prototype.Index(_bruteGroup), -cloud.HealBrute);
        heal += new DamageSpecifier(_prototype.Index(_burnGroup), -cloud.HealBurn);
        _damageable.TryChangeDamage(diona, heal, ignoreResistances: true);
    }

    private void ApplySpeedBuff(EntityUid diona, PollenAdvancedPollenComponent cloud)
    {
        var buff = EnsureComp<PollenSpeedBuffComponent>(diona);
        buff.WalkModifier = cloud.SpeedWalkModifier;
        buff.SprintModifier = cloud.SpeedSprintModifier;
        buff.ExpiresAt = _timing.CurTime + cloud.SpeedBuffDuration;
        _movementSpeed.RefreshMovementSpeedModifiers(diona);
    }

    private void BeginFade(EntityUid uid, PollenAdvancedPollenComponent cloud)
    {
        if (cloud.Fading)
            return;

        cloud.Fading = true;
        cloud.FadeEndsAt = _timing.CurTime + cloud.FadeDuration;
        _appearance.SetData(uid, PollenAdvancedPollenVisuals.Fading, true);
    }
}
