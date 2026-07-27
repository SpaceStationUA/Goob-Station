using Content.Pirate.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Silicons.Borgs;

public sealed class BorgModuleLightingClientSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgLightingInstalledComponent, ComponentShutdown>(OnInstalledShutdown);
    }

    private void OnInstalledShutdown(Entity<BorgLightingInstalledComponent> ent, ref ComponentShutdown args)
    {
        ResetColors(ent);
    }

    private void ResetColors(EntityUid uid)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            if (sprite.LayerMapTryGet(BorgVisualLayers.Light, out var eyeLayer, false))
                sprite.LayerSetColor(eyeLayer, Color.White);

            if (sprite.LayerMapTryGet(BorgVisualLayers.LightStatus, out var statusLayer, false))
                sprite.LayerSetColor(statusLayer, Color.White);
        }

        if (TryComp<PointLightComponent>(uid, out var pointLight))
            _light.SetColor(uid, Color.White, pointLight);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<BorgLightingInstalledComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var installed, out var sprite))
        {
            if (installed.DiscoMode)
                continue;

            var color = installed.CurrentColor;

            if (sprite.LayerMapTryGet(BorgVisualLayers.Light, out var eyeLayer, false))
                sprite.LayerSetColor(eyeLayer, color);

            if (sprite.LayerMapTryGet(BorgVisualLayers.LightStatus, out var statusLayer, false))
                sprite.LayerSetColor(statusLayer, color);

            if (TryComp<PointLightComponent>(uid, out var pointLight))
                _light.SetColor(uid, color, pointLight);
        }
    }
}
