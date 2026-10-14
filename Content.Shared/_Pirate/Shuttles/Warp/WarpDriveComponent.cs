// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Shuttles.Warp;

/// <summary>
///     A warp drive: a machine that carries a hull across real distance rather than hopping through
///     bluespace.
/// </summary>
/// <remarks>
///     Unlike a bluespace drive, its range is not a fixed number. The field has to be spooled and then
///     held, heats up while it is held, and detonates if it is held too long.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WarpDriveComponent : Component
{
    /// <summary>
    ///     Where the drive is in its cycle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public WarpDriveState DriveState = WarpDriveState.Idle;

    /// <summary>
    ///     Core heat, 0 to 1. Climbs while engaged and falls while idle. At 1 the drive breaks down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Heat;

    /// <summary>
    ///     How long the grid must feed the drive before the field is up.
    /// </summary>
    [DataField]
    public TimeSpan SpoolTime = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     How long after a clean shutdown the drive refuses to spool again.
    /// </summary>
    [DataField]
    public TimeSpan CoolDownTime = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     How long after a flown jump the drive refuses to spool again.
    /// </summary>
    [DataField]
    public TimeSpan TransitCoolDownTime = TimeSpan.FromSeconds(600);

    /// <summary>
    ///     How long after a breakdown the drive refuses to spool again.
    /// </summary>
    [DataField]
    public TimeSpan BreakdownCoolDownTime = TimeSpan.FromSeconds(180);

    /// <summary>
    ///     The FTL radius granted to the grid while engaged, in tiles.
    /// </summary>
    [DataField]
    public float Range = 20_000f;

    /// <summary>
    ///     Change in core heat per second while engaged, as a fraction of the 0 to 1 scale.
    /// </summary>
    [DataField]
    public float HeatRiseRate = 1f / 120f;

    /// <summary>
    ///     Change in core heat per second while idle, as a fraction of the 0 to 1 scale.
    /// </summary>
    [DataField]
    public float HeatFallRate = 1f / 60f;

    /// <summary>
    ///     How long the drive tolerates losing supply before it shuts down.
    /// </summary>
    [DataField]
    public TimeSpan PowerGrace = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     When the console last toggled the drive.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastToggleTime = TimeSpan.Zero;

    /// <summary>
    ///     Minimum gap between two accepted toggle presses.
    /// </summary>
    [DataField]
    public TimeSpan ToggleDebounce = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     The last flicker step drawn, so each step is drawn once.
    /// </summary>
    [ViewVariables]
    public int LastFlickerStep;

    /// <summary>
    ///     When the current supply dip started. Zero when fed.
    /// </summary>
    [ViewVariables]
    public TimeSpan UnpoweredSince = TimeSpan.Zero;

    /// <summary>
    ///     Spool progress, 0 to 1. Advances with delivered power, not with the clock: a half-fed grid
    ///     spools at half speed.
    /// </summary>
    [ViewVariables]
    public float SpoolProgress;

    /// <summary>
    ///     When the drive may spool again. Zero when not on cooldown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CoolDownFinishTime = TimeSpan.Zero;

    /// <summary>
    ///     Draw while the drive is cold. Not zero on purpose, so a parked drive still shows on the HV
    ///     monitor and can be told apart from one that is not wired up.
    /// </summary>
    [DataField]
    public float IdlePowerDraw = 2_000f;

    /// <summary>
    ///     Draw while the spool is building. Above <see cref="WorkingPowerDraw"/>: the spool is the
    ///     engineering check, the hold is just the bill.
    /// </summary>
    [DataField]
    public float SpoolPowerDraw = 300_000f;

    /// <summary>
    ///     Draw while the field is up.
    /// </summary>
    /// <remarks>
    ///     Far above what an APC can supply (10 kW), so the drive has to be wired to HV, in practice a SMES.
    /// </remarks>
    [DataField]
    public float WorkingPowerDraw = 150_000f;

    /// <summary>
    ///     Share of the phase draw the net must deliver for the drive to count as fed. Not 1.0, because
    ///     pow3r ramps supply and 98% of demand is not a brownout.
    /// </summary>
    [DataField]
    public float RequiredPowerRatio = 0.9f;

    /// <summary>
    ///     Set by the power system. A drive that is not receiving its share cannot spool or stay engaged.
    /// </summary>
    [ViewVariables]
    public bool Powered;

    /// <summary>
    ///     Loop while the drive is spooling, engaged or in transit. The drive swaps its ambient sound
    ///     between this and <see cref="IdleLoopSound"/>.
    /// </summary>
    [DataField]
    public SoundPathSpecifier? ActiveLoopSound = new("/Audio/_Pirate/Effects/Shuttle/main_drive_loop.ogg");

    /// <summary>
    ///     Loop while the drive is cold.
    /// </summary>
    [DataField]
    public SoundPathSpecifier IdleLoopSound = new("/Audio/_Pirate/Effects/Shuttle/FTL_drive_hum.ogg");

    /// <summary>
    ///     Played once when the drive begins spooling.
    /// </summary>
    [DataField]
    public SoundPathSpecifier? SpoolUpSound = new("/Audio/_Pirate/Effects/Shuttle/main_drive_spoolup.ogg");

    /// <summary>
    ///     Played once when the drive shuts down cleanly.
    /// </summary>
    [DataField]
    public SoundPathSpecifier? SpoolDownSound = new("/Audio/_Pirate/Effects/Shuttle/main_drive_spooldown.ogg");

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";

    [DataField]
    public float TotalIntensity = 120f;

    [DataField]
    public float IntensitySlope = 2f;

    [DataField]
    public int MaxTileBreak = 8;

    /// <summary>
    ///     Journey sector laid in as this drive's course. Empty means nowhere. Only the journey rule reads it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string CourseSector = string.Empty;

    /// <summary>
    ///     The latest refusal or status line, shown on the console.
    /// </summary>
    [ViewVariables]
    public string Notice = string.Empty;
}
