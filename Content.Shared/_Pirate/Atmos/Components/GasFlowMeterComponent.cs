// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Atmos.Components;

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

[RegisterComponent, NetworkedComponent]
public sealed partial class GasFlowMeterComponent : Component;

[RegisterComponent]
public sealed partial class GasFlowMeterAttachableComponent : Component;

[Serializable, NetSerializable]
public enum GasFlowMeterVisuals : byte
{
    PressureState,
    TemperatureState,
}

[Serializable, NetSerializable]
public enum GasFlowMeterVisualLayers : byte
{
    Base,
    Pressure,
    Buttons,
}

[Serializable, NetSerializable]
public enum GasFlowMeterPressureState : byte
{
    Offline,
    Meter0,
    Meter1_1,
    Meter1_2,
    Meter1_3,
    Meter1_4,
    Meter1_5,
    Meter1_6,
    Meter2_1,
    Meter2_2,
    Meter2_3,
    Meter2_4,
    Meter2_5,
    Meter2_6,
    Meter3_1,
    Meter3_2,
    Meter3_3,
    Meter3_4,
    Meter3_5,
    Meter3_6,
    Meter4,
}

[Serializable, NetSerializable]
public enum GasFlowMeterTemperatureState : byte
{
    Gray,
    Violet,
    Blue,
    Cyan,
    Lime,
    Yellow,
    Orange,
    Red,
}
