using Content.Shared._Pirate.Coffeemaker.Components;
using Content.Shared.Chemistry;
using Content.Shared.Nutrition.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Coffeemaker;

public sealed class CoffeeCupVisualizerSystem : VisualizerSystem<CoffeeCupVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, CoffeeCupVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_sprite.LayerMapTryGet((uid, args.Sprite), component.Layer, out var layer, false))
            return;

        AppearanceSystem.TryGetData<bool>(uid, OpenableVisuals.Opened, out var opened, args.Component);
        AppearanceSystem.TryGetData<float>(uid, SolutionContainerVisuals.FillFraction, out var fillFraction, args.Component);

        var state = component.ClosedState;
        if (opened)
            state = fillFraction > component.FilledThreshold ? component.OpenedFilledState : component.OpenedEmptyState;

        _sprite.LayerSetRsiState((uid, args.Sprite), layer, state);
    }
}
