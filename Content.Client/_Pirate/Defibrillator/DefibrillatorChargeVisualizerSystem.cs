// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Defibrillator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.Defibrillator;

/// <summary>
/// Renders the power cell charge bar and missing-cell overlay on belt defibrillators.
/// </summary>
public sealed class DefibrillatorChargeVisualizerSystem : VisualizerSystem<DefibrillatorChargeVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, DefibrillatorChargeVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<int>(uid, DefibrillatorChargeVisuals.ChargeLevel, out var level, args.Component)
            && level > 0)
        {
            var state = level switch
            {
                4 => "charge100",
                3 => "charge75",
                2 => "charge50",
                _ => "charge25",
            };
            _sprite.LayerSetRsiState((uid, args.Sprite), DefibrillatorChargeLayers.Charge, state);
            args.Sprite.LayerSetVisible(DefibrillatorChargeLayers.Charge, true);
        }
        else
        {
            args.Sprite.LayerSetVisible(DefibrillatorChargeLayers.Charge, false);
        }

        var noCell = AppearanceSystem.TryGetData<bool>(uid, DefibrillatorChargeVisuals.NoCell, out var missing, args.Component)
            && missing;
        args.Sprite.LayerSetVisible(DefibrillatorChargeLayers.NoCell, noCell);

        // Some belts (combat/NT) are emag-immune and don't define the Emagged layer at all.
        if (args.Sprite.LayerMapTryGet(DefibrillatorChargeLayers.Emagged, out _))
        {
            var emagged = AppearanceSystem.TryGetData<bool>(uid, DefibrillatorChargeVisuals.Emagged, out var isEmagged, args.Component)
                && isEmagged;
            args.Sprite.LayerSetVisible(DefibrillatorChargeLayers.Emagged, emagged);
        }
    }
}

/// <summary>
/// Sprite layers for the defibrillator charge overlays. Referenced from YAML via <c>map:</c> keys.
/// </summary>
public enum DefibrillatorChargeLayers : byte
{
    Charge,
    NoCell,
    Emagged,
}
