// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chemistry.Components;
using Content.Shared._Goobstation.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components; // Pirate: mixed syringe and ballistic ammunition
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Goobstation.Server.Weapons.Ranged;

/// <summary>
///     System for handling projectiles and altering their properties when fired from a Syringe Gun.
/// </summary>
public sealed class SyringeGunSystem : EntitySystem
{

    public override void Initialize()
    {
        SubscribeLocalEvent<SyringeGunComponent, AmmoShotEvent>(OnFire);
        SubscribeLocalEvent<SyringeGunComponent, AttemptShootEvent>(OnShootAttemot);
    }

    private void OnShootAttemot(Entity<SyringeGunComponent> ent, ref AttemptShootEvent args)
    {
        // Pirate: Omu's Pnuey must fire shells normally while launching loaded syringes.
        if (TryComp<BallisticAmmoProviderComponent>(ent.Owner, out var ammo))
        {
            if (ammo.Entities.Count > 0 && HasComp<SolutionInjectWhileEmbeddedComponent>(ammo.Entities[^1]))
                args.ThrowItems = true;
        }
        else
        {
            args.ThrowItems = true;
        }
    }

    private void OnFire(Entity<SyringeGunComponent> gun, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp(projectile, out SolutionInjectWhileEmbeddedComponent? whileEmbedded))
            {
                whileEmbedded.Injections = null; // uncap the injection maximum
                whileEmbedded.PierceArmorOverride = gun.Comp.PierceArmor;
                whileEmbedded.SpeedMultiplier = gun.Comp.InjectionSpeedMultiplier; // store it in the component to reset it
                whileEmbedded.UpdateInterval /= whileEmbedded.SpeedMultiplier;
            }
            if (TryComp(projectile, out SolutionInjectOnEmbedComponent? onEmbed))
                onEmbed.PierceArmorOverride = gun.Comp.PierceArmor;
        }
    }

}
