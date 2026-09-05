// SPDX-License-Identifier: MIT
using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Overlays;

/// <summary>
/// Makes the entity see air temperature.
/// When added to a clothing item it will also grant the wearer the same overlay.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ThermalSightComponent : Component
{
    /// <summary>
    /// Whether the thermal overlay is enabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public EntProtoId? ToggleAction = "ActionToggleThermalSight";

    [DataField, NonSerialized]
    public EntityUid? ToggleActionEntity;

    [DataField]
    public SoundSpecifier? SoundOn;

    [DataField]
    public SoundSpecifier? SoundOff;
}

public sealed partial class ToggleThermalSightEvent : InstantActionEvent;

/// <summary>Appearance keys for the enabled and disabled item icon.</summary>
[Serializable, NetSerializable]
public enum ThermalSightVisual : byte
{
    Visual,
    On,
    Off,
}
