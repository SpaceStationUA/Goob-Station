// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Atmos.HFR;

/// <summary>
///     State sent to the HFR interface.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRBoundUserInterfaceState : BoundUserInterfaceState
{
    // Fusion mix
    public float FusionTemperature;
    public float FusionMoles;

    // Moderator mix
    public float ModeratorTemperature;
    public float ModeratorMoles;

    // Coolant / output
    public float CoolantTemperature;
    public float OutputTemperature;
    public bool OutputConnected;

    // Status
    public float PowerLevel;
    public float Integrity;
    public float IronContent;
    public float HeatOutput;
    public float HeatLimiter;
    public float Energy;
    public bool Endothermic;

    // Meltdown
    public bool MeltdownActive;
    public float MeltdownCountdown;

    // Switch states (per /tg/)
    public bool StartPower;
    public bool StartCooling;
    public bool StartFuel;
    public bool StartModerator;

    // Temperature rate of change (K/s) for the monitoring chart
    public float FusionTempDelta;
    public float ModeratorTempDelta;
    public float CoolantTempDelta;
    public float OutputTempDelta;

    // Gas breakdowns (moles) for the UI bars
    public Dictionary<Gas, float> FusionGases = [];
    public Dictionary<Gas, float> ModeratorGases = [];

    // Settings
    public float HeatingConductor;
    public float MagneticConstrictor;
    public float FuelInjectionRate;
    public float CurrentDampener;
    public float ModeratorInjectionRate;
    public bool WasteRemoval;
    public float ModeratorFilteringRate;
    public byte Recipe;
    public int ModeratorFilterId;
}

/// <summary>
///     Message to change the selected recipe.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetRecipeMessage(byte recipe) : BoundUserInterfaceMessage
{
    public byte Recipe = recipe;
}

/// <summary>
///     Message to change the heating conductor (50-500).
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetHeatingConductorMessage(float value) : BoundUserInterfaceMessage
{
    public float Value = value;
}

/// <summary>
///     Message to change the magnetic constrictor (50-1000).
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetMagneticConstrictorMessage(float value) : BoundUserInterfaceMessage
{
    public float Value = value;
}

/// <summary>
///     Message to change the fuel injection rate (0.5-150 mol/s).
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetFuelInjectionRateMessage(float value) : BoundUserInterfaceMessage
{
    public float Value = value;
}

/// <summary>
///     Message to change the current dampener (0-1000).
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetCurrentDampenerMessage(float value) : BoundUserInterfaceMessage
{
    public float Value = value;
}

/// <summary>
///     Message to change the moderator injection rate (0.5-150 mol/s).
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetModeratorInjectionRateMessage(float value) : BoundUserInterfaceMessage
{
    public float Value = value;
}

/// <summary>
///     Message to toggle waste removal.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRToggleWasteRemovalMessage(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
}

/// <summary>
///     Message to toggle the reactor power switch.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetPowerMessage(bool on) : BoundUserInterfaceMessage
{
    public bool On = on;
}

/// <summary>
///     Message to toggle the cooling switch.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetCoolingMessage(bool on) : BoundUserInterfaceMessage
{
    public bool On = on;
}

/// <summary>
///     Message to toggle the fuel injection switch.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetFuelSwitchMessage(bool on) : BoundUserInterfaceMessage
{
    public bool On = on;
}

/// <summary>
///     Message to toggle the moderator injection switch.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetModeratorSwitchMessage(bool on) : BoundUserInterfaceMessage
{
    public bool On = on;
}

/// <summary>
///     Message to set the moderator filtering rate (5-200 mol/s).
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetModeratorFilteringRateMessage(float value) : BoundUserInterfaceMessage
{
    public float Value = value;
}

/// <summary>
///     Message to set the moderator filter gas.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFRSetModeratorFilterMessage(int gasId) : BoundUserInterfaceMessage
{
    public int GasId = gasId;
}

/// <summary>
///     Message to trigger an emergency shutdown.
/// </summary>
[Serializable, NetSerializable]
public sealed class HFREmergencyShutdownMessage : BoundUserInterfaceMessage;

/// <summary>
///     Appearance keys used by the HFR visualizer.
/// </summary>
[Serializable, NetSerializable]
public enum HFRVisuals : byte
{
    /// <summary>
    ///     Current visual state: <see cref="HFRVisualState"/>.
    /// </summary>
    State
}

/// <summary>
///     Visual states of the HFR machine sprite.
/// </summary>
[Serializable, NetSerializable]
public enum HFRVisualState : byte
{
    /// <summary>
    ///     Reactor assembled but not running.
    /// </summary>
    Idle,

    /// <summary>
    ///     Reactor running (animated core).
    /// </summary>
    Active,

    /// <summary>
    ///     Reactor heavily damaged (cracked sprite).
    /// </summary>
    Broken,
}
