// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Pirate.Shared.Skia;

public abstract class SharedSkiaLightLevelHealthSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkiaLightLevelHealthComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<SkiaLightLevelDamageMultComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<SkiaLightLevelDamageMultComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    public int CurrentThreshold(float lightLevel, SkiaLightLevelHealthComponent comp)
    {
        var lowLight = lightLevel < comp.DarkThreshold;
        var highLight = lightLevel > comp.LightThreshold;
        return lowLight && !highLight ? -1 : !lowLight && highLight ? 1 : 0;
    }

    public void TryDealDamage(Entity<SkiaLightLevelHealthComponent> target, DamageSpecifier damage)
    {
        if (damage.AnyPositive())
            _audio.PlayPvs(target.Comp.SizzleSound, target);

        _damageable.TryChangeDamage(target.Owner, damage, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void OnRefreshMovementSpeed(
        Entity<SkiaLightLevelHealthComponent> entity,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<SkiaLightReactiveComponent>(entity, out var lightReactive))
            return;

        if (lightReactive.CurrentLightLevel < entity.Comp.DarkThreshold)
            args.ModifySpeed(entity.Comp.DarkMovementSpeedMultiplier);
        else if (lightReactive.CurrentLightLevel > entity.Comp.LightThreshold)
            args.ModifySpeed(entity.Comp.LightMovementSpeedMultiplier);
    }

    private void OnDamageModify(Entity<SkiaLightLevelDamageMultComponent> entity, ref DamageModifyEvent args)
    {
        if (!TryComp<SkiaLightLevelHealthComponent>(entity, out var lightHealth))
            return;

        args.Damage *= lightHealth.CurrentThreshold switch
        {
            -1 => entity.Comp.DarkReceivedMultiplier,
            1 => entity.Comp.LightReceivedMultiplier,
            _ => 1f,
        };

        var modifiers = lightHealth.CurrentThreshold switch
        {
            -1 => entity.Comp.DarkReceivedModifiers,
            1 => entity.Comp.LightReceivedModifiers,
            _ => null,
        };

        if (modifiers != null)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
    }

    private void OnGetMeleeDamage(Entity<SkiaLightLevelDamageMultComponent> entity, ref GetMeleeDamageEvent args)
    {
        if (!TryComp<SkiaLightLevelHealthComponent>(entity, out var lightHealth))
            return;

        args.Damage *= lightHealth.CurrentThreshold switch
        {
            -1 => entity.Comp.DarkDealtMultiplier,
            1 => entity.Comp.LightDealtMultiplier,
            _ => 1f,
        };

        var modifiers = lightHealth.CurrentThreshold switch
        {
            -1 => entity.Comp.DarkDealtModifiers,
            1 => entity.Comp.LightDealtModifiers,
            _ => null,
        };

        if (modifiers != null)
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
    }
}
