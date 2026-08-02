// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Light.Components;
using Content.Pirate.Shared.Skia;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Skia;

public sealed class SkiaExpendableLightEnergySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpendableLightComponent, SkiaGetLightEnergyEvent>(OnGetLightEnergy);
    }

    private void OnGetLightEnergy(Entity<ExpendableLightComponent> entity, ref SkiaGetLightEnergyEvent args)
    {
        if (!TryComp<PointLightComponent>(entity, out var light))
            return;

        args.LightEnergy = light.Energy;
        args.LightRadius = light.Radius;
    }
}
