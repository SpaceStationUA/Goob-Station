// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Modifies incoming and outgoing damage according to Skia's current light threshold.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkiaLightLevelDamageMultComponent : Component
{
    [DataField]
    public float DarkReceivedMultiplier = 1f;

    [DataField]
    public float LightReceivedMultiplier = 1f;

    [DataField]
    public float LightDealtMultiplier = 1f;

    [DataField]
    public float DarkDealtMultiplier = 1f;

    [DataField]
    public DamageModifierSet? DarkReceivedModifiers;

    [DataField]
    public DamageModifierSet? LightReceivedModifiers;

    [DataField]
    public DamageModifierSet? DarkDealtModifiers;

    [DataField]
    public DamageModifierSet? LightDealtModifiers;
}
