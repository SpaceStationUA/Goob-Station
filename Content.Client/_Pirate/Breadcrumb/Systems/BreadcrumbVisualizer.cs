// SPDX-FileCopyrightText: 2017-2026 Space Wizards Federation
// SPDX-FileCopyrightText: 2024-2026 Starlight
// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Breadcrumb.Components;
using Robust.Client.GameObjects;
using Content.Shared.Light.Components;

namespace Content.Client._Pirate.Breadcrumb.Systems;

/// <summary>
/// Applies the set color to breadcrumbs.
/// </summary>
public sealed class BreadcrumbVisualizer : VisualizerSystem<BreadcrumbComponent>
{
    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<BreadcrumbComponent, PointLightComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var light, out var sprite))
        {
            if (!AppearanceSystem.TryGetData<ExpendableLightState>(uid, ExpendableLightVisuals.State, out var state) ||
                state != ExpendableLightState.Fading ||
                !SpriteSystem.TryGetLayer((uid, sprite), BreadcrumbVisualLayers.LitColor, out var glow, false))
                continue;

            SpriteSystem.LayerSetColor(glow, glow.Color.WithAlpha(Math.Clamp(light.Energy / 3.5f, 0f, 1f)));
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, BreadcrumbComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (!AppearanceSystem.TryGetData(uid, BreadcrumbVisualData.Color, out Color color))
            return;

        SpriteSystem.LayerSetColor(uid, BreadcrumbVisualLayers.UnlitColor, color);
        SpriteSystem.LayerSetColor(uid, BreadcrumbVisualLayers.LitColor, color);
    }
}
