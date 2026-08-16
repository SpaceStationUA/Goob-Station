using Content.Shared._Pirate.Bed;
using Content.Shared._Pirate.Bed.Components;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Pirate.Bed;

public sealed class BedsheetCoverVisualizerSystem : VisualizerSystem<BedsheetCoverComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BedsheetCoverComponent, AfterAutoHandleStateEvent>(OnCoverState);
    }

    protected override void OnAppearanceChange(EntityUid uid, BedsheetCoverComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !AppearanceSystem.TryGetData<bool>(uid, BedsheetVisuals.Covered, out var covered, args.Component))
            return;

        SetDrawDepth(uid, args.Sprite, covered);
    }

    private void OnCoverState(Entity<BedsheetCoverComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        SetDrawDepth(ent.Owner, sprite, ent.Comp.Covered);
    }

    private void SetDrawDepth(EntityUid uid, SpriteComponent sprite, bool covered)
    {
        _sprite.SetDrawDepth((uid, sprite), covered
            ? (int) DrawDepth.OverMobs
            : (int) DrawDepth.SmallObjects);
    }
}
