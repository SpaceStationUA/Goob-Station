using Content.Shared._Pirate.Furniture.Tables.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Furniture.Tables;

public sealed class RouletteVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RouletteComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, RouletteComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData(uid, RouletteVisuals.State, out RouletteState state))
            state = RouletteState.Idle;

        switch (state)
        {
            case RouletteState.Idle:
                SetState(uid, args.Sprite, "idle");
                break;
            case RouletteState.Rolling:
                SetState(uid, args.Sprite, "roll");
                break;
            case RouletteState.Result:
                SetState(uid, args.Sprite, "idle");
                break;
        }
    }

    private void SetState(EntityUid uid, SpriteComponent sprite, string state)
    {
        Entity<SpriteComponent?> entity = (uid, sprite);
        _sprite.LayerSetAutoAnimated(entity, RouletteVisualLayers.Base, state == "roll");
        _sprite.LayerSetRsiState(entity, RouletteVisualLayers.Base, state);
    }
}
