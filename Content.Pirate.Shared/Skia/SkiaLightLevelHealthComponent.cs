// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Skia;

/// <summary>
/// Applies healing or damage and movement modifiers based on Skia's ambient light level.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkiaLightLevelHealthComponent : Component
{
    [DataField]
    public float DarkThreshold = 0.2f;

    [DataField]
    public float LightThreshold = 0.8f;

    [DataField(required: true)]
    public DamageSpecifier DarkDamage = default!;

    [DataField(required: true)]
    public DamageSpecifier LightDamage = default!;

    [DataField]
    public bool HealWhenDead;

    [DataField]
    public float DarkMovementSpeedMultiplier = 1f;

    [DataField]
    public float LightMovementSpeedMultiplier = 1f;

    [DataField]
    public SoundSpecifier SizzleSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");

    [DataField]
    public int CurrentThreshold;
}
