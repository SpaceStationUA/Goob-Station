using System;
using Content.Shared._Pirate.ZLevels.Elevators;
using Content.Shared._Pirate.ZLevels.Elevators.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.ZLevels.Elevators;

/// <summary>
/// Draws the cab's floor number by swapping the digit sprite layer (lift_indo-num0..9) from the
/// <see cref="ElevatorIndicatorVisuals.Floor"/> appearance value. Pre-rendered sprites are used
/// instead of maptext (SS13's approach), since SS14 maptext doesn't scale with the world.
/// </summary>
public sealed class ElevatorIndicatorVisualizerSystem : VisualizerSystem<ElevatorIndicatorComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, ElevatorIndicatorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, ElevatorIndicatorVisuals.Floor, out var floor, args.Component))
            return;

        // Single-digit readout; floors above 9 show the ones digit.
        var digit = Math.Abs(floor) % 10;
        _sprite.LayerSetRsiState((uid, args.Sprite), ElevatorVisualLayers.Digit, $"lift_indo-num{digit}");
    }
}
