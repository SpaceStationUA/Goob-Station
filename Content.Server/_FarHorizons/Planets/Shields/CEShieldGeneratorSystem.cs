/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._FarHorizons.Camera;
using Content.Shared._FarHorizons.Planets.Shields;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.Planets.Shields;

/// <summary>
/// Drives <see cref="CEShieldGeneratorComponent"/> through its charge → spinup → fire →
/// fail/cooldown cycle. Charging banks received grid power; a full buffer starts the
/// spinup track; surviving spinup fires the beam (planet-wide shake + whiteout) and
/// raises the planet field through <see cref="CEPlanetShieldSystem"/>; damage past the
/// threshold mid-spinup detonates the generator instead. While Active, grid shortfall
/// is paid from the buffer, and an empty buffer collapses the field into a cooldown.
/// </summary>
public sealed partial class CEShieldGeneratorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private CEPlanetShieldSystem _shield = default!;
    [Dependency] private CEPlanetSystem _planet = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    /// <summary>How often an open status window gets a fresh snapshot.</summary>
    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEShieldGeneratorComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<CEShieldGeneratorComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<CEShieldGeneratorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CEShieldGeneratorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CEShieldGeneratorComponent, CEShieldGeneratorToggleMessage>(OnUiToggle);
    }

    private void OnUiOpened(Entity<CEShieldGeneratorComponent> ent, ref BoundUIOpenedEvent args)
    {
        // Don't leave the freshly opened window blank until the next throttle tick.
        ent.Comp.NextUiUpdate = TimeSpan.Zero;
    }

    private void OnUiToggle(Entity<CEShieldGeneratorComponent> ent, ref CEShieldGeneratorToggleMessage args)
    {
        ent.Comp.Enabled = args.Enabled;
        Dirty(ent);

        if (args.Enabled)
            return;

        // Powering down drops any in-flight spinup or active field right away and
        // parks the cycle in Charging (the banked charge survives).
        if (ent.Comp.Stage is not (CEShieldGeneratorStage.SpinningUp or CEShieldGeneratorStage.Active))
            return;

        ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);
        ent.Comp.ActiveSoundStream = _audio.Stop(ent.Comp.ActiveSoundStream);
        LowerShield(ent);
        SetStage(ent, CEShieldGeneratorStage.Charging);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CEShieldGeneratorComponent, PowerConsumerComponent>();
        while (query.MoveNext(out var uid, out var gen, out var consumer))
        {
            // A generator that isn't standing on a planet's z-network is inert: it
            // wants nothing from the grid, banks nothing, and any in-flight cycle
            // aborts. The planet entity is what actually carries the shield flag, so
            // without one there is nothing this machine could ever guard.
            var xform = Transform(uid);
            if (xform.MapUid is not { } mapUid || !_planet.TryGetPlanetForMap(mapUid, out _))
            {
                consumer.DrawRate = 0f;
                AbortOffPlanet((uid, gen));
                UpdateUi((uid, gen), consumer, now, onPlanet: false);
                continue;
            }

            // Master switch off: no draw, and any in-flight spinup or active field is
            // dropped right away (the cycle parks in Charging with its charge intact).
            if (!gen.Enabled)
            {
                consumer.DrawRate = 0f;
                if (gen.Stage is CEShieldGeneratorStage.SpinningUp or CEShieldGeneratorStage.Active)
                {
                    gen.ChargeSoundStream = _audio.Stop(gen.ChargeSoundStream);
                    gen.ActiveSoundStream = _audio.Stop(gen.ActiveSoundStream);
                    LowerShield((uid, gen));
                    SetStage((uid, gen), CEShieldGeneratorStage.Charging);
                }

                UpdateUi((uid, gen), consumer, now);
                continue;
            }

            // Cooldown wants nothing from the grid; every other stage pulls full draw
            // (charging banks it, spinup holds the net warm, active burns it).
            consumer.DrawRate = gen.Stage == CEShieldGeneratorStage.Cooldown ? 0f : gen.DrawRate;

            switch (gen.Stage)
            {
                case CEShieldGeneratorStage.Charging:
                    gen.Charge = Math.Min(gen.Charge + consumer.ReceivedPower * frameTime, gen.MaxCharge);
                    if (gen.Charge >= gen.MaxCharge)
                        StartSpinup((uid, gen), now);
                    break;

                case CEShieldGeneratorStage.SpinningUp:
                    if (now >= gen.SpinupEndsAt)
                        Fire((uid, gen));
                    break;

                case CEShieldGeneratorStage.Active:
                    // Grid shortfall comes out of the buffer: 120 MJ ≈ 120 s of grace
                    // at full starvation. The beam doesn't spin down until it's spent.
                    var deficit = gen.DrawRate - consumer.ReceivedPower;
                    if (deficit > 0f)
                    {
                        gen.Charge -= deficit * frameTime;
                        if (gen.Charge <= 0f)
                            Fail((uid, gen), now);
                    }

                    // The activation loop plays for a while, then the machine runs silent.
                    if (gen.ActiveSoundStream != null && now >= gen.ActiveSoundEndsAt)
                        gen.ActiveSoundStream = _audio.Stop(gen.ActiveSoundStream);
                    break;

                case CEShieldGeneratorStage.Cooldown:
                    if (now >= gen.CooldownEndsAt)
                        SetStage((uid, gen), CEShieldGeneratorStage.Charging);
                    break;
            }

            UpdateUi((uid, gen), consumer, now);
        }
    }

    /// <summary>Push a snapshot to any open status window, throttled to twice a second.</summary>
    private void UpdateUi(Entity<CEShieldGeneratorComponent> ent, PowerConsumerComponent consumer, TimeSpan now, bool onPlanet = true)
    {
        if (now < ent.Comp.NextUiUpdate)
            return;

        ent.Comp.NextUiUpdate = now + UiUpdateInterval;
        if (!_ui.IsUiOpen(ent.Owner, CEShieldGeneratorUiKey.Key))
            return;

        var gen = ent.Comp;
        var eta = !onPlanet ? -1f : gen.Stage switch
        {
            // Time to a full buffer at the current inflow.
            CEShieldGeneratorStage.Charging when consumer.ReceivedPower > 0f
                => (gen.MaxCharge - gen.Charge) / consumer.ReceivedPower,
            // Time to beam fire.
            CEShieldGeneratorStage.SpinningUp => (float) (gen.SpinupEndsAt - now).TotalSeconds,
            // Starving: time until the buffer runs dry and the field collapses.
            CEShieldGeneratorStage.Active when gen.DrawRate - consumer.ReceivedPower > 0f
                => gen.Charge / (gen.DrawRate - consumer.ReceivedPower),
            // Time until the lockout lifts.
            CEShieldGeneratorStage.Cooldown => (float) (gen.CooldownEndsAt - now).TotalSeconds,
            _ => -1f,
        };

        _ui.SetUiState(ent.Owner, CEShieldGeneratorUiKey.Key, new CEShieldGeneratorBuiState
        {
            Stage = gen.Stage,
            Charge = gen.Charge,
            MaxCharge = gen.MaxCharge,
            ReceivedPower = consumer.ReceivedPower,
            WantedPower = consumer.DrawRate,
            EtaSeconds = eta,
            OnPlanet = onPlanet,
            Enabled = gen.Enabled,
        });
    }

    /// <summary>
    /// The generator's map stopped resolving to a planet (or never did): kill any
    /// in-flight cycle and park it in Charging. Not a punishment cooldown — this is a
    /// mapping/admin situation, not a gameplay failure — so the banked charge survives,
    /// but nothing progresses until the machine is back on a real planet.
    /// </summary>
    private void AbortOffPlanet(Entity<CEShieldGeneratorComponent> ent)
    {
        if (ent.Comp.Stage is not (CEShieldGeneratorStage.SpinningUp or CEShieldGeneratorStage.Active))
            return;

        ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);
        ent.Comp.ActiveSoundStream = _audio.Stop(ent.Comp.ActiveSoundStream);
        LowerShield(ent);
        SetStage(ent, CEShieldGeneratorStage.Charging);
    }

    /// <summary>Buffer full: hold it, start the countdown and the chargeup track.</summary>
    private void StartSpinup(Entity<CEShieldGeneratorComponent> ent, TimeSpan now)
    {
        ent.Comp.SpinupEndsAt = now + ent.Comp.SpinupTime;
        ent.Comp.SpinupDamageTaken = 0f;
        ent.Comp.ChargeSoundStream = _audio.PlayPvs(ent.Comp.ChargeSound, ent)?.Entity;
        SetStage(ent, CEShieldGeneratorStage.SpinningUp);
    }

    /// <summary>
    /// Spinup survived: raise the planet field and sell the moment — fire track plus a
    /// sustained radial shake and a harmless whiteout for everyone on the planet.
    /// </summary>
    private void Fire(Entity<CEShieldGeneratorComponent> ent)
    {
        // The shield flag lives on the space-side planet entity — the thing descent
        // validation and the client visuals read — never on the z-stack maps, so the
        // generator's map has to be resolved back to its owning planet first.
        var xform = Transform(ent);
        if (xform.MapUid is { } mapUid
            && _planet.TryGetPlanetForMap(mapUid, out var planet)
            && _shield.SetShieldActive(planet, true))
        {
            // The audience is the whole z-stack, not just the ground map: riders in
            // transit and ships parked on levels above live on sibling maps of the
            // same network, and they're exactly who the "planet just lit up" moment
            // is for.
            var filter = Filter.Empty();
            if (_zLevels.TryGetZNetwork(mapUid, out var zNetwork))
            {
                foreach (var levelUid in zNetwork.Value.Comp.SortedZLevels)
                {
                    if (TryComp<MapComponent>(levelUid, out var map))
                        filter.AddInMap(map.MapId);
                }
            }
            else
            {
                filter.AddInMap(xform.MapID);
            }

            RaiseNetworkEvent(new RadialShakeEvent
            {
                Duration = ent.Comp.FireShakeDuration,
                Amplitude = ent.Comp.FireShakeAmplitude,
            }, filter);

            // Cinematic whiteout via the screen overlay — deliberately not the
            // gameplay flash stack, so it can't be eaten by eye protection.
            RaiseNetworkEvent(new CEScreenFlashEvent
            {
                Duration = (float) ent.Comp.FireFlashDuration.TotalSeconds,
                Color = Color.White,
            }, filter);
        }
        else
        {
            // Should be unreachable: Update aborts any off-planet cycle before it can
            // reach Fire. Kept as a tripwire for planet/network teardown races.
            Log.Warning($"Shield generator {ToPrettyString(ent)} fired but no planet resolved for its map; no field raised.");
        }

        _audio.PlayPvs(ent.Comp.FireSound, ent);
        SpawnBeams(ent);
        SetStage(ent, CEShieldGeneratorStage.Active);

        // Loud start, then silence: loop the activation track for a few seconds.
        ent.Comp.ActiveSoundStream = _audio.PlayPvs(ent.Comp.ActiveSound, ent, AudioParams.Default.WithLoop(true))?.Entity;
        ent.Comp.ActiveSoundEndsAt = _timing.CurTime + ent.Comp.ActiveSoundDuration;
    }

    /// <summary>
    /// Raise the beam column: an evaporating slice at the generator's world position on
    /// every z-level above it, so anything transiting the stack over a live generator
    /// gets a hole burned in it. No slice on the generator's own level — the ground-side
    /// light show belongs to the generator sprite itself.
    /// </summary>
    private void SpawnBeams(Entity<CEShieldGeneratorComponent> ent)
    {
        // Temporarily disabled. The beam will be readded later.
    }

    /// <summary>Buffer ran dry: drop the field and lock out for the cooldown.</summary>
    private void Fail(Entity<CEShieldGeneratorComponent> ent, TimeSpan now)
    {
        ent.Comp.Charge = 0f;
        ent.Comp.CooldownEndsAt = now + ent.Comp.CooldownTime;
        ent.Comp.ActiveSoundStream = _audio.Stop(ent.Comp.ActiveSoundStream);
        LowerShield(ent);
        _audio.PlayPvs(ent.Comp.FailSound, ent);
        SetStage(ent, CEShieldGeneratorStage.Cooldown);
    }

    private void OnDamaged(Entity<CEShieldGeneratorComponent> ent, ref DamageChangedEvent args)
    {
        // Only spinup is volatile: rattling the generator during chargeup sets it off.
        if (ent.Comp.Stage != CEShieldGeneratorStage.SpinningUp)
            return;

        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        ent.Comp.SpinupDamageTaken += args.DamageDelta.GetTotal().Float();
        if (ent.Comp.SpinupDamageTaken >= ent.Comp.SpinupDamageThreshold)
            Detonate(ent);
    }

    private void OnEmpPulse(Entity<CEShieldGeneratorComponent> ent, ref EmpPulseEvent args)
    {
        // Already mid-detonation; nothing left to set off.
        if (ent.Comp.Detonating)
            return;

        // A cold generator has nothing banked to vent - the pulse just washes over it.
        if (ent.Comp.Stage is not (CEShieldGeneratorStage.SpinningUp or CEShieldGeneratorStage.Active))
            return;

        // An EMP reaching a generator with a live buffer dumps the whole bank at once:
        // same fireworks as a spinup overload, no matter how gently it was delivered.
        args.Affected = true;
        args.Disabled = true;
        Detonate(ent);
    }

    private void OnShutdown(Entity<CEShieldGeneratorComponent> ent, ref ComponentShutdown args)
    {
        // Generator destroyed/deconstructed out from under an active field: field dies
        // with it. The fail sting plays at the wreck.
        ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);
        ent.Comp.ActiveSoundStream = _audio.Stop(ent.Comp.ActiveSoundStream);

        if (ent.Comp.Stage != CEShieldGeneratorStage.Active)
            return;

        LowerShield(ent);
        _audio.PlayPvs(ent.Comp.FailSound, Transform(ent).Coordinates);
    }

    /// <summary>
    /// The generator took too much of a beating mid-spinup and goes critical: a
    /// conventional blast at the wreck. No EMP of its own — it's the *victim* of
    /// EMPs (see <see cref="OnEmpPulse"/>), not a source.
    /// </summary>
    private void Detonate(Entity<CEShieldGeneratorComponent> ent)
    {
        if (ent.Comp.Detonating)
            return;
        ent.Comp.Detonating = true;

        ent.Comp.ChargeSoundStream = _audio.Stop(ent.Comp.ChargeSoundStream);
        ent.Comp.ActiveSoundStream = _audio.Stop(ent.Comp.ActiveSoundStream);

        _explosion.QueueExplosion(ent, ExplosionSystem.DefaultExplosionPrototypeId,
            totalIntensity: 300f, slope: 2f, maxTileIntensity: 30f);
        QueueDel(ent);
    }

    private void LowerShield(Entity<CEShieldGeneratorComponent> ent)
    {
        if (Transform(ent).MapUid is { } mapUid && _planet.TryGetPlanetForMap(mapUid, out var planet))
            _shield.SetShieldActive(planet, false);
    }

    private void SetStage(Entity<CEShieldGeneratorComponent> ent, CEShieldGeneratorStage stage)
    {
        if (ent.Comp.Stage == stage)
            return;

        ent.Comp.Stage = stage;
        Dirty(ent);
        _appearance.SetData(ent, CEShieldGeneratorVisuals.Stage, stage);
    }
}
