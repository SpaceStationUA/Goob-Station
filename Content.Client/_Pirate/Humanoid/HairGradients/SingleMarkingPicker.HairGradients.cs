// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid.Markings;
using Robust.Client.Graphics;

namespace Content.Client.Humanoid;

public sealed partial class SingleMarkingPicker
{
    private Texture? GetMarkingTexture(MarkingPrototype marking)
    {
        return marking.Sprites.Count == 0 ? null : _sprite.Frame0(marking.Sprites[0]);
    }
}
