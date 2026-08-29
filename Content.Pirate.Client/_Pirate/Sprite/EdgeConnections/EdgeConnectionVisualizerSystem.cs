using Content.Pirate.Shared._Pirate.Sprite.EdgeConnections;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client._Pirate.Sprite.EdgeConnections;

public sealed class EdgeConnectionVisualizerSystem : VisualizerSystem<EdgeConnectionComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, EdgeConnectionComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        AppearanceSystem.TryGetData<EdgeConnectionDirections>(uid, EdgeConnectionVisuals.ConnectionMask, out _, args.Component);
    }
}
