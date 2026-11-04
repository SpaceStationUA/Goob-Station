// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DisplacementMap;
using System.Numerics; // Pirate: preserve the aspect ratio of rectangular sprite frames.
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.DisplacementMap;

public sealed class DisplacementMapSystem : EntitySystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>
    /// Attempting to apply a displacement map to a specific layer of SpriteComponent
    /// </summary>
    /// <param name="data">Information package for applying the displacement map</param>
    /// <param name="sprite">Entity with SpriteComponent</param>
    /// <param name="index">Index of the layer where the new map layer will be added</param>
    /// <param name="key">Unique layer key, which will determine which layer to apply displacement map to</param>
    /// <param name="displacementKey">The key of the new displacement map layer added by this function.</param>
    /// <returns></returns>
    public bool TryAddDisplacement(DisplacementData data,
        Entity<SpriteComponent> sprite,
        int index,
        object key,
        out string displacementKey)
    {
        displacementKey = $"{key}-displacement";

        if (key.ToString() is null)
            return false;

        var shaderOverride = data.ShaderOverride;
        // Pirate: keep emissive clothing layers unshaded while applying species displacement.
        if (shaderOverride != null
            && sprite.Comp[index] is SpriteComponent.Layer { ShaderPrototype: { } shader }
            && shader.Id == "unshaded")
        {
            shaderOverride = "DisplacedUnshadedDraw";
        }

        if (shaderOverride != null)
            sprite.Comp.LayerSetShader(index, shaderOverride);
        // Pirate end

        _sprite.RemoveLayer(sprite.AsNullable(), displacementKey, false);

        //allows you not to write it every time in the YML
        foreach (var pair in data.SizeMaps)
        {
            pair.Value.CopyToShaderParameters ??= new()
            {
                LayerKey = "dummy",
                ParameterTexture = "displacementMap",
                ParameterUV = "displacementUV",
            };
        }

        if (!data.SizeMaps.ContainsKey(32))
        {
            Log.Error($"DISPLACEMENT: {displacementKey} don't have 32x32 default displacement map");
            return false;
        }

        // We choose a displacement map from the possible ones, matching the size with the original layer size.
        // If there is no such a map, we use a standard 32 by 32 one
        var displacementDataLayer = data.SizeMaps[EyeManager.PixelsPerMeter];
        var mapSize = EyeManager.PixelsPerMeter; // Pirate: fallback for displacement layers without an RSI.
        var actualRSI = _sprite.LayerGetEffectiveRsi(sprite.AsNullable(), index);
        if (actualRSI is not null)
        {
            var layerSize = actualRSI.Size.X;
            if (data.SizeMaps.TryGetValue(layerSize, out var map))
            {
                displacementDataLayer = map;
                mapSize = layerSize; // Pirate
            }
        }

        var displacementLayer = _serialization.CreateCopy(displacementDataLayer, notNullableOverride: true);
        displacementLayer.CopyToShaderParameters!.LayerKey = key.ToString() ?? "this is impossible";

        var targetLayer = sprite.Comp[index] as SpriteComponent.Layer; // Pirate: adding the map moves the target index.
        _sprite.AddLayer(sprite.AsNullable(), displacementLayer, index);
        _sprite.LayerMapSet(sprite.AsNullable(), displacementKey, index);

        // Pirate: rectangular markings use a centered map without stretching its vertical features.
        // Keep shader parameters local to this layer, including the map's texel size for atlas-safe sampling.
        if (actualRSI != null && targetLayer?.Shader is { } instance &&
            targetLayer.ShaderPrototype?.Id is "DisplacedDraw" or "DisplacedStencilDraw" or "DisplacedUnshadedDraw")
        {
            var mapRsi = _sprite.LayerGetEffectiveRsi(sprite.AsNullable(), index);
            var size = mapRsi?.Size ?? new Vector2i(mapSize, mapSize);
            var scale = new Vector2(1f, (float) actualRSI.Size.Y * size.X / (actualRSI.Size.X * size.Y));
            if (!instance.Mutable)
                targetLayer.Shader = instance = instance.Duplicate();
            instance.SetParameter("displacementMapScale", scale);
            instance.SetParameter("displacementMapTexelSize", new Vector2(1f / size.X, 1f / size.Y));
        }

        return true;
    }

    /// <inheritdoc cref="TryAddDisplacement"/>
    [Obsolete("Use the Entity<SpriteComponent> overload")]
    public bool TryAddDisplacement(DisplacementData data,
        SpriteComponent sprite,
        int index,
        object key,
        out string displacementKey)
    {
        return TryAddDisplacement(data, (sprite.Owner, sprite), index, key, out displacementKey);
    }
}
