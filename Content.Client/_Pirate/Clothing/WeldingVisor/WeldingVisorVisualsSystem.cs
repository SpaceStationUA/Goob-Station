// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Clothing.WeldingVisor;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Clothing.WeldingVisor;

/// <summary>Pirate: welding visor - updates the item's sprite when the visor is toggled.</summary>
public sealed class WeldingVisorVisualsSystem : VisualizerSystem<WeldingVisorComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, WeldingVisorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, WeldingVisorVisuals.Lowered, out var lowered, args.Component))
            return;

        var state = lowered ? component.LoweredIconState : component.RaisedIconState;
        if (state is null)
            return;

        sprite.LayerSetState(0, state);
    }
}
