// SPDX-License-Identifier: MIT

using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.Humanoid;

public sealed partial class HumanoidAppearanceSystem
{
    private void UpdateExperimentEyes(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        if (ent.Comp1.Species != "Experiment")
            return;

        var layer = _sprite.LayerMapReserve((ent.Owner, ent.Comp2), HumanoidVisualLayers.Eyes);
        ent.Comp2[layer].Color = Color.White;
        ent.Comp2.LayerSetShader(layer, "unshaded");
    }
}
