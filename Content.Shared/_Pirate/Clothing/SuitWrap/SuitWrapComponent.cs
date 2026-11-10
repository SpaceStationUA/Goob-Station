// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Clothing.SuitWrap;

[RegisterComponent]
public sealed partial class SuitWrapComponent : Component
{
    // Four colours are required; the darkest is only a blend target to prevent black rims.
    [DataField(required: true)]
    public List<Color> Ramp = new();

    [DataField]
    public Color Accent = Color.White;

    [DataField]
    public float AccentStrength = 0.5f;

    [DataField]
    public int SeamPeriod = 4;
}
