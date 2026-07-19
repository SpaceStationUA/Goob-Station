// SPDX-FileCopyrightText: 2026 kotobdev <59124164+kotobdev@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later OR MIT

using Content.Pirate.Shared.Avali.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Avali.EntitySystems;

/// <summary>
/// Colors feathers and their equipped clothing layers.
/// </summary>
public sealed class FeatherVisualizer : VisualizerSystem<FeatherComponent>
{
    [Dependency] private readonly ClothingSystem _clothing = default!;

    protected override void OnAppearanceChange(
        EntityUid uid,
        FeatherComponent component,
        ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<Color>(
                uid,
                FeatherVisuals.FeatherColor,
                out var featherColor,
                args.Component))
        {
            return;
        }

        SpriteSystem.LayerSetColor(uid, FeatherVisualLayers.Feather, featherColor);

        if (TryComp<ClothingComponent>(uid, out var clothing))
        {
            foreach (var slotPair in clothing.ClothingVisuals)
                _clothing.SetLayerColor(clothing, slotPair.Key, "feather", featherColor);
        }

        if (!AppearanceSystem.TryGetData<Color>(uid, FeatherVisuals.BloodColor, out var bloodColor, args.Component))
            return;

        SpriteSystem.LayerSetColor(uid, FeatherVisualLayers.Blood, bloodColor);
    }
}

public enum FeatherVisualLayers : byte
{
    Feather,
    Blood,
}
