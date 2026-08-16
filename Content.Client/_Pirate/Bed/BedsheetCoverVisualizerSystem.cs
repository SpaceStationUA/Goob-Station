using Content.Shared._Pirate.Bed;
using Content.Shared._Pirate.Bed.Components;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Pirate.Bed;

public sealed class BedsheetCoverVisualizerSystem : VisualizerSystem<BedsheetCoverComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, BedsheetCoverComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !AppearanceSystem.TryGetData<bool>(uid, BedsheetVisuals.Covered, out var covered, args.Component))
            return;

        _sprite.SetDrawDepth((uid, args.Sprite), covered
            ? (int) DrawDepth.OverMobs
            : (int) DrawDepth.SmallObjects);
    }
}
