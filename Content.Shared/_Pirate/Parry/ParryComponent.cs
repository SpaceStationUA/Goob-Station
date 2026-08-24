// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Parry;

/// <summary>
/// Allows a held weapon to parry melee attacks and, when configured, reflect shots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ParryComponent : Component
{
    [DataField]
    public ReflectType Reflects = ReflectType.Energy | ReflectType.NonEnergy;

    [DataField, AutoNetworkedField]
    public float ParryExhaustionCost = 0.5f;

    [DataField, AutoNetworkedField]
    public float ReflectExhaustionCost = 1.1f;

    [DataField]
    public int ReflectMinSkill = 26;

    [DataField]
    public int ParryMinSkill = 26;

    [DataField]
    public Angle ReflectSpread = Angle.FromDegrees(140);

    [DataField]
    public SoundSpecifier? SoundOnReflect = new SoundPathSpecifier(
        "/Audio/Weapons/Guns/Hits/laser_sear_wall.ogg",
        AudioParams.Default.WithVariation(0.05f));

    [DataField]
    public SoundSpecifier? SoundOnParry = new SoundPathSpecifier(
        "/Audio/Weapons/Guns/Hits/laser_sear_wall.ogg",
        AudioParams.Default.WithVariation(0.05f));
}
