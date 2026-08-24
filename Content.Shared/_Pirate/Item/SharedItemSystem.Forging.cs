// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Item;

public abstract partial class SharedItemSystem
{
    public void SetInhandLayerColorByShader(
        Entity<ItemComponent?> ent,
        string shader,
        Color color)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var layers in ent.Comp.InhandVisuals.Values)
        {
            foreach (var layer in layers)
            {
                if (layer.Shader == shader)
                    layer.Color = color;
            }
        }

        VisualsChanged(ent.Owner);
    }

    public void SetRsiPath(Entity<ItemComponent?> ent, string path)
    {
        if (!Resolve(ent, ref ent.Comp) || ent.Comp.RsiPath == path)
            return;

        ent.Comp.RsiPath = path;
        VisualsChanged(ent.Owner);
    }
}
