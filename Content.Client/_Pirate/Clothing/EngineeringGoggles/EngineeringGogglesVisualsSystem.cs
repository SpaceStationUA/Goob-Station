// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Clothing.EngineeringGoggles;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.Clothing.EngineeringGoggles;

/// <summary>
/// Pirate: engineering goggles - swaps the item's own (dropped/inventory) sprite state to match its current
/// mode. The worn/in-hand layers are handled separately via ClothingComponent.EquippedPrefix.
/// </summary>
public sealed class EngineeringGogglesVisualsSystem : VisualizerSystem<EngineeringGogglesComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, EngineeringGogglesComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!AppearanceSystem.TryGetData<EngineeringGogglesMode>(uid, EngineeringGogglesVisuals.Mode, out var mode, args.Component))
            return;

        var state = mode switch
        {
            EngineeringGogglesMode.XRay => "icon-xray",
            EngineeringGogglesMode.Tray => "icon-tray",
            _ => "icon",
        };
        sprite.LayerSetState(0, state);
    }
}
