// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;

namespace Content.Pirate.Shared.Skia;

public sealed class SkiaItemTogglePointLightEnergySystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemTogglePointLightComponent, SkiaGetLightEnergyEvent>(OnGetLightEnergy);
    }

    private void OnGetLightEnergy(Entity<ItemTogglePointLightComponent> entity, ref SkiaGetLightEnergyEvent args)
    {
        if (!_light.TryGetLight(entity.Owner, out var light) || !light.Enabled)
            return;

        args.LightEnergy = light.Energy;
        args.LightRadius = light.Radius;
    }
}
