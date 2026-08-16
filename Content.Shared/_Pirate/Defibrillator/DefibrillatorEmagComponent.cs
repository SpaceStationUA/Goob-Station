// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Allows a belt defibrillator to be emagged, disabling its safety protocols so it can shock living targets.
/// Each emag application toggles the state, mirroring SS13's <c>emag_act</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DefibrillatorEmagComponent : Component
{
    /// <summary>
    /// Whether the safety protocols are currently disabled (emagged).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool SafetyDisabled;

    /// <summary>
    /// Electrocution damage dealt to living targets while the safety is disabled.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int HarmDamage = 15;

    /// <summary>
    /// How long the target writhes from the harmful zap.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan WritheDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Sound played when the defibrillator is used offensively.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier ZapSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");

    /// <summary>
    /// How long the initial prep takes before the first offensive shock lands.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PrepDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Delay between each follow-up shock while the paddles stay on the target.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ChannelInterval = TimeSpan.FromSeconds(0.2);

    /// <summary>
    /// Electrocution damage dealt by each offensive shock.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int ChannelDamage = 10;

    /// <summary>
    /// Battery charge drained from the belt per offensive shock.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChannelChargeCost = 10;
}

/// <summary>
/// Raised on the belt defibrillator for each step of the emagged offensive channel:
/// the first step is preceded by the prep do-after, then follow-up shocks are chained
/// every <see cref="DefibrillatorEmagComponent.ChannelInterval"/> until the battery runs
/// dry, the target dies, or the user moves away / drops the paddles.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DefibrillatorEmagChannelDoAfterEvent : SimpleDoAfterEvent
{
}
