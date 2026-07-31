// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Humanoid.Markings;
using Robust.Client.Graphics;

namespace Content.Client.Humanoid;

public sealed partial class SingleMarkingPicker
{
    private Texture? GetMarkingTexture(MarkingPrototype marking)
    {
        if (marking.Sprites.Count > 0)
            return _sprite.Frame0(marking.Sprites[0]);

        var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(marking.BodyPart);
        var parentPrototype = _markingPrototypeCache?.Values.FirstOrDefault(x => x.MarkingCategory == category);
        var sprite = parentPrototype?.Sprites.FirstOrDefault();

        return sprite == null ? null : _sprite.Frame0(sprite);
    }
}
