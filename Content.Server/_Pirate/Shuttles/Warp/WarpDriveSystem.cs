// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Events;
using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Explosion.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Shuttles.Warp;

/// <summary>
///     Drives the warp drive's state machine.
/// </summary>
/// <remarks>
///     The drive's whole job is to make the hull's FTL radius something the crew has to earn rather
///     than a property the hull happens to have. While it is spooling or engaged it publishes
///     <see cref="WarpDriveGridComponent"/> on its grid, and that is what
///     <c>SharedShuttleSystem.GetFTLRange</c> reads. When the drive goes down the range goes with
///     it, so a ship caught mid-scenario with a cold drive is a ship that cannot jump.
///
///     It runs on HV (<c>PowerConsumerComponent</c>) rather than an APC. That is the whole point of
///     the power budget: an APC tops out at 10 kW and this asks for 150, so the drive cannot be
///     plugged into a shuttle and forgotten. It has to be wired to a SMES, which means somebody
///     has to build something first.
/// </remarks>
public sealed class WarpDriveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>
    ///     How often the console is refreshed. Fast enough that a countdown does not visibly
    ///     stutter, slow enough that a room full of consoles is not a per-frame cost.
    /// </summary>
    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(500);

    private TimeSpan _nextUiUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarpDriveComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WarpDriveComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WarpDriveComponent, AnchorStateChangedEvent>(OnAnchored);

        // HV supply reports through PowerConsumerReceivedChanged, not PowerChangedEvent - the
        // latter is the APC path and never fires for a machine that is not on an APC.
        SubscribeLocalEvent<WarpDriveComponent, PowerConsumerReceivedChanged>(OnReceivedChanged);

        SubscribeLocalEvent<WarpDriveComponent, WarpDriveToggleMessage>(OnToggleMessage);
        SubscribeLocalEvent<WarpDriveComponent, ExaminedEvent>(OnExamined);

        // A jump is the FTL machinery's business, so the drive watches it rather than running its
        // own. Both events are raised on the grid.
        SubscribeLocalEvent<WarpDriveGridComponent, FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<WarpDriveGridComponent, FTLCompletedEvent>(OnFTLCompleted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<WarpDriveComponent>();
        while (query.MoveNext(out var uid, out var drive))
        {
            if (drive.DriveState == WarpDriveState.Idle)
                continue;

            // Losing supply mid-field goes through the same path as the stability breakdown. The
            // distinction between "someone cut a cable" and "the net was never big enough" is made
            // by when it happens, not by a separate code path - see ShutDown's callers.
            if (!drive.Powered && drive.DriveState != WarpDriveState.InTransit)
            {
                ShutDown((uid, drive), drive.BreakdownCoolDownTime);
                continue;
            }

            switch (drive.DriveState)
            {
                case WarpDriveState.Spooling:
                    if (curTime >= drive.SpoolFinishTime)
                        FinishSpooling((uid, drive));
                    break;

                case WarpDriveState.Engaged:
                case WarpDriveState.InTransit:
                    // Heat climbs in both, so a long transit eats into the stability window. This
                    // is what stops "warp somewhere enormous" from being a free action.
                    drive.Heat = MathF.Min(drive.Heat + drive.HeatRiseRate * frameTime, 1f);
                    if (curTime >= drive.BreakdownTime || drive.Heat >= 1f)
                        BreakDown((uid, drive));
                    break;

                default:
                    drive.Heat = MathF.Max(drive.Heat - drive.HeatFallRate * frameTime, 0f);
                    break;
            }
        }

        if (curTime < _nextUiUpdate)
            return;

        _nextUiUpdate = curTime + UiUpdateInterval;
        UpdateConsoles();
    }

    #region Lifecycle

    private void OnInit(Entity<WarpDriveComponent> ent, ref ComponentInit args)
    {
        // Ask for the idle draw up front so the drive is visible on the HV monitor before anyone
        // touches it, rather than looking identical to a machine that is not wired up.
        SetDrawRate(ent, ent.Comp.IdlePowerDraw);
        UpdatePowered(ent);
        SyncGrid(ent);
    }

    private void OnShutdown(Entity<WarpDriveComponent> ent, ref ComponentShutdown args)
    {
        // The grid outlives the machine, so the range has to be withdrawn explicitly or the hull
        // keeps a warp bubble around a drive that no longer exists.
        ClearGrid(ent);
    }

    private void OnAnchored(Entity<WarpDriveComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
        {
            SyncGrid(ent);
            return;
        }

        // Dragging the drive off the hull takes its range with it.
        ClearGrid(ent);
        if (ent.Comp.DriveState != WarpDriveState.Idle)
            ShutDown(ent, ent.Comp.CoolDownTime);
    }

    #endregion

    #region Power

    private void OnReceivedChanged(Entity<WarpDriveComponent> ent, ref PowerConsumerReceivedChanged args)
    {
        var wasPowered = ent.Comp.Powered;
        UpdatePowered(ent);

        if (ent.Comp.Powered == wasPowered)
            return;

        // A drive that browns out before it ever got the field up has not done anything wrong, and
        // punishing it with the breakdown cooldown would make the drive unusable in exactly the
        // chaotic rounds where it is most wanted. Going brown mid-jump is a different story.
        if (!ent.Comp.Powered && ent.Comp.DriveState == WarpDriveState.Spooling)
            ShutDown(ent, ent.Comp.CoolDownTime);

        SyncGrid(ent);
    }

    /// <summary>
    ///     Recomputes whether the net is actually feeding the drive, and mirrors it into the
    ///     appearance layer.
    /// </summary>
    /// <remarks>
    ///     The APC path does this for free via <c>IsPoweredCalculate</c>. An HV consumer has no
    ///     such flag, so the drive has to ask the question itself - which is also why the ratio is
    ///     configurable: pow3r ramps supply, and a net at 98% of demand is not a brownout.
    /// </remarks>
    private void UpdatePowered(Entity<WarpDriveComponent> ent)
    {
        var powered = false;
        var received = 0f;
        var demanded = ent.Comp.IdlePowerDraw;

        if (TryComp<PowerConsumerComponent>(ent, out var consumer))
        {
            received = consumer.ReceivedPower;
            demanded = consumer.DrawRate;
            powered = consumer.NetworkLoad.LinkedNetwork != default
                      && received >= demanded * ent.Comp.RequiredPowerRatio;
        }

        ent.Comp.Powered = powered;

        // Drives the screen sprite layer. The power system only does this for APC devices.
        _appearance.SetData(ent, PowerDeviceVisuals.Powered, powered);
    }

    private void SetDrawRate(Entity<WarpDriveComponent> ent, float rate)
    {
        if (!TryComp<PowerConsumerComponent>(ent, out var consumer))
            return;

        consumer.DrawRate = rate;
        Dirty(ent, consumer);
    }

    #endregion

    #region State transitions

    /// <summary>
    ///     Brings the drive up or takes it down, if it is allowed to.
    /// </summary>
    public void TryToggle(Entity<WarpDriveComponent> ent)
    {
        var curTime = _timing.CurTime;

        switch (ent.Comp.DriveState)
        {
            case WarpDriveState.Spooling:
            case WarpDriveState.Engaged:
                ShutDown(ent, ent.Comp.CoolDownTime);
                return;

            case WarpDriveState.Idle:
                break;

            default:
                // In transit. The drive is committed until the jump lands.
                return;
        }

        if (curTime < ent.Comp.CoolDownFinishTime)
            return;

        // Refuse if the net cannot sustain the working draw, not merely if the machine is currently
        // drawing its idle load. Without this the drive accepts a spool it cannot finish and then
        // browns out one tick later, which reads as a broken machine rather than an undersized
        // grid.
        if (!HasHeadroom(ent))
            return;

        StartSpooling(ent);
    }

    /// <summary>
    ///     Whether the connected net could plausibly sustain the working draw.
    /// </summary>
    /// <remarks>
    ///     Conservative: it compares demand against what is actually being received right now. That
    ///     is not a guarantee - other loads may still come online mid-jump - but it catches the
    ///     common case of a drive wired to something far too small, which is the one that was
    ///     silently eating a three minute cooldown.
    /// </remarks>
    private bool HasHeadroom(Entity<WarpDriveComponent> ent)
    {
        if (!TryComp<PowerConsumerComponent>(ent, out var consumer))
            return false;

        if (consumer.NetworkLoad.LinkedNetwork == default)
            return false;

        return consumer.ReceivedPower >= ent.Comp.WorkingPowerDraw * ent.Comp.RequiredPowerRatio;
    }

    private void StartSpooling(Entity<WarpDriveComponent> ent)
    {
        var curTime = _timing.CurTime;

        ent.Comp.DriveState = WarpDriveState.Spooling;
        ent.Comp.SpoolStartTime = curTime;
        ent.Comp.SpoolFinishTime = curTime + ent.Comp.SpoolTime;
        ent.Comp.ReadyForJump = true;

        SetDrawRate(ent, ent.Comp.WorkingPowerDraw);
        UpdatePowered(ent);
        SetVisuals(ent, active: false, spooling: true);
        PlaySound(ent, ent.Comp.SpoolUpSound);
        SyncGrid(ent);
        Dirty(ent);
    }

    private void FinishSpooling(Entity<WarpDriveComponent> ent)
    {
        var curTime = _timing.CurTime;

        ent.Comp.DriveState = WarpDriveState.Engaged;
        ent.Comp.SpoolFinishTime = TimeSpan.Zero;
        ent.Comp.BreakdownTime = curTime + ent.Comp.StableTime;

        SetVisuals(ent, active: true, spooling: false);

        // The field is up, so the hull can actually reach anywhere inside the bubble now. Until
        // this moment the warp drive is an expensive space heater.
        SyncGrid(ent);
        Dirty(ent);
    }

    /// <summary>
    ///     Takes the drive down without breaking it, and starts its cooldown.
    /// </summary>
    public void ShutDown(Entity<WarpDriveComponent> ent, TimeSpan coolDown)
    {
        var curTime = _timing.CurTime;

        ent.Comp.DriveState = WarpDriveState.Idle;
        ent.Comp.SpoolStartTime = TimeSpan.Zero;
        ent.Comp.SpoolFinishTime = TimeSpan.Zero;
        ent.Comp.BreakdownTime = TimeSpan.Zero;
        ent.Comp.CoolDownFinishTime = curTime + coolDown;
        ent.Comp.ReadyForJump = true;

        SetDrawRate(ent, ent.Comp.IdlePowerDraw);
        UpdatePowered(ent);
        SetVisuals(ent, active: false);
        PlaySound(ent, ent.Comp.SpoolDownSound);
        SyncGrid(ent);
        Dirty(ent);
    }

    /// <summary>
    ///     The drive let go. This is the fail state the whole scenario design leans on, and the
    ///     reason warp is not just a bigger bluespace drive: there is no bluespace equivalent of
    ///     being stranded between stars.
    /// </summary>
    private void BreakDown(Entity<WarpDriveComponent> ent)
    {
        if (ent.Comp.DriveState != WarpDriveState.Engaged &&
            ent.Comp.DriveState != WarpDriveState.InTransit)
        {
            return;
        }

        ShutDown(ent, ent.Comp.BreakdownCoolDownTime);
        ent.Comp.Heat = 1f;
        Dirty(ent);

        _explosion.QueueExplosion(
            ent.Owner,
            ent.Comp.ExplosionType,
            ent.Comp.TotalIntensity,
            ent.Comp.IntensitySlope,
            ent.Comp.MaxTileBreak,
            canCreateVacuum: true);
    }

    #endregion

    #region Grid linkage

    /// <summary>
    ///     Publishes or withdraws this drive's range on its grid.
    /// </summary>
    /// <remarks>
    ///     One drive per hull is assumed. If a hull ever carries two, the last one to change state
    ///     wins, which is wrong but harmless - and cheaper than arbitrating between them for a
    ///     case that does not exist yet.
    /// </remarks>
    private void SyncGrid(Entity<WarpDriveComponent> ent)
    {
        var grid = Transform(ent.Owner).GridUid;
        if (grid is not { } gridUid)
            return;

        // Range is granted for the whole time the field is up, including during transit: a ship
        // mid-jump is not a place you can FTL out of.
        var active = ent.Comp.DriveState is WarpDriveState.Engaged or WarpDriveState.InTransit;

        var gridComp = EnsureComp<WarpDriveGridComponent>(gridUid);
        if (gridComp.Active == active && gridComp.Range == ent.Comp.Range)
            return;

        gridComp.Active = active;
        gridComp.Range = active ? ent.Comp.Range : 0f;
        Dirty(gridUid, gridComp);
    }

    private void ClearGrid(Entity<WarpDriveComponent> ent)
    {
        var grid = Transform(ent.Owner).GridUid;
        if (grid is not { } gridUid)
            return;

        if (!TryComp<WarpDriveGridComponent>(gridUid, out var gridComp))
            return;

        if (!gridComp.Active && gridComp.Range == 0f)
            return;

        gridComp.Active = false;
        gridComp.Range = 0f;
        Dirty(gridUid, gridComp);
    }

    #endregion

    #region FTL coupling

    private void OnFTLStarted(Entity<WarpDriveGridComponent> ent, ref FTLStartedEvent args)
    {
        if (!TryFindDriveOnGrid(ent.Owner, out var drive) || drive.Comp.DriveState != WarpDriveState.Engaged)
            return;

        // One jump at a time. Nothing should be queueing a second behind the first, and a hull
        // that tried would arrive somewhere it never aimed at.
        drive.Comp.DriveState = WarpDriveState.InTransit;
        drive.Comp.ReadyForJump = false;
        SyncGrid(drive);
        Dirty(drive);
    }

    private void OnFTLCompleted(Entity<WarpDriveGridComponent> ent, ref FTLCompletedEvent args)
    {
        if (!TryFindDriveOnGrid(ent.Owner, out var drive) || drive.Comp.DriveState != WarpDriveState.InTransit)
            return;

        drive.Comp.DriveState = WarpDriveState.Engaged;
        drive.Comp.ReadyForJump = true;

        // A jump restarts the stability window, so a crew that has arrived is not immediately
        // counting down to a breakdown - but heat is not reset, so repeated hops are not free.
        drive.Comp.BreakdownTime = _timing.CurTime + drive.Comp.StableTime;
        SyncGrid(drive);
        Dirty(drive);
    }

    private bool TryFindDriveOnGrid(EntityUid grid, out Entity<WarpDriveComponent> drive)
    {
        var query = EntityQueryEnumerator<WarpDriveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Transform(uid).GridUid != grid)
                continue;

            drive = (uid, comp);
            return true;
        }

        drive = default!;
        return false;
    }

    #endregion

    #region Presentation

    private void OnToggleMessage(Entity<WarpDriveComponent> ent, ref WarpDriveToggleMessage args)
    {
        TryToggle(ent);
    }

    /// <summary>
    ///     Adds a state line to the examine text.
    /// </summary>
    /// <remarks>
    ///     This exists so the drive is distinguishable from a bluespace drive without opening
    ///     anything. A player who examines both in the same room should be able to tell from the
    ///     description alone which one can fail, because that is the entire naming problem this
    ///     feature inherited.
    /// </remarks>
    private void OnExamined(Entity<WarpDriveComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var text = ent.Comp.DriveState switch
        {
            WarpDriveState.Idle when ent.Comp.CoolDownFinishTime > _timing.CurTime =>
                "warp-drive-examine-cooling",
            WarpDriveState.Idle when !ent.Comp.Powered =>
                "warp-drive-examine-unpowered",
            WarpDriveState.Idle => "warp-drive-examine-ready",
            WarpDriveState.Spooling => "warp-drive-examine-spooling",
            WarpDriveState.Engaged => "warp-drive-examine-engaged",
            WarpDriveState.InTransit => "warp-drive-examine-in-transit",
            _ => null
        };

        if (text is null)
            return;

        // Detail only: this is a close-read line, not something to shout across a room.
        args.AddMarkup(Loc.GetString(text), 1);
    }

    private void SetVisuals(Entity<WarpDriveComponent> ent, bool active, bool spooling = false)
    {
        // Appearance is driven by the shared visual enum so the sprite layer and the client agree
        // without either of them duplicating the state machine. The screen layer is NOT set here -
        // it follows the power appearance, which UpdatePowered owns.
        _appearance.SetData(ent, WarpDriveVisuals.Active, active);

        // The point light comes up with the field, so the drive is identifiable from a corridor.
        // Dimmed during spool so it does not strobe the room.
        if (TryComp<PointLightComponent>(ent, out var light))
        {
            light.Enabled = active;
            light.Energy = active ? 2.5f : spooling ? 1f : 0f;
            Dirty(ent, light);
        }

        // Idle keeps a hum, running swaps in the drive note. SetSound rather than writing the
        // field, because AmbientSoundComponent is access-restricted to its own system.
        var sound = active ? ent.Comp.ActiveLoopSound ?? ent.Comp.IdleLoopSound : ent.Comp.IdleLoopSound;
        if (sound is not null)
            _ambientSound.SetSound(ent, sound);
    }

    private void PlaySound(Entity<WarpDriveComponent> ent, SoundPathSpecifier? sound)
    {
        if (sound is null)
            return;

        _audio.PlayPredicted(sound, ent, ent);
    }

    private void UpdateConsoles()
    {
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<WarpDriveComponent>();
        while (query.MoveNext(out var uid, out var drive))
        {
            // Read the live numbers per drive rather than once outside the loop.
            var received = 0f;
            var demanded = drive.Comp.IdlePowerDraw;
            if (TryComp<PowerConsumerComponent>(uid, out var consumer))
            {
                received = consumer.ReceivedPower;
                demanded = consumer.DrawRate;
            }

            // Transit is the one state where heat still climbs, so it gets a stability countdown
            // too - otherwise a long jump looks safe on the console and then explodes.
            var showStability = drive.DriveState is WarpDriveState.Engaged or WarpDriveState.InTransit;

            _ui.SetUiState(uid, WarpDriveUiKey.Key, new WarpDriveBuiState(
                drive.DriveState,
                drive.Heat,
                drive.Powered,
                received,
                demanded,
                SpoolProgress(curTime, drive),
                showStability
                    ? (float) (drive.BreakdownTime - curTime).TotalSeconds
                    : -1f,
                // Only report a cooldown that is actually in the future. A finished cooldown
                // reads as "no cooldown" rather than as a negative number.
                drive.CoolDownFinishTime > curTime
                    ? (float) (drive.CoolDownFinishTime - curTime).TotalSeconds
                    : -1f,
                drive.DriveState is WarpDriveState.Engaged or WarpDriveState.InTransit
                    ? drive.Range
                    : 0f));
        }
    }

    private static float SpoolProgress(TimeSpan curTime, WarpDriveComponent drive)
    {
        if (drive.DriveState != WarpDriveState.Spooling || drive.SpoolTime <= TimeSpan.Zero)
            return 0f;

        var elapsed = (float) (curTime - drive.SpoolStartTime).TotalSeconds;
        return Math.Clamp(elapsed / (float) drive.SpoolTime.TotalSeconds, 0f, 1f);
    }

    #endregion
}