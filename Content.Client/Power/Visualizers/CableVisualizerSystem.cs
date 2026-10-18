// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.SubFloor;
using Content.Shared.Wires;
using Robust.Client.GameObjects;

namespace Content.Client.Power.Visualizers;

public sealed class CableVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CableVisualizerComponent, AppearanceChangeEvent>(OnAppearanceChange, after: new[] { typeof(SubFloorHideSystem) });
    }

    private void OnAppearanceChange(EntityUid uid, CableVisualizerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Pirate: keep hidden cables current too; revealing a sprite need not change its appearance data.
        if (!_appearanceSystem.TryGetData<WireVisDirFlags>(uid, WireVisVisuals.ConnectedMask, out var mask, args.Component))
            mask = WireVisDirFlags.None;

        _sprite.LayerSetRsiState((uid, args.Sprite), 0, $"{component.StatePrefix}{(int)mask}");
        if (component.ExtraLayerPrefix != null)
            _sprite.LayerSetRsiState((uid, args.Sprite), 1, $"{component.ExtraLayerPrefix}{(int)mask}");
    }
}
