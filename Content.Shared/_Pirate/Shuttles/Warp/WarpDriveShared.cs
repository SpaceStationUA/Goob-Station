// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Shuttles.Warp;

/// <summary>
///     The warp drive's state machine.
/// </summary>
/// <remarks>
///     This is deliberately NOT the same shape as a bluespace drive. A bluespace drive
///     (<c>MachineFTLDrive</c> and friends) is a passive range extender: while it is powered it
///     widens the shuttle's FTL radius, and it cannot fail. The warp drive is an active machine
///     that has to be brought up, held, and taken down, and that can destroy itself.
/// </remarks>
[Serializable, NetSerializable]
public enum WarpDriveState : byte
{
    /// <summary>
    ///     Cold. The drive is offline and holds no cooldown.
    /// </summary>
    Idle,

    /// <summary>
    ///     Spooling up. Range is not yet granted, so bluespace FTL is still limited to whatever
    ///     the hull's bluespace drives provide.
    /// </summary>
    Spooling,

    /// <summary>
    ///     The warp field is up and range is granted. Heat climbs while this lasts.
    /// </summary>
    Engaged,

    /// <summary>
    ///     A jump is under way. Set by the travel system when the FTL machinery accepts the
    ///     jump; cleared when the grid arrives.
    /// </summary>
    InTransit,
}

[Serializable, NetSerializable]
public enum WarpDriveUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum WarpDriveVisuals : byte
{
    /// <summary>
    ///     The drive is engaged or in transit, and the coil is energised.
    /// </summary>
    /// <remarks>
    ///     Deliberately the only visual key. Spooling is shown by the console readout and by the
    ///     housing light coming up dim, because the sprite has no distinct "spooling" state and
    ///     faking one with a layer swap read as a flicker bug rather than as a ramp.
    /// </remarks>
    Active
}

/// <summary>
///     Sent to the server to spool the drive up or shut it down.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarpDriveToggleMessage : BoundUserInterfaceMessage
{
    public WarpDriveToggleMessage()
    {
    }
}

/// <summary>
///     The state the drive console renders.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarpDriveBuiState : BoundUserInterfaceState
{
    /// <summary>
    ///     Where the drive is in its cycle.
    /// </summary>
    public WarpDriveState DriveState;

    /// <summary>
    ///     Core heat, 0 to 1. Rises while <see cref="WarpDriveState.Engaged"/>; a breakdown is
    ///     imminent at 1.
    /// </summary>
    public float Heat;

    /// <summary>
    ///     Whether the grid is actually supplying the drive.
    /// </summary>
    public bool Powered;

    /// <summary>
    ///     What the drive is drawing right now, in watts.
    /// </summary>
    public float PowerDraw;

    /// <summary>
    ///     The grid's supply, in watts. Power draw at or above this means the drive is
    ///     browning out.
    /// </summary>
    public float PowerDrawMax;

    /// <summary>
    ///     Spool progress, 0 to 1. Only meaningful while <see cref="DriveState"/> is
    ///     <see cref="WarpDriveState.Spooling"/>.
    /// </summary>
    public float SpoolProgress;

    /// <summary>
    ///     Seconds of stable engagement left before the drive breaks down. Negative when not
    ///     applicable.
    /// </summary>
    public float StabilityRemaining;

    /// <summary>
    ///     Seconds of cooldown left before the drive can spool again. Negative when not on
    ///     cooldown.
    /// </summary>
    public float CoolDownRemaining;

    /// <summary>
    ///     The FTL radius the drive is currently granting, in tiles. Zero unless engaged, which
    ///     is what makes the drive's power draw and stability window matter.
    /// </summary>
    public float Range;

    public WarpDriveBuiState(
        WarpDriveState driveState,
        float heat,
        bool powered,
        float powerDraw,
        float powerDrawMax,
        float spoolProgress,
        float stabilityRemaining,
        float coolDownRemaining,
        float range)
    {
        DriveState = driveState;
        Heat = heat;
        Powered = powered;
        PowerDraw = powerDraw;
        PowerDrawMax = powerDrawMax;
        SpoolProgress = spoolProgress;
        StabilityRemaining = stabilityRemaining;
        CoolDownRemaining = coolDownRemaining;
        Range = range;
    }
}