// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.DeepSpaceScanner;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.DeepSpaceScanner;

// The screen animation spans one scan interval, so use the server timestamp to keep it in sync.
public sealed partial class DeepSpaceScannerVisualizerSystem : VisualizerSystem<DeepSpaceScannerComponent>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void OnAppearanceChange(EntityUid uid, DeepSpaceScannerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<TimeSpan>(uid, DeepSpaceScannerVisuals.ScanStart, out var start, args.Component) ||
            !SpriteSystem.LayerMapTryGet((uid, args.Sprite), ToggleableVisuals.Layer, out var layer, false))
        {
            return;
        }

        var elapsed = _timing.CurTime - start;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        SpriteSystem.LayerSetAnimationTime((uid, args.Sprite), layer, (float) elapsed.TotalSeconds);
    }
}
