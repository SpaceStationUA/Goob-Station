using Robust.Client.GameObjects;

namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothSystem
{
    private static readonly CornerLayers[] AllCornerLayers = [CornerLayers.SE, CornerLayers.NE, CornerLayers.NW, CornerLayers.SW];

    // Layer keys are private to IconSmooth, so other systems toggle corners through this helper.
    public void SetCornersVisible(Entity<SpriteComponent?> ent, bool visible)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var key in AllCornerLayers)
        {
            if (_sprite.LayerMapTryGet(ent, key, out var layer, false))
                _sprite.LayerSetVisible(ent, layer, visible);
        }
    }
}
