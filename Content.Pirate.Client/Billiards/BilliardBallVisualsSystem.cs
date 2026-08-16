// SPDX-License-Identifier: MIT

using Content.Pirate.Shared.Billiards;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Billiards;

public sealed class BilliardBallVisualsSystem : VisualizerSystem<BilliardBallComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(
        EntityUid uid,
        BilliardBallComponent component,
        ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (AppearanceSystem.TryGetData<Color>(uid, BilliardVisuals.Color, out var color, args.Component) &&
            _sprite.LayerMapTryGet((uid, sprite), BilliardVisualLayers.Base, out var baseLayer, false))
        {
            _sprite.LayerSetColor((uid, sprite), baseLayer, color);
        }

        if (AppearanceSystem.TryGetData<bool>(uid, BilliardVisuals.Stripe, out var hasStripe, args.Component) &&
            _sprite.LayerMapTryGet((uid, sprite), BilliardVisualLayers.Stripe, out var stripeLayer, false))
        {
            _sprite.LayerSetVisible((uid, sprite), stripeLayer, hasStripe);
        }
    }
}
