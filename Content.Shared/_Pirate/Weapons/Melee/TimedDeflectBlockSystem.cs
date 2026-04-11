using System.Numerics;
using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared._Pirate.Projectiles;
using Content.Shared._Pirate.Weapons.Melee.Components;
using Content.Shared._Pirate.Weapons.Ranged.Events;
using Content.Shared._Shitmed.ItemSwitch;
using Content.Shared._Shitmed.ItemSwitch.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Item;
using Content.Shared._White.Animations;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Goobstation.Common.Effects;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Weapons.Melee;

public sealed class TimedDeflectBlockSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedItemSwitchSystem _itemSwitch = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedNinjaSuitSystem _ninjaSuit = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SparksSystem _sparks = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedWieldableSystem _wieldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedDeflectBlockComponent, ItemWieldedEvent>(OnWielded);
        SubscribeLocalEvent<TimedDeflectBlockComponent, ItemUnwieldedEvent>(OnUnwielded);
        SubscribeLocalEvent<TimedDeflectBlockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TimedDeflectBlockComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<TimedDeflectBlockComponent, ItemSwitchedEvent>(OnItemSwitched);
        SubscribeLocalEvent<TimedDeflectBlockComponent, HeldRelayedEvent<ProjectileReflectAttemptEvent>>(OnProjectileReflectAttempt);
        SubscribeLocalEvent<TimedDeflectBlockComponent, HeldRelayedEvent<HitScanReflectAttemptEvent>>(OnHitscanReflectAttempt);
        SubscribeLocalEvent<TimedDeflectBlockComponent, HeldRelayedEvent<HitScanBlockAttemptEvent>>(OnHitscanBlockAttempt);
        SubscribeLocalEvent<TimedDeflectBlockComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<TimedDeflectBlockComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);

        SubscribeLocalEvent<HandsComponent, BeforeHarmfulActionEvent>(OnBeforeHarmfulAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<TimedDeflectBlockComponent>();
        while (query.MoveNext(out var uid, out var block))
        {
            // Detect sheath state by checking if the weapon is inside a SheathPreservesEnergy container.
            var sheathed = _containers.TryGetContainingContainer((uid, null, null), out var container)
                && HasComp<SheathPreservesEnergyComponent>(container.Owner);

            if (sheathed && !block.IsSheathed)
            {
                // Just sheathed — record when.
                block.IsSheathed = true;
                block.SheathStartTime = _timing.CurTime;
            }
            else if (!sheathed && block.IsSheathed)
            {
                // Just unsheathed — push LastDeflectTime forward so the sheathed period
                // only counts at SheathDecayMultiplier speed toward the decay delay.
                var sheathDuration = _timing.CurTime - block.SheathStartTime;
                block.LastDeflectTime += sheathDuration * (1.0 - block.SheathWindowMultiplier);
                block.IsSheathed = false;
            }

            if (_timing.CurTime - block.LastDeflectTime < TimeSpan.FromSeconds(block.PowerDecayDelay) ||
                block.CurrentPower <= block.MinPower)
            {
                continue;
            }

            var decayRate = sheathed ? block.SheathDecayMultiplier : 1f;

            // Advance power locally every frame without marking dirty —
            // clients derive display and damage from the discrete level, not the float.
            var levelBefore = GetLevel(block);
            block.CurrentPower = Math.Clamp(
                block.CurrentPower - block.PowerDecayPerSecond * frameTime * decayRate,
                block.MinPower,
                block.MaxPower);

            // Only replicate when the level (and therefore visual state / damage) actually changes.
            if (GetLevel(block) != levelBefore)
            {
                UpdateVisualState(uid, block);
                Dirty(uid, block);
            }
        }
    }

    private void OnStartup(Entity<TimedDeflectBlockComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.CurrentPower = Math.Clamp(ent.Comp.CurrentPower, ent.Comp.MinPower, ent.Comp.MaxPower);
        ent.Comp.LastDeflectTime = _timing.CurTime;

        if (_net.IsServer)
            UpdateVisualState(ent.Owner, ent.Comp);
    }

    private void OnWielded(Entity<TimedDeflectBlockComponent> ent, ref ItemWieldedEvent args)
    {
        ent.Comp.DeflectWindowStart = _timing.CurTime - GetActivationGrace(args.User, ent.Comp);
        ent.Comp.DeflectWindowEnd = _timing.CurTime + TimeSpan.FromSeconds(GetDeflectWindow(ent.Comp));

        if (_net.IsServer)
            Dirty(ent.Owner, ent.Comp);
    }

    private void OnAfterState(Entity<TimedDeflectBlockComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyReplicatedVisualState(ent.Owner, GetVisualState(ent.Comp));
    }

    private void OnItemSwitched(Entity<TimedDeflectBlockComponent> ent, ref ItemSwitchedEvent args)
    {
        ApplyReplicatedVisualState(ent.Owner, args.State);
    }

    private void OnUnwielded(Entity<TimedDeflectBlockComponent> ent, ref ItemUnwieldedEvent args)
    {
        ent.Comp.DeflectWindowStart = TimeSpan.Zero;
        ent.Comp.DeflectWindowEnd = TimeSpan.Zero;

        if (_net.IsServer)
            Dirty(ent.Owner, ent.Comp);
    }

    private void OnBeforeHarmfulAction(Entity<HandsComponent> ent, ref BeforeHarmfulActionEvent args)
    {
        if (args.Cancelled || args.Type != HarmfulActionType.Harm)
            return;

        if (!TryGetActiveDeflectWeapon(ent, out var weapon, out var block) || block == null)
            return;

        ApplyDefense(ent.Owner, weapon, block, args.User, projectile: null, out var deflected);
        args.Cancel();

        // If the attacker was wielding a TimedDeflectBlock weapon, break their grip —
        // a blocked swing counts as "reaching the target" for the wield-break rule.
        // BeforeHarmfulActionEvent is shared, but wield state is server-authoritative.
        if (_net.IsServer &&
            _hands.TryGetActiveItem(args.User, out var attackerWeapon) &&
            TryComp<TimedDeflectBlockComponent>(attackerWeapon, out _) &&
            TryComp<WieldableComponent>(attackerWeapon, out var attackerWieldable) &&
            attackerWieldable.Wielded)
        {
            _wieldable.TryUnwield(attackerWeapon.Value, attackerWieldable, args.User, force: true);
        }
    }

    private void OnProjectileReflectAttempt(
        Entity<TimedDeflectBlockComponent> ent,
        ref HeldRelayedEvent<ProjectileReflectAttemptEvent> args)
    {
        if (args.Args.Cancelled ||
            !TryComp<WieldableComponent>(ent, out var wieldable) ||
            !wieldable.Wielded)
        {
            return;
        }

        if (ent.Comp.DeflectToSource &&
            args.Args.Component.Shooter is { } shooter &&
            TryDeflectProjectileToSource(args.Args.Target, ent.Owner, ent.Comp, shooter, args.Args.ProjUid, args.Args.Component))
        {
            args.Args.Cancelled = true;
            return;
        }

        ApplyDefense(args.Args.Target, ent.Owner, ent.Comp, args.Args.Component.Shooter, args.Args.ProjUid, out _);
        args.Args.Cancelled = true;
    }

    private void OnHitscanReflectAttempt(
        Entity<TimedDeflectBlockComponent> ent,
        ref HeldRelayedEvent<HitScanReflectAttemptEvent> args)
    {
        if (args.Args.Reflected ||
            !ent.Comp.DeflectToSource ||
            args.Args.Shooter == null ||
            !TryComp<WieldableComponent>(ent, out var wieldable) ||
            !wieldable.Wielded ||
            !TryApplyDirectedDeflect(args.Args.Target, ent.Owner, ent.Comp, args.Args.Shooter.Value))
        {
            return;
        }

        var direction = GetDirectionToEntity(args.Args.Target, args.Args.Shooter.Value);
        args.Args.Direction = direction == Vector2.Zero
            ? -args.Args.Direction
            : direction;
        args.Args.Reflected = true;
    }

    private void OnHitscanBlockAttempt(
        Entity<TimedDeflectBlockComponent> ent,
        ref HeldRelayedEvent<HitScanBlockAttemptEvent> args)
    {
        if (args.Args.Cancelled ||
            !TryComp<WieldableComponent>(ent, out var wieldable) ||
            !wieldable.Wielded)
        {
            return;
        }

        ApplyDefense(args.Args.Target, ent.Owner, ent.Comp, args.Args.Shooter, projectile: null, out _);
        args.Args.Cancelled = true;
    }

    private void OnGetMeleeDamage(Entity<TimedDeflectBlockComponent> ent, ref GetMeleeDamageEvent args)
    {
        var level = GetLevel(ent.Comp);
        if (level <= 0)
            return;

        var bonus = new DamageSpecifier();
        foreach (var (type, perLevel) in ent.Comp.BonusDamagePerLevel)
        {
            var amount = level * perLevel;
            if (amount > 0f)
                bonus.DamageDict[type] = FixedPoint2.New(amount);
        }

        args.Damage += bonus;
    }

    private void OnMeleeHit(Entity<TimedDeflectBlockComponent> ent, ref MeleeHitEvent args)
    {
        if (!_net.IsServer)
            return;

        // Only drain power and break wield when the swing actually reached a target (miss = empty list).
        if (args.HitEntities.Count > 0)
        {
            SetPower(ent.Owner, ent.Comp, ent.Comp.CurrentPower - ent.Comp.PowerLossOnMeleeHit);

            if (TryComp<WieldableComponent>(ent, out var wieldable) && wieldable.Wielded)
                _wieldable.TryUnwield(ent.Owner, wieldable, args.User, force: true);
        }
    }

    private bool TryGetActiveDeflectWeapon(
        Entity<HandsComponent> user,
        out EntityUid weapon,
        out TimedDeflectBlockComponent? block)
    {
        EntityUid bestWeapon = EntityUid.Invalid;
        TimedDeflectBlockComponent? bestBlock = null;

        foreach (var held in _hands.EnumerateHeld((user.Owner, user.Comp)))
        {
            if (!TryComp<TimedDeflectBlockComponent>(held, out TimedDeflectBlockComponent? foundBlock) ||
                !TryComp<WieldableComponent>(held, out var wieldable) ||
                !wieldable.Wielded)
            {
                continue;
            }

            if (bestBlock == null)
            {
                bestWeapon = held;
                bestBlock = foundBlock;
                continue;
            }

            var foundInWindow = IsWithinDeflectWindow(user.Owner, foundBlock);
            var bestInWindow = IsWithinDeflectWindow(user.Owner, bestBlock);

            if (foundInWindow && !bestInWindow ||
                foundInWindow == bestInWindow && foundBlock.CurrentPower > bestBlock.CurrentPower)
            {
                bestWeapon = held;
                bestBlock = foundBlock;
            }
        }

        weapon = bestWeapon;
        block = bestBlock;
        return bestBlock != null;
    }

    private bool TryApplyDirectedDeflect(
        EntityUid defender,
        EntityUid weapon,
        TimedDeflectBlockComponent block,
        EntityUid attacker)
    {
        if (!IsWithinDeflectWindow(defender, block))
            return false;

        ApplySuccessfulDeflect(defender, weapon, block, attacker);
        RevealDefender(defender);
        return true;
    }

    private bool TryDeflectProjectileToSource(
        EntityUid defender,
        EntityUid weapon,
        TimedDeflectBlockComponent block,
        EntityUid attacker,
        EntityUid projectileUid,
        ProjectileComponent projectile)
    {
        // Validate all redirect prerequisites before committing any side effects.
        if (!TryComp<PhysicsComponent>(projectileUid, out var physics))
            return false;

        var direction = GetDirectionToEntity(projectileUid, attacker);
        if (direction == Vector2.Zero)
            return false;

        var existingVelocity = _physics.GetMapLinearVelocity(projectileUid, physics);
        var speed = existingVelocity.Length();
        if (speed <= 0.001f)
            speed = physics.LinearVelocity.Length();

        if (speed <= 0.001f)
            return false;

        // All checks passed — apply deflect side effects (power gain, stamina, sound, stealth reveal).
        if (!TryApplyDirectedDeflect(defender, weapon, block, attacker))
            return false;

        var desiredVelocity = direction * speed;
        _physics.SetLinearVelocity(projectileUid, physics.LinearVelocity + desiredVelocity - existingVelocity, body: physics);
        _transform.SetWorldRotation(projectileUid, direction.ToWorldAngle() + projectile.Angle);

        projectile.Weapon = weapon;
        projectile.ProjectileSpent = false;
        projectile.IgnoredEntities.Clear(); // allow the redirected bullet to hit its original shooter
        projectile.IgnoredEntities.Add(defender); // prevent the deflected bullet from immediately re-hitting the defender

        if (TryGetMapPosition(attacker, out var attackerPosition))
            projectile.TargetCoordinates = attackerPosition.Position;

        if (TryComp<HomingProjectileComponent>(projectileUid, out var homing))
        {
            homing.Target = attacker;

            if (_net.IsServer)
                Dirty(projectileUid, homing);
        }

        if (_net.IsServer)
            Dirty(projectileUid, projectile);

        return true;
    }

    private void ApplyDefense(
        EntityUid defender,
        EntityUid weapon,
        TimedDeflectBlockComponent block,
        EntityUid? attacker,
        EntityUid? projectile,
        out bool deflected)
    {
        deflected = IsWithinDeflectWindow(defender, block);

        if (deflected)
            ApplySuccessfulDeflect(defender, weapon, block, attacker);
        else
        {
            AdjustStamina(defender, GetBlockStaminaDamage(block), attacker, weapon);
            _audio.PlayPredicted(block.BlockSound, weapon, attacker);
        }

        // A blocked hit still counts as being hit — reveal stealth/ninja invisibility.
        RevealDefender(defender);

        if (projectile is { } projectileUid && !Deleted(projectileUid))
        {
            var deleteEv = new DeletingProjectileEvent(projectileUid);
            RaiseLocalEvent(ref deleteEv);
            PredictedQueueDel(projectileUid);
        }
    }

    private void ApplySuccessfulDeflect(
        EntityUid defender,
        EntityUid weapon,
        TimedDeflectBlockComponent block,
        EntityUid? attacker)
    {
        AddDeflectPower(weapon, block);
        block.DeflectWindowEnd = _timing.CurTime + TimeSpan.FromSeconds(GetDeflectWindow(block));

        if (_net.IsServer)
            Dirty(weapon, block);

        AdjustStamina(defender, GetDeflectStaminaCost(block), attacker, weapon);
        _sparks.DoSparks(Transform(weapon).Coordinates, minSparks: 1, maxSparks: 2, playSound: false);
        _audio.PlayPredicted(block.DeflectSound, weapon, attacker);
        TryBackflip(defender, block);
    }

    /// <summary>
    /// Reveals a defending ninja's invisibility when they intercept an attack.
    /// Mirrors the reveal that <see cref="SharedGoobStealthSystem"/> would apply on actual damage,
    /// since projectile deletion means <c>BeforeDamageChangedEvent</c> never fires on the defender.
    /// </summary>
    private void RevealDefender(EntityUid defender)
    {
        if (!TryComp<StealthComponent>(defender, out var stealth) || !stealth.RevealOnDamage)
            return;

        _stealth.ModifyVisibility(defender, stealth.MaxVisibility, stealth);

        if (TryComp<SpaceNinjaComponent>(defender, out var ninja) &&
            ninja.Suit is { } suit &&
            TryComp<NinjaSuitComponent>(suit, out var suitComp))
        {
            _ninjaSuit.RevealNinja((suit, suitComp), defender, true);
        }
    }

    private void AdjustStamina(EntityUid defender, float fraction, EntityUid? attacker, EntityUid weapon)
    {
        if (!_net.IsServer ||
            fraction == 0f ||
            !TryComp<StaminaComponent>(defender, out var stamina))
        {
            return;
        }

        var amount = stamina.CritThreshold * fraction;
        _stamina.TakeStaminaDamage(defender, amount, stamina, source: attacker, with: weapon, visual: false, logDamage: amount > 0f);
    }

    private void AddDeflectPower(EntityUid weapon, TimedDeflectBlockComponent block)
    {
        block.LastDeflectTime = _timing.CurTime;
        SetPower(weapon, block, block.CurrentPower + block.PowerGainOnDeflect);
    }

    private void SetPower(EntityUid weapon, TimedDeflectBlockComponent block, float power)
    {
        var clampedPower = Math.Clamp(power, block.MinPower, block.MaxPower);
        if (Math.Abs(block.CurrentPower - clampedPower) < 0.001f)
            return;

        block.CurrentPower = clampedPower;

        if (_net.IsServer)
        {
            UpdateVisualState(weapon, block);
            Dirty(weapon, block);
        }
    }

    private void UpdateVisualState(EntityUid weapon, TimedDeflectBlockComponent block)
    {
        if (!TryComp<ItemSwitchComponent>(weapon, out var itemSwitch))
            return;

        var state = GetVisualState(block);
        var wieldedState = GetWieldedInhandState(state);

        if (itemSwitch.State != state)
        {
            _itemSwitch.Switch((weapon, itemSwitch), state, predicted: false);
        }

        if (!TryComp<WieldableComponent>(weapon, out var wieldable))
            return;

        var wieldableChanged = false;
        if (wieldable.WieldedInhandPrefix != wieldedState)
        {
            wieldable.WieldedInhandPrefix = wieldedState;
            wieldableChanged = true;
        }
        if (wieldable.OldInhandPrefix != state)
        {
            wieldable.OldInhandPrefix = state;
            wieldableChanged = true;
        }

        if (TryComp<ItemComponent>(weapon, out var item) && wieldable.Wielded)
            _item.SetHeldPrefix(weapon, wieldedState, component: item);

        if (wieldableChanged)
            Dirty(weapon, wieldable);
    }

    private void ApplyReplicatedVisualState(EntityUid weapon, string state)
    {
        if (!HasComp<ItemSwitchComponent>(weapon))
            return;

        var wieldedState = GetWieldedInhandState(state);
        var changed = false;

        if (TryComp<ItemComponent>(weapon, out var item))
        {
            var targetHeldPrefix = TryComp<WieldableComponent>(weapon, out var wieldableState) && wieldableState.Wielded
                ? wieldedState
                : state;

            if (item.HeldPrefix != targetHeldPrefix)
            {
                _item.SetHeldPrefix(weapon, targetHeldPrefix, component: item);
                changed = true;
            }
        }

        if (TryComp<ClothingComponent>(weapon, out var clothing) && clothing.EquippedPrefix != state)
        {
            _clothing.SetEquippedPrefix(weapon, state, clothing);
            changed = true;
        }

        if (TryComp<WieldableComponent>(weapon, out var wieldable))
        {
            if (wieldable.WieldedInhandPrefix != wieldedState)
            {
                wieldable.WieldedInhandPrefix = wieldedState;
                changed = true;
            }

            if (wieldable.OldInhandPrefix != state)
            {
                wieldable.OldInhandPrefix = state;
                changed = true;
            }
        }

        if (changed)
            _item.VisualsChanged(weapon);
    }

    private float GetBlockStaminaDamage(TimedDeflectBlockComponent block)
    {
        return GetBaseStaminaDamage(block) * block.BlockStaminaMultiplier;
    }

    private float GetDeflectStaminaCost(TimedDeflectBlockComponent block)
    {
        return GetBaseStaminaDamage(block) * block.DeflectStaminaMultiplier;
    }

    private float GetBaseStaminaDamage(TimedDeflectBlockComponent block)
    {
        return MathF.Max(0f, block.BlockStaminaDamageFraction - GetLevel(block) * block.BlockStaminaDamageReductionPerLevel);
    }

    private float GetDeflectWindow(TimedDeflectBlockComponent block)
    {
        return block.DeflectWindow + GetLevel(block) * block.DeflectWindowBonusPerLevel;
    }

    private bool IsWithinDeflectWindow(EntityUid defender, TimedDeflectBlockComponent block)
    {
        var now = _timing.CurTime;
        var lagComp = GetDeflectLagCompensation(defender, block);
        return now >= block.DeflectWindowStart && now <= block.DeflectWindowEnd + lagComp;
    }

    // session.Ping is RTT in milliseconds.
    // Dividing by 2000f converts to one-way latency in seconds (RTT / 2 / 1000ms).
    // Dividing by 1000f converts to full RTT in seconds (RTT / 1000ms).
    private const float OneWayPingDivisor = 2000f;
    private const float RttPingDivisor = 1000f;

    /// <summary>
    /// How much extra time to add to the deflect window end for incoming hit detection.
    /// Uses one-way latency (RTT/2): a shot fired on the client takes one-way time to reach
    /// the server, so we extend the window by that amount to avoid false misses.
    /// Scaled by <see cref="TimedDeflectBlockComponent.DeflectLagCompensationMultiplier"/>.
    /// </summary>
    private TimeSpan GetDeflectLagCompensation(EntityUid defender, TimedDeflectBlockComponent block)
    {
        if (block.DeflectLagCompensationMultiplier <= 0f ||
            block.MaxDeflectLagCompensation <= 0f ||
            !_player.TryGetSessionByEntity(defender, out var session))
        {
            return TimeSpan.Zero;
        }

        var compensationSeconds = MathF.Min(
            block.MaxDeflectLagCompensation,
            session.Ping / OneWayPingDivisor * block.DeflectLagCompensationMultiplier);

        return TimeSpan.FromSeconds(compensationSeconds);
    }

    /// <summary>
    /// How far back to push the deflect window start when the holder wields the weapon.
    /// Uses full RTT: the client pressed wield at time T, but the server only processes it
    /// after a full round-trip, so the window start is pushed back by RTT to align with
    /// when the client believed the deflect became active.
    /// Scaled by <see cref="TimedDeflectBlockComponent.BlockActivationLagCompensationMultiplier"/>.
    /// </summary>
    private TimeSpan GetActivationGrace(EntityUid? holder, TimedDeflectBlockComponent block)
    {
        if (!_net.IsServer ||
            block.BlockActivationLagCompensationMultiplier <= 0f ||
            block.MaxBlockActivationLagCompensation <= 0f ||
            holder == null ||
            !_player.TryGetSessionByEntity(holder.Value, out var session))
        {
            return TimeSpan.Zero;
        }

        var graceSeconds = MathF.Min(
            block.MaxBlockActivationLagCompensation,
            session.Ping / RttPingDivisor * block.BlockActivationLagCompensationMultiplier);

        return TimeSpan.FromSeconds(graceSeconds);
    }

    private Vector2 GetDirectionToEntity(EntityUid from, EntityUid to)
    {
        if (!TryGetMapPosition(from, out var fromPosition) ||
            !TryGetMapPosition(to, out var toPosition))
        {
            return Vector2.Zero;
        }

        var direction = toPosition.Position - fromPosition.Position;
        return direction == Vector2.Zero
            ? Vector2.Zero
            : direction.Normalized();
    }

    private bool TryGetMapPosition(EntityUid entity, out MapCoordinates coordinates)
    {
        coordinates = default;

        if (TerminatingOrDeleted(entity) ||
            !TryComp<TransformComponent>(entity, out var xform))
        {
            return false;
        }

        coordinates = _transform.GetMapCoordinates((entity, xform));
        return true;
    }

    private void TryBackflip(EntityUid defender, TimedDeflectBlockComponent block)
    {
        if (!_net.IsServer ||
            block.BackflipChance <= 0f ||
            !_random.Prob(Math.Clamp(block.BackflipChance, 0f, 1f)))
        {
            return;
        }

        RaiseNetworkEvent(new FlipOnHitEvent(GetNetEntity(defender)), Filter.Pvs(defender, entityManager: EntityManager));
    }

    private int GetLevel(TimedDeflectBlockComponent block)
    {
        if (block.PowerPerLevel <= 0f)
            return 0;

        return Math.Clamp((int) (block.CurrentPower / block.PowerPerLevel), 0, block.MaxLevel);
    }

    private string GetVisualState(TimedDeflectBlockComponent block)
    {
        var level = GetLevel(block);
        return level <= 0
            ? block.BaseVisualState
            : $"{block.LevelVisualStatePrefix}{level}";
    }

    private string GetWieldedInhandState(string state)
    {
        return $"{state}-wielded";
    }
}
