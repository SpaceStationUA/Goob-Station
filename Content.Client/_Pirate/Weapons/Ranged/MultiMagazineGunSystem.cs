// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Pirate.Weapons.Ranged;

namespace Content.Client._Pirate.Weapons.Ranged;

public sealed class MultiMagazineGunSystem : SharedMultiMagazineGunSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, GunSystem.UpdateAmmoCounterEvent>(OnAmmoUpdate);
        SubscribeLocalEvent<MultiMagazineAmmoProviderComponent, GunSystem.AmmoCounterControlEvent>(OnAmmoControl);
    }

    private void OnAmmoUpdate(Entity<MultiMagazineAmmoProviderComponent> ent,
        ref GunSystem.UpdateAmmoCounterEvent args)
    {
        foreach (var (slotId, nested) in GetMagazineEntities(ent))
        {
            if (nested is not { } uid)
                continue;

            if (ent.Comp.Slots[slotId] is { } multiplier)
            {
                var update = new GunSystem.UpdateAmmoCounterEvent
                {
                    FireCostMultiplier = multiplier,
                    Control = args.Control,
                };
                RaiseLocalEvent(uid, update);
                continue;
            }

            RaiseLocalEvent(uid, args);
        }
    }

    private void OnAmmoControl(Entity<MultiMagazineAmmoProviderComponent> ent,
        ref GunSystem.AmmoCounterControlEvent args)
    {
        var nested = GetMagazineEntities(ent).Values.ToList();
        foreach (var uid in nested)
        {
            if (uid is { } actual)
                RaiseLocalEvent(actual, args);
        }

        if (args.Controls.Count < nested.Count)
            args.Control = new GunSystem.DefaultStatusControl();
    }
}
