// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Shuttles.Warp;

/// <summary>
///     The warp drive's state machine. Unlike a bluespace drive, it has to be brought up, held and taken
///     down, and it can destroy itself.
/// </summary>
[Serializable, NetSerializable]
public enum WarpDriveState : byte
{
    /// <summary>
    ///     Cold. The drive is offline and has no cooldown.
    /// </summary>
    Idle,

    /// <summary>
    ///     Spooling up. No range is granted yet.
    /// </summary>
    Spooling,

    /// <summary>
    ///     The field is up and range is granted. Heat climbs while this lasts.
    /// </summary>
    Engaged,

    /// <summary>
    ///     A jump is under way. Set when the journey rule commits the drive, and held until the jump lands.
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
    Active
}

/// <summary>
///     Spools the drive up or shuts it down.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarpDriveToggleMessage : BoundUserInterfaceMessage
{
    public WarpDriveToggleMessage()
    {
    }
}

/// <summary>
///     Flies the laid-in course off a live field. The server checks everything and answers on the notice line.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarpDriveJumpMessage : BoundUserInterfaceMessage
{
    public WarpDriveJumpMessage()
    {
    }
}

/// <summary>
///     Lays in a course for the next jump.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarpDriveSetCourseMessage : BoundUserInterfaceMessage
{
    /// <summary>
    ///     Journey sector prototype to fly to.
    /// </summary>
    public readonly string Sector;

    public WarpDriveSetCourseMessage(string sector)
    {
        Sector = sector;
    }
}

/// <summary>
///     The state the drive console renders.
/// </summary>
[Serializable, NetSerializable]
public sealed class WarpDriveBuiState : BoundUserInterfaceState
{
    public WarpDriveState DriveState;

    /// <summary>
    ///     Core heat, 0 to 1.
    /// </summary>
    public float Heat;

    /// <summary>
    ///     Whether the grid is supplying the drive.
    /// </summary>
    public bool Powered;

    /// <summary>
    ///     What the drive is drawing right now, in watts.
    /// </summary>
    public float PowerDraw;

    /// <summary>
    ///     What the drive is asking for, in watts.
    /// </summary>
    public float PowerDrawMax;

    /// <summary>
    ///     Spool progress, 0 to 1.
    /// </summary>
    public float SpoolProgress;

    /// <summary>
    ///     Seconds until the core overheats at its current rate. Negative when not engaged.
    /// </summary>
    public float StabilityRemaining;

    /// <summary>
    ///     Seconds of cooldown left. Negative when not cooling down.
    /// </summary>
    public float CoolDownRemaining;

    /// <summary>
    ///     The FTL radius the drive is granting, in tiles. Zero unless engaged.
    /// </summary>
    public float Range;

    /// <summary>
    ///     What the grid can spare right now, in watts.
    /// </summary>
    public float GridSupply;

    /// <summary>
    ///     What a spool needs to see free, in watts.
    /// </summary>
    public float GridNeed;

    /// <summary>
    ///     Whether the grid can sustain a spool right now.
    /// </summary>
    public bool SpoolReady;

    /// <summary>
    ///     Name of the sector the ship is in, or empty when no journey is running.
    /// </summary>
    public string CurrentSystem;

    /// <summary>
    ///     Journey sector laid in as the next course. Empty means none.
    /// </summary>
    public string CourseSector;

    /// <summary>
    ///     The latest refusal or status line for the console.
    /// </summary>
    public string Notice;

    public WarpDriveBuiState(
        WarpDriveState driveState,
        float heat,
        bool powered,
        float powerDraw,
        float powerDrawMax,
        float spoolProgress,
        float stabilityRemaining,
        float coolDownRemaining,
        float range,
        string courseSector,
        string notice,
        float gridSupply,
        bool spoolReady,
        string currentSystem,
        float gridNeed)
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
        CourseSector = courseSector;
        Notice = notice;
        GridSupply = gridSupply;
        SpoolReady = spoolReady;
        CurrentSystem = currentSystem;
        GridNeed = gridNeed;
    }
}
