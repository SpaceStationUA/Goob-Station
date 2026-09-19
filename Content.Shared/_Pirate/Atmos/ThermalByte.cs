// SPDX-License-Identifier: MIT
using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Atmos;

[Serializable, NetSerializable]
public struct ThermalByte : IEquatable<ThermalByte>
{
    public const float TempMinimum = 0f;
    public const float TempMaximum = 1000f;
    public const int TempResolution = 250;

    public const byte ReservedFuture0 = 251;
    public const byte ReservedFuture1 = 252;
    public const byte ReservedFuture2 = 253;
    public const byte StateVacuum = 254;
    public const byte AtmosImpossible = 255;

    public const float TempDegreeResolution = (TempMaximum - TempMinimum) / TempResolution;
    public const float TempToByteFactor = TempResolution / (TempMaximum - TempMinimum);

    private byte _coreValue;

    public ThermalByte(float temperatureKelvin)
    {
        SetTemperature(temperatureKelvin);
    }

    public ThermalByte()
    {
        _coreValue = AtmosImpossible;
    }

    public void SetTemperature(float temperatureKelvin)
    {
        var clampedTemp = Math.Clamp(temperatureKelvin, TempMinimum, TempMaximum);
        _coreValue = (byte) ((clampedTemp - TempMinimum) * TempResolution / (TempMaximum - TempMinimum));
    }

    public void SetAtmosIsImpossible()
    {
        _coreValue = AtmosImpossible;
    }

    public void SetVacuum()
    {
        _coreValue = StateVacuum;
    }

    public bool IsAtmosImpossible => _coreValue == AtmosImpossible;
    public bool IsVacuum => _coreValue == StateVacuum;
    public byte Value => _coreValue;

    public readonly bool TryGetTemperature(out float temperature, bool onVacuumReturnTcmb = true)
    {
        switch (_coreValue)
        {
            case AtmosImpossible:
                temperature = 0f;
                return false;
            case StateVacuum when onVacuumReturnTcmb:
                temperature = Atmospherics.TCMB;
                return true;
            case StateVacuum:
                temperature = 0f;
                return false;
            // Reserved values are not temperatures.
            case ReservedFuture0:
            case ReservedFuture1:
            case ReservedFuture2:
                temperature = 0f;
                return false;
            default:
                temperature = (_coreValue * TempDegreeResolution) + TempMinimum;
                return true;
        }
    }

    public bool Equals(ThermalByte other)
    {
        return _coreValue == other._coreValue;
    }

    public static bool operator ==(ThermalByte left, ThermalByte right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ThermalByte left, ThermalByte right)
    {
        return !left.Equals(right);
    }

    public override bool Equals(object? obj)
    {
        return obj is ThermalByte other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _coreValue.GetHashCode();
    }
}
