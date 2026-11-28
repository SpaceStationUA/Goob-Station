// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.CrystalMass;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.CrystalMass;

public sealed class CrystalMassVisualsSystem : VisualizerSystem<CrystalMassVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, CrystalMassVisualsComponent component, ref AppearanceChangeEvent args)
    {

        if (args.Sprite == null)
            return;
        if (AppearanceSystem.TryGetData<int>(uid, CrystalMassVisuals.Variant, out var variant, args.Component))
        {
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), component.Layer, $"crystal_cascade_{variant}");
            args.Sprite.LayerSetShader(component.Layer, "unshaded");
        }
    }
}
