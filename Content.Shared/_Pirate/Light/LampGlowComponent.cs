// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Shared._Pirate.Light;

// Local space uses +Y for the wall and -Y for the lamp's beam.
[RegisterComponent]
public sealed partial class LampGlowComponent : Component
{
    [DataField]
    public bool Enabled = true;

    /// <summary>Bulb-center offset in lamp-local tiles.</summary>
    [DataField]
    public Vector2 Offset = new(0f, 0.44f);

    [DataField]
    public float Size = 1f;

    [DataField]
    public bool Cone = true;

    [DataField]
    public float Strength = 1f;
}
