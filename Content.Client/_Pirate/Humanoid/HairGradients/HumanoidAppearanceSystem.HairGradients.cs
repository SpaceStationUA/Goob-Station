// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client.Humanoid;

public sealed partial class HumanoidAppearanceSystem
{
    private bool TryGetParentMarking(MarkingPrototype markingPrototype,
        HumanoidAppearanceComponent humanoid,
        [NotNullWhen(true)] out SpriteSpecifier.Rsi? rsi,
        [NotNullWhen(true)] out string? layerId)
    {
        rsi = null;
        layerId = null;

        var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(markingPrototype.BodyPart);
        var marking = humanoid.MarkingSet.Markings.GetValueOrDefault(category)?.FirstOrDefault();

        if (marking == null || !_markingManager.Markings.TryGetValue(marking.MarkingId, out var prototype))
            return false;

        rsi = prototype.Sprites.FirstOrDefault() as SpriteSpecifier.Rsi;
        if (rsi == null)
            return false;

        layerId = $"{prototype.ID}-{rsi.RsiState}";
        return true;
    }

    private bool HasParentShaderMarking(HumanoidVisualLayers bodyPart, MarkingSet markingSet)
    {
        foreach (var markings in markingSet.Markings.Values)
        {
            foreach (var marking in markings)
            {
                if (_markingManager.TryGetMarking(marking, out var prototype) &&
                    prototype.BodyPart == bodyPart &&
                    prototype.Sprites.Count == 0 &&
                    prototype.Shader != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void TryRemoveParentShader(
        MarkingPrototype prototype,
        Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        if (prototype.Sprites.Count == 0 &&
            prototype.Shader != null &&
            TryGetParentMarking(prototype, entity.Comp1, out _, out var layerId) &&
            _sprite.LayerMapTryGet((entity, entity.Comp2), layerId, out var layer, false))
        {
            entity.Comp2.LayerSetShader(layer, null, null);
        }
    }

    private void TryApplyParentShader(
        MarkingPrototype markingPrototype,
        int targetLayer,
        Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        if (markingPrototype.Sprites.Count != 0 || markingPrototype.Shader is not { } shaderPrototype)
            return;

        var humanoid = entity.Comp1;
        var sprite = entity.Comp2;
        Entity<SpriteComponent?> spriteEntity = (entity, sprite);

        var shader = _prototypeManager.Index<ShaderPrototype>(shaderPrototype).InstanceUnique();
        if (markingPrototype.Coloring.Layers is { } layers)
        {
            foreach (var (key, definition) in layers)
            {
                shader.SetParameter(key,
                    definition.GetColor(humanoid.SkinColor, humanoid.EyeColor, humanoid.MarkingSet).RGBA);
            }
        }

        if (!TryGetParentMarking(markingPrototype, humanoid, out var rsi, out var layerId))
        {
            sprite.LayerSetShader(targetLayer, shader, shaderPrototype);
            return;
        }

        if (!_sprite.LayerMapTryGet(spriteEntity, layerId, out var layer, false))
        {
            layer = _sprite.AddLayer(spriteEntity, rsi, targetLayer + 1);
            _sprite.LayerMapSet(spriteEntity, layerId, layer);
        }

        sprite.LayerSetShader(layerId, shader, shaderPrototype);
    }
}
