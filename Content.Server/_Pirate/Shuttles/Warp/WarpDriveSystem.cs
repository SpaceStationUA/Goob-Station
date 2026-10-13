// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Journey;
using Content.Server.GameTicking;
using Content.Server.NodeContainer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.NodeGroups;
using Content.Server.Shuttles.Events;
using Content.Shared._Pirate.Journey;
using Content.Shared._Pirate.Shuttles.Warp;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Shuttles.Warp;

/// <summary>
///     Runs the warp drive's state machine.
/// </summary>
/// <remarks>
///     While the field is spooling or engaged, the drive publishes <see cref="WarpDriveGridComponent"/>
///     on its grid, and that is what <c>SharedShuttleSystem.GetFTLRange</c> reads. A drive that goes down
///     takes its range with it.
///
///     The drive runs on HV (<c>PowerConsumerComponent</c>) rather than an APC. An APC supplies 10 kW and
///     the drive asks for 150 kW, so it has to be wired to a SMES.
/// </remarks>
public sealed class WarpDriveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly PowerNetSystem _powerNet = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(500);

    private TimeSpan _nextUiUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarpDriveComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WarpDriveComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WarpDriveComponent, AnchorStateChangedEvent>(OnAnchored);

        // HV supply reports through PowerConsumerReceivedChanged. PowerChangedEvent is only sent by APCs.
        SubscribeLocalEvent<WarpDriveComponent, PowerConsumerReceivedChanged>(OnReceivedChanged);

        SubscribeLocalEvent<WarpDriveComponent, WarpDriveToggleMessage>(OnToggleMessage);
        SubscribeLocalEvent<WarpDriveComponent, WarpDriveJumpMessage>(OnJumpMessage);
        SubscribeLocalEvent<WarpDriveComponent, WarpDriveSetCourseMessage>(OnSetCourseMessage);
        SubscribeLocalEvent<WarpDriveComponent, ExaminedEvent>(OnExamined);

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
            // Heat falls while idle, so a cooldown actually cools.
            if (drive.DriveState == WarpDriveState.Idle)
            {
                drive.Heat = MathF.Max(drive.Heat - drive.HeatFallRate * frameTime, 0f);
                continue;
            }

            // Read live every tick. The Powered flag only refreshes on power events.
            var delivered = 0f;
            if (TryComp<PowerConsumerComponent>(uid, out var flowConsumer))
                delivered = flowConsumer.ReceivedPower;

            // Only a grid that delivers nothing shuts the drive down. Partial power just slows the spool.
            if (drive.DriveState != WarpDriveState.InTransit && delivered < 1f)
            {
                if (drive.UnpoweredSince == TimeSpan.Zero)
                    drive.UnpoweredSince = curTime;
                else if (curTime - drive.UnpoweredSince >= drive.PowerGrace)
                {
                    Log.Info($"warp drive {uid} without supply for {drive.PowerGrace.TotalSeconds:0} s while {drive.DriveState} (grid free {GridFree(uid):0} W), shutting down");
                    drive.UnpoweredSince = TimeSpan.Zero;
                    ShutDown((uid, drive),
                        drive.DriveState == WarpDriveState.Spooling
                            ? drive.CoolDownTime
                            : drive.BreakdownCoolDownTime);
                }
                continue;
            }

            drive.UnpoweredSince = TimeSpan.Zero;

            switch (drive.DriveState)
            {
                case WarpDriveState.Spooling:
                    // Spool speed follows delivery: full power is full speed, half power is half speed.
                    var ratio = drive.SpoolPowerDraw > 0f
                        ? Math.Clamp(delivered / drive.SpoolPowerDraw, 0f, 1f)
                        : 1f;
                    drive.SpoolProgress = MathF.Min(
                        drive.SpoolProgress + frameTime / (float) drive.SpoolTime.TotalSeconds * ratio,
                        1f);
                    if (drive.SpoolProgress >= 1f)
                        FinishSpooling((uid, drive));
                    break;

                case WarpDriveState.Engaged:
                case WarpDriveState.InTransit:
                    // Heat is the only way to break down. A held field cooks in about two minutes.
                    drive.Heat = MathF.Min(drive.Heat + drive.HeatRiseRate * frameTime, 1f);
                    if (drive.Heat >= 1f)
                        BreakDown((uid, drive));
                    else
                        FlickerLight((uid, drive), curTime);
                    break;

                default:
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
        // The idle draw shows on the HV monitor even before anyone touches the drive.
        SetDrawRate(ent, ent.Comp.IdlePowerDraw);
        UpdatePowered(ent);
        SyncGrid(ent);
    }

    private void OnShutdown(Entity<WarpDriveComponent> ent, ref ComponentShutdown args)
    {
        // The grid outlives the machine, so its range has to be withdrawn explicitly.
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
        UpdatePowered(ent);
        SyncGrid(ent);
    }

    /// <summary>
    ///     Works out whether the net is feeding the drive, and mirrors it into the appearance layer.
    /// </summary>
    /// <remarks>
    ///     An APC gets this flag for free. An HV consumer does not, so the drive checks it itself, using
    ///     <see cref="WarpDriveComponent.RequiredPowerRatio"/> for the ramp in supply.
    /// </remarks>
    private void UpdatePowered(Entity<WarpDriveComponent> ent)
    {
        var powered = false;

        if (TryComp<PowerConsumerComponent>(ent, out var consumer))
        {
            var received = consumer.ReceivedPower;
            var demanded = consumer.DrawRate;
            powered = consumer.NetworkLoad.LinkedNetwork != default
                      && received >= demanded * ent.Comp.RequiredPowerRatio;
        }

        ent.Comp.Powered = powered;

        // The power system only drives this layer for APC devices.
        _appearance.SetData(ent, PowerDeviceVisuals.Powered, powered);
    }

    private void SetDrawRate(Entity<WarpDriveComponent> ent, float rate)
    {
        if (!TryComp<PowerConsumerComponent>(ent, out var consumer))
            return;

        // The power net reads the draw rate directly, so there is nothing to replicate.
        consumer.DrawRate = rate;
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
                // In transit: the drive is committed until the jump lands.
                return;
        }

        if (curTime < ent.Comp.CoolDownFinishTime)
            return;

        // Refuse when the net cannot carry the spool draw, not merely when the machine is idling. Otherwise
        // the drive accepts a spool it cannot finish and browns out a tick later.
        if (!HasHeadroom(ent))
        {
            var free = GridFree(ent.Owner);
            var need = ent.Comp.SpoolPowerDraw * ent.Comp.RequiredPowerRatio;
            SetNotice(ent, Loc.GetString("warp-drive-notice-no-headroom", ("free", free), ("need", need)));
            Log.Info($"warp drive {ent.Owner} spool refused: grid free {free:0} W, need {need:0} W");
            return;
        }

        SetNotice(ent, string.Empty);
        StartSpooling(ent);
    }

    /// <summary>
    ///     Whether the grid can cover the spool draw. Free supply is used, because intake only ever shows
    ///     the idle draw and gating on it would never let a drive start.
    /// </summary>
    private bool HasHeadroom(Entity<WarpDriveComponent> ent)
    {
        if (!TryComp<PowerConsumerComponent>(ent, out var consumer))
            return false;

        if (consumer.NetworkLoad.LinkedNetwork == default)
            return false;

        return GridFree(ent) >= ent.Comp.SpoolPowerDraw * ent.Comp.RequiredPowerRatio;
    }

    /// <summary>
    ///     What the drive's grid can spare right now, in watts: cable supply plus battery discharge minus what
    ///     the grid already feeds, floored at zero. Zero when unwired.
    /// </summary>
    private float GridFree(EntityUid uid)
    {
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return 0f;

        foreach (var node in container.Nodes.Values)
        {
            if (node.NodeGroup is not IBasePowerNet net)
                continue;

            var stats = _powerNet.GetNetworkStatistics(net.NetworkNode);
            var supply = stats.SupplyCurrent + stats.SupplyBatteries;
            return MathF.Max(supply - stats.Consumption, 0f);
        }

        return 0f;
    }

    private void StartSpooling(Entity<WarpDriveComponent> ent)
    {
        ent.Comp.DriveState = WarpDriveState.Spooling;
        ent.Comp.SpoolProgress = 0f;
        ent.Comp.UnpoweredSince = TimeSpan.Zero;
        Log.Info($"warp drive {ent.Owner} spooling, grid free {GridFree(ent.Owner):0} W for a {ent.Comp.SpoolPowerDraw * ent.Comp.RequiredPowerRatio:0} W need");

        SetDrawRate(ent, ent.Comp.SpoolPowerDraw);
        UpdatePowered(ent);
        SetVisuals(ent, active: false, spooling: true);
        PlaySound(ent, ent.Comp.SpoolUpSound);
        SyncGrid(ent);
        Dirty(ent);
    }

    private void FinishSpooling(Entity<WarpDriveComponent> ent)
    {
        ent.Comp.DriveState = WarpDriveState.Engaged;
        Log.Info($"warp drive {ent.Owner} engaged");

        // The field is up, so the draw drops from the spool rate to the hold rate.
        SetDrawRate(ent, ent.Comp.WorkingPowerDraw);
        UpdatePowered(ent);

        SetVisuals(ent, active: true, spooling: false);

        // Range is granted only now. Until this moment the drive is an expensive space heater.
        SyncGrid(ent);
        Dirty(ent);
    }

    /// <summary>
    ///     Takes the drive down without breaking it, and starts its cooldown.
    /// </summary>
    public void ShutDown(Entity<WarpDriveComponent> ent, TimeSpan coolDown)
    {
        ent.Comp.DriveState = WarpDriveState.Idle;
        ent.Comp.SpoolProgress = 0f;
        ent.Comp.CoolDownFinishTime = _timing.CurTime + coolDown;
        ent.Comp.UnpoweredSince = TimeSpan.Zero;

        SetDrawRate(ent, ent.Comp.IdlePowerDraw);
        UpdatePowered(ent);
        SetVisuals(ent, active: false);
        PlaySound(ent, ent.Comp.SpoolDownSound);
        SyncGrid(ent);
        Dirty(ent);
    }

    /// <summary>
    ///     The drive lets go and detonates.
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
        Log.Info($"warp drive {ent.Owner} BREAKDOWN, detonating");

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
    ///     One drive per hull is assumed. With two, the last one to change state wins.
    /// </remarks>
    private void SyncGrid(Entity<WarpDriveComponent> ent)
    {
        var grid = Transform(ent.Owner).GridUid;
        if (grid is not { } gridUid)
            return;

        // The range holds for the whole field, transit included.
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

        // One jump at a time, so nothing can queue a second one behind the first.
        drive.Comp.DriveState = WarpDriveState.InTransit;
        SyncGrid(drive);
        Dirty(drive);
    }

    private void OnFTLCompleted(Entity<WarpDriveGridComponent> ent, ref FTLCompletedEvent args)
    {
        if (!TryFindDriveOnGrid(ent.Owner, out var drive) || drive.Comp.DriveState != WarpDriveState.InTransit)
            return;

        drive.Comp.DriveState = WarpDriveState.Engaged;
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
        // The console refreshes twice a second, so a second press lands blind. Without the debounce it
        // would abort the spool that the first press just started.
        if (_timing.CurTime - ent.Comp.LastToggleTime < ent.Comp.ToggleDebounce)
            return;

        ent.Comp.LastToggleTime = _timing.CurTime;
        TryToggle(ent);
    }

    private void OnJumpMessage(Entity<WarpDriveComponent> ent, ref WarpDriveJumpMessage args)
    {
        // The rule owns the jump. It is resolved lazily because the rule already depends on this system.
        EntityManager.System<JourneyRuleSystem>().CommitDrive(ent);
    }

    private void OnSetCourseMessage(Entity<WarpDriveComponent> ent, ref WarpDriveSetCourseMessage args)
    {
        // Only existence is checked here. The gate and the position are checked when the course is flown,
        // because the gate can change in between.
        if (!_proto.HasIndex<JourneySectorPrototype>(args.Sector))
        {
            SetNotice(ent, Loc.GetString("warp-drive-notice-no-such-system"));
            return;
        }

        ent.Comp.CourseSector = args.Sector;
        SetNotice(ent, string.Empty);
        Dirty(ent);
    }

    /// <summary>
    ///     Writes a line on the console. Refusals and commitments all land here, so the drive never fails silently.
    /// </summary>
    public void SetNotice(Entity<WarpDriveComponent> ent, string notice)
    {
        ent.Comp.Notice = notice;
        Dirty(ent);
    }

    /// <summary>
    ///     Commits an engaged drive to a journey transit. The journey rule calls this, because the station never
    ///     physically travels and no FTL event will arrive.
    /// </summary>
    /// <returns>False when the drive was not engaged.</returns>
    public bool SetInTransit(Entity<WarpDriveComponent> ent)
    {
        if (ent.Comp.DriveState != WarpDriveState.Engaged)
            return false;

        ent.Comp.DriveState = WarpDriveState.InTransit;
        SyncGrid(ent);
        Dirty(ent);
        return true;
    }

    /// <summary>
    ///     Lands a journey transit: the drive shuts down onto the transit cooldown, which is much longer than
    ///     a clean one. Heat is kept.
    /// </summary>
    public void CompleteTransit(Entity<WarpDriveComponent> ent)
    {
        if (ent.Comp.DriveState != WarpDriveState.InTransit)
            return;

        ShutDown(ent, ent.Comp.TransitCoolDownTime);
    }

    /// <summary>
    ///     Adds the drive's state to its examine text, so it can be told apart from a bluespace drive.
    /// </summary>
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

        args.AddMarkup(Loc.GetString(text), 1);
    }

    /// <summary>
    ///     While the field is up, the housing light breathes, faster and wilder as the core heats.
    /// </summary>
    private void FlickerLight(Entity<WarpDriveComponent> ent, TimeSpan curTime)
    {
        // About twelve steps a second: smooth enough to read as flicker, cheap enough to ignore.
        var step = (int) (curTime.TotalSeconds * 12);
        if (step == ent.Comp.LastFlickerStep)
            return;
        ent.Comp.LastFlickerStep = step;

        var t = (float) curTime.TotalSeconds;
        var energy = 2.5f
            + 0.6f * MathF.Sin(t * 13f)
            + 0.4f * MathF.Sin(t * 7.3f + 1.7f)
            + ent.Comp.Heat * 1.2f;
        if (TryComp<PointLightComponent>(ent, out var light))
            _pointLight.SetEnergy(ent, MathF.Max(energy, 1.5f), light);
    }

    private void SetVisuals(Entity<WarpDriveComponent> ent, bool active, bool spooling = false)
    {
        // The sprite follows the shared visual enum. The screen layer follows the power state, which
        // UpdatePowered owns.
        _appearance.SetData(ent, WarpDriveVisuals.Active, active);

        // The light comes up with the field, and dims during the spool so it does not strobe the room.
        if (TryComp<PointLightComponent>(ent, out var light))
        {
            _pointLight.SetEnabled(ent, active, light);
            _pointLight.SetEnergy(ent, active ? 2.5f : spooling ? 1f : 0f, light);
        }

        // The idle hum plays while cold. The louder loop takes over as soon as the spool starts.
        var sound = active || spooling ? ent.Comp.ActiveLoopSound ?? ent.Comp.IdleLoopSound : ent.Comp.IdleLoopSound;
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
            var received = 0f;
            var demanded = drive.IdlePowerDraw;
            if (TryComp<PowerConsumerComponent>(uid, out var consumer))
            {
                received = consumer.ReceivedPower;
                demanded = consumer.DrawRate;
            }

            // Stability is the time until the core reaches 1 at its current rate of heating.
            var engaged = drive.DriveState is WarpDriveState.Engaged or WarpDriveState.InTransit;
            var stability = -1f;
            if (engaged && drive.HeatRiseRate > 0f)
                stability = MathF.Max((1f - drive.Heat) / drive.HeatRiseRate, 0f);

            var supply = GridFree(uid);
            var need = drive.SpoolPowerDraw * drive.RequiredPowerRatio;

            _ui.SetUiState(uid, WarpDriveUiKey.Key, new WarpDriveBuiState(
                drive.DriveState,
                drive.Heat,
                drive.Powered,
                received,
                demanded,
                SpoolProgress(drive),
                stability,
                // A finished cooldown reads as no cooldown, not as a negative number.
                drive.CoolDownFinishTime > curTime
                    ? (float) (drive.CoolDownFinishTime - curTime).TotalSeconds
                    : -1f,
                engaged ? drive.Range : 0f,
                drive.CourseSector,
                drive.Notice,
                supply,
                HasHeadroom((uid, drive)),
                CurrentSectorName(),
                need));
        }
    }

    /// <summary>
    ///     The name of the sector the ship is in, or empty when no journey is running.
    /// </summary>
    private string CurrentSectorName()
    {
        var rules = EntityManager.EntityQueryEnumerator<JourneyRuleComponent, GameRuleComponent>();
        while (rules.MoveNext(out var uid, out var journey, out var rule))
        {
            if (!_ticker.IsGameRuleActive(uid, rule) || journey.Sector == string.Empty)
                continue;

            return _proto.TryIndex<JourneySectorPrototype>(journey.Sector, out var sector)
                ? Loc.GetString(sector.Name)
                : journey.Sector;
        }

        return string.Empty;
    }

    private static float SpoolProgress(WarpDriveComponent drive)
    {
        if (drive.DriveState != WarpDriveState.Spooling)
            return 0f;

        return Math.Clamp(drive.SpoolProgress, 0f, 1f);
    }

    #endregion
}
