using Content.Pirate.Shared.LightPaint;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.LightPaint;

/// <summary>
///     Tints the spray can's paint layer to whatever colour is currently loaded, so the
///     can visibly shows what it will spray.
/// </summary>
public sealed class LightPaintVisualizerSystem : VisualizerSystem<LightPaintComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, LightPaintComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !AppearanceSystem.TryGetData<Color>(uid, LightPaintVisuals.Color, out var color, args.Component))
            return;

        if (_sprite.LayerMapTryGet((uid, args.Sprite), LightPaintLayers.Paint, out var layer, false))
            _sprite.LayerSetColor((uid, args.Sprite), layer, color);
    }
}
