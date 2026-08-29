using System.Numerics;
using Content.Pirate.Shared.EnergyDome;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.EnergyDome;

/// <summary>Scales the dome sprite to its physics radius.</summary>
public sealed class EnergyDomeVisualsSystem : VisualizerSystem<SpriteComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, SpriteComponent sprite, ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<float>(uid, EnergyDomeVisuals.Scale, out var scale, args.Component))
            return;

        SpriteSystem.SetScale((uid, sprite), new Vector2(scale, scale));
    }
}
