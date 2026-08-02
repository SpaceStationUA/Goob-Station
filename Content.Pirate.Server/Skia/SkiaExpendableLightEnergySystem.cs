// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Skia;
using Content.Server.Light.Components;
using Content.Shared.Light.Components;

namespace Content.Pirate.Server.Skia;

public sealed class SkiaExpendableLightEnergySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpendableLightComponent, SkiaGetLightEnergyEvent>(OnGetLightEnergy);
    }

    private void OnGetLightEnergy(Entity<ExpendableLightComponent> entity, ref SkiaGetLightEnergyEvent args)
    {
        if (!TryComp<SkiaExpendableLightComponent>(entity, out var data))
            return;

        float lightFactor;
        switch (entity.Comp.CurrentState)
        {
            case ExpendableLightState.Lit:
                var timeElapsed = entity.Comp.GlowDuration.Seconds - entity.Comp.StateExpiryTime;
                lightFactor = MathF.Min(1f, 1f - timeElapsed / data.FadeInDuration);
                break;
            case ExpendableLightState.Fading:
                lightFactor = MathF.Min(1f, entity.Comp.StateExpiryTime / entity.Comp.FadeOutDuration.Seconds);
                break;
            default:
                return;
        }

        args.LightEnergy = data.LitEnergy * lightFactor;
        args.LightRadius = data.LitRadius * lightFactor;
    }
}
