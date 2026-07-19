// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Nuclear.Turbine;

[Serializable, NetSerializable]
public enum TurbineUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class TurbinePartBuiData(string name, EntProtoId? prototypeId)
{
    public readonly string Name = name;
    public readonly EntProtoId? PrototypeId = prototypeId;
}

[Serializable, NetSerializable]
public sealed class TurbineBuiState(
    EntProtoId? turbinePrototypeId,
    float rpm,
    float bestRpm,
    float flowRate,
    float maximumFlowRate,
    float statorLoad,
    float generatedPower,
    float suppliedPower,
    bool overspeed,
    bool overtemp,
    bool stalling,
    bool undertemp,
    bool ruined,
    int bladeHealth,
    int maximumBladeHealth,
    TurbinePartBuiData? blade,
    TurbinePartBuiData? stator)
    : BoundUserInterfaceState
{
    public readonly EntProtoId? TurbinePrototypeId = turbinePrototypeId;
    public readonly float Rpm = rpm;
    public readonly float BestRpm = bestRpm;
    public readonly float FlowRate = flowRate;
    public readonly float MaximumFlowRate = maximumFlowRate;
    public readonly float StatorLoad = statorLoad;
    public readonly float GeneratedPower = generatedPower;
    public readonly float SuppliedPower = suppliedPower;
    public readonly bool Overspeed = overspeed;
    public readonly bool Overtemp = overtemp;
    public readonly bool Stalling = stalling;
    public readonly bool Undertemp = undertemp;
    public readonly bool Ruined = ruined;
    public readonly int BladeHealth = bladeHealth;
    public readonly int MaximumBladeHealth = maximumBladeHealth;
    public readonly TurbinePartBuiData? Blade = blade;
    public readonly TurbinePartBuiData? Stator = stator;
}

[Serializable, NetSerializable]
public sealed class TurbineChangeFlowRateMessage(float flowRate) : NuclearMachineBUIMessage
{
    public float FlowRate { get; } = flowRate;
}

[Serializable, NetSerializable]
public sealed class TurbineChangeStatorLoadMessage(float statorLoad) : NuclearMachineBUIMessage
{
    public float StatorLoad { get; } = statorLoad;
}
