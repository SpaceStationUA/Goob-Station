// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Nuclear.Reactor;

[Serializable, NetSerializable]
public enum NuclearReactorUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class NuclearReactorBuiState(
    ReactorSlotBUIData[] data,
    int gridWidth,
    int gridHeight,
    float temperature,
    float meltdownTemperature,
    float radiationLevel,
    float maximumRadiation,
    float thermalPower,
    float maximumThermalPower,
    float controlRodInsertion,
    float averageControlRodInsertion,
    string? partSlotItemName)
    : BoundUserInterfaceState
{
    public readonly ReactorSlotBUIData[] SlotData = data;
    public readonly int GridWidth = gridWidth;
    public readonly int GridHeight = gridHeight;
    public readonly float Temperature = temperature;
    public readonly float MeltdownTemperature = meltdownTemperature;
    public readonly float RadiationLevel = radiationLevel;
    public readonly float MaximumRadiation = maximumRadiation;
    public readonly float ThermalPower = thermalPower;
    public readonly float MaximumThermalPower = maximumThermalPower;
    public readonly float ControlRodInsertion = controlRodInsertion;
    public readonly float AverageControlRodInsertion = averageControlRodInsertion;
    public readonly string? PartSlotItemName = partSlotItemName;
}

[Serializable, NetSerializable]
public sealed class ReactorSlotBUIData
{
    public bool HasPart;
    public string? PartName;
    public string IconStateInserted = "base";

    public double Temperature = 0;
    public int NeutronCount = 0;

    public float NeutronRadioactivity = 0f;
    public float Radioactivity = 0f;
    public float SpentFuel = 0f;
}

/// <summary>
/// Message to swap a reactor part at a position with the reactor' part itemslot.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReactorSwapPartMessage(Vector2i position) : BoundUserInterfaceMessage
{
    public Vector2i Position { get; } = position;
}

/// <summary>
/// Message to eject the reactor's part itemslot.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReactorEjectItemMessage : BoundUserInterfaceMessage;

/// <summary>
/// Message to change the control rods insertion target by adding/subtracing a value to it.
/// </summary>
[Serializable, NetSerializable]
public sealed class ReactorAdjustControlRodsMessage(float change) : NuclearMachineBUIMessage
{
    public float Change { get; } = change;
}
