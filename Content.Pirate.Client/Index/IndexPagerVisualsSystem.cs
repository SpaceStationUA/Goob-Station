// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Index;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Index;

/// <summary>
///     Plays the receiving animation on the pager item while a prescription arrives.
/// </summary>
public sealed partial class IndexPagerVisualsSystem : VisualizerSystem<IndexPagerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, IndexPagerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        var entity = new Entity<SpriteComponent?, AppearanceComponent>(uid, sprite, args.Component);
        var receiving = AppearanceSystem.TryGetData<bool>(entity, IndexPagerVisuals.Receiving, out var value, entity)
                        && value;

        if (!SpriteSystem.LayerMapTryGet((uid, sprite), IndexPagerVisualLayers.Icon, out var layer, false))
            return;

        SpriteSystem.LayerSetRsiState((uid, sprite), layer, receiving ? "prescription" : "icon");
        SpriteSystem.LayerSetVisible((uid, sprite), layer, true);
    }
}
