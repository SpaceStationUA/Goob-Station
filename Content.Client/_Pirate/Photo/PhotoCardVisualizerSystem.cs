// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.IO;
using Content.Shared._Pirate.Photo;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Pirate.Photo;

public sealed class PhotoCardVisualizerSystem : VisualizerSystem<PhotoCardVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, PhotoCardVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<byte[]>(uid, PhotoCardVisuals.PreviewImage, out var previewData, args.Component) ||
            previewData.Length == 0)
        {
            _sprite.LayerSetVisible((uid, args.Sprite), PhotoCardVisualLayers.Preview, false);
            return;
        }

        try
        {
            using var stream = new MemoryStream(previewData);
            var texture = Texture.LoadFromPNGStream(stream, $"photo-preview-{uid}");
            _sprite.LayerSetTexture((uid, args.Sprite), PhotoCardVisualLayers.Preview, texture);
            _sprite.LayerSetVisible((uid, args.Sprite), PhotoCardVisualLayers.Preview, true);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to apply photo card preview texture for {ToPrettyString(uid)} on sprite {args.Sprite}: {ex}");
            _sprite.LayerSetVisible((uid, args.Sprite), PhotoCardVisualLayers.Preview, false);
        }
    }
}


