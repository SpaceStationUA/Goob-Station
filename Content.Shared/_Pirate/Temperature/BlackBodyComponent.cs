// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Temperature;

/// <summary>
/// Drives emissive sprite layers and a client-side point light from item temperature.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BlackBodyComponent : Component
{
    [DataField]
    public float MaxLightRadius = 1f;

    [ViewVariables]
    public Color Color;
}

[Serializable, NetSerializable]
public enum BlackBodyVisuals : byte
{
    Temperature,
}
