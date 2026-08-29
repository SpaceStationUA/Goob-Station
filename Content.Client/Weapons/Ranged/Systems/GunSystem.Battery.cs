// SPDX-License-Identifier: MIT

using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void InitializeBattery()
    {
        base.InitializeBattery();

        SubscribeLocalEvent<BatteryAmmoProviderComponent, UpdateAmmoCounterEvent>(OnAmmoCountUpdate);
        SubscribeLocalEvent<BatteryAmmoProviderComponent, AmmoCounterControlEvent>(OnControl);
    }

    private void OnAmmoCountUpdate(Entity<BatteryAmmoProviderComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        if (args.Control is not BoxesStatusControl boxes)
            return;

        // Pirate: multi-magazine guns can scale the effective power-cell fire cost.
        var multiplier = Math.Max(args.FireCostMultiplier, float.Epsilon);
        boxes.Update((int) (ent.Comp.ShotsFloat / multiplier), (int) (ent.Comp.CapacityFloat / multiplier));
    }

    private void OnControl(Entity<BatteryAmmoProviderComponent> ent, ref AmmoCounterControlEvent args)
    {
        args.Control = new BoxesStatusControl();
    }
}
