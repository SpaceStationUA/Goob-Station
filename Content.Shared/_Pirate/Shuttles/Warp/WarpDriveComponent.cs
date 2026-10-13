// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Shuttles.Warp;

/// <summary>
///     A warp drive: the machine that lets a hull cross real distance instead of stepping
///     sideways through bluespace.
/// </summary>
/// <remarks>
///     The whole reason this is not a <c>FTLDriveGeneratorComponent</c> with a large number in
///     it is that a big number has no failure mode. A bluespace drive grants its range for free
///     and cannot lose it. This one has to be spooled, has to be held, heats up while it is
///     held, and detonates if it is held too long — which is what makes "the star is going nova
///     and we have to get the field up in time" a round rather than a wait.
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
    ///     Core heat, 0 to 1. Climbs while engaged and bleeds off while idle. At 1 the drive
    ///     breaks down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Heat;

    /// <summary>
    ///     How long the grid must hold the drive before the warp field is up.
    /// </summary>
    [DataField]
    public TimeSpan SpoolTime = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     How long the drive can stay engaged before it breaks down. This is the ceiling on how
    ///     long a jump may be held open, and the reason a transit has to be committed to rather
    ///     than fiddled with.
    /// </summary>
    [DataField]
    public TimeSpan StableTime = TimeSpan.FromSeconds(45);

    /// <summary>
    ///     How long after a clean shutdown the drive refuses to spool again.
    /// </summary>
    [DataField]
    public TimeSpan CoolDownTime = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     How long the drive refuses to spool after a breakdown. Much longer than
    ///     <see cref="CoolDownTime"/>, because a breakdown is an incident.
    /// </summary>
    [DataField]
    public TimeSpan BreakdownCoolDownTime = TimeSpan.FromSeconds(180);

    /// <summary>
    ///     The FTL radius granted to the grid while engaged, in tiles. This is the number that
    ///     makes a warp feel like crossing distance rather than a longer teleport.
    /// </summary>
    [DataField]
    public float Range = 20_000f;

    /// <summary>
    ///     Degrees per second of core temperature change while engaged.
    /// </summary>
    /// <remarks>
    ///     Deliberately slower than <see cref="StableTime"/> would imply. When these were equal
    ///     the drive heated to its breakdown point at exactly the moment the stability window
    ///     expired, which made the two readouts the same event and left the crew nothing to
    ///     manage. Heat is now the "held far too long" indicator and the stability window is the
    ///     real budget, so the console shows a bar that still has somewhere to go.
    /// </remarks>
    [DataField]
    public float HeatRiseRate = 1f / 120f;

    /// <summary>
    ///     Degrees per second of core temperature change while idle.
    /// </summary>
    [DataField]
    public float HeatFallRate = 1f / 60f;

    /// <summary>
    ///     When the spool finishes. Zero when not spooling.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SpoolFinishTime = TimeSpan.Zero;

    /// <summary>
    ///     When the drive breaks down. Zero when not engaged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BreakdownTime = TimeSpan.Zero;

    /// <summary>
    ///     When the drive may spool again. Zero when not on cooldown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CoolDownFinishTime = TimeSpan.Zero;

    /// <summary>
    ///     When the drive was last brought up. Used to work out spool progress for the console.
    /// </summary>
    [DataField]
    public TimeSpan SpoolStartTime = TimeSpan.Zero;

    /// <summary>
    ///     Draw while the drive is cold. Not zero on purpose: a parked drive should still show up
    ///     on the HV monitor, so a crew can tell "plugged in but idle" from "not wired up at all".
    /// </summary>
    [DataField]
    public float IdlePowerDraw = 2_000f;

    /// <summary>
    ///     Draw while the field is up.
    /// </summary>
    /// <remarks>
    ///     This is high on purpose, and it is the reason the drive lives on HV. A bluespace drive
    ///     asks 3-6 kW, which any shuttle's APC supplies forever. This asks for enough that an APC
    ///     (10 kW max) cannot run it at all, so the drive has to be wired to real HV infrastructure
    ///     - a SMES, in practice. That makes engineering part of operating the drive rather than
    ///     something that happens elsewhere on the station.
    /// </remarks>
    [DataField]
    public float WorkingPowerDraw = 150_000f;

    /// <summary>
    ///     Fraction of <see cref="WorkingPowerDraw"/> the net must actually be delivering for the
    ///     drive to consider itself fed.
    /// </summary>
    /// <remarks>
    ///     Not 1.0, because pow3r ramps supply and a net sitting at 98% of demand is not a
    ///     brownout. The singularity emitter and the particle accelerator both use a ratio for the
    ///     same reason.
    /// </remarks>
    [DataField]
    public float RequiredPowerRatio = 0.9f;

    /// <summary>
    ///     Set by the power system. A drive that is not receiving its share cannot spool or stay
    ///     engaged.
    /// </summary>
    [ViewVariables]
    public bool Powered;

    /// <summary>
    ///     Looping sound while the drive is spooling, engaged, or in transit. The drive swaps
    ///     <c>AmbientSoundComponent.Sound</c> between this and <see cref="IdleLoopSound"/> rather
    ///     than running two streams, which is the pattern the bluespace drives use.
    /// </summary>
    /// <remarks>
    ///     Reuses the existing FTL hyperspace loop rather than shipping new audio. It is the right
    ///     sound for "the ship is in transit" and it is already licensed and attributed. Proper warp
    ///     audio is a content task, not a systems one.
    /// </remarks>
    [DataField]
    public SoundPathSpecifier? ActiveLoopSound = new("/Audio/Effects/Shuttle/hyperspace_progress.ogg");

    /// <summary>
    ///     Looping sound while the drive is cold. The engine hum is deliberately near-inaudible:
    ///     it exists so a parked drive is identifiable from its sound alone, not to be noticed.
    /// </summary>
    [DataField]
    public SoundPathSpecifier IdleLoopSound = new("/Audio/Ambience/Objects/engine_hum.ogg");

    /// <summary>
    ///     Played once when the drive begins spooling.
    /// </summary>
    [DataField]
    public SoundPathSpecifier? SpoolUpSound = new("/Audio/Effects/Shuttle/hyperspace_begin.ogg");

    /// <summary>
    ///     Played once when the drive shuts down cleanly.
    /// </summary>
    [DataField]
    public SoundPathSpecifier? SpoolDownSound = new("/Audio/Effects/Shuttle/hyperspace_end.ogg");

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";

    [DataField]
    public float TotalIntensity = 120f;

    [DataField]
    public float IntensitySlope = 2f;

    [DataField]
    public int MaxTileBreak = 8;

    /// <summary>
    ///     Whether the grid is allowed to jump through this drive. Set false while a jump is in
    ///     progress so nothing can queue a second one behind the first.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ReadyForJump = true;
}