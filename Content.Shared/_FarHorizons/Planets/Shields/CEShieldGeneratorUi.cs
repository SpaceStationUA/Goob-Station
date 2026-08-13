/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Planets.Shields;

/// <seealso cref="CEShieldGeneratorComponent"/>
[Serializable, NetSerializable]
public enum CEShieldGeneratorUiKey : byte
{
    Key,
}

/// <summary>
/// Server → client snapshot for the generator status window. The window reports where the
/// cycle is and carries the master on/off switch (see <see cref="CEShieldGeneratorToggleMessage"/>).
/// </summary>
[Serializable, NetSerializable]
public sealed class CEShieldGeneratorBuiState : BoundUserInterfaceState
{
    public CEShieldGeneratorStage Stage;

    /// <summary>Joules banked / buffer size.</summary>
    public float Charge;
    public float MaxCharge;

    /// <summary>Watts actually arriving vs. watts currently wanted from the grid.</summary>
    public float ReceivedPower;
    public float WantedPower;

    /// <summary>
    /// Seconds until the next lifecycle event (buffer full / beam fire / field collapse /
    /// cooldown end), or negative when nothing is pending.
    /// </summary>
    public float EtaSeconds = -1f;

    /// <summary>
    /// Whether the generator is standing on a map that belongs to a planet's z-network.
    /// False means the whole cycle is locked out: no draw, no charging, no beam.
    /// </summary>
    public bool OnPlanet = true;

    /// <summary>Master switch: when off the generator draws nothing and the cycle is parked.</summary>
    public bool Enabled = true;
}

/// <summary>
/// Client → server: flip the generator's master switch. Turning it off drops any
/// in-flight spinup or active field immediately and parks the cycle in Charging.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEShieldGeneratorToggleMessage : BoundUserInterfaceMessage
{
    public bool Enabled;
}
