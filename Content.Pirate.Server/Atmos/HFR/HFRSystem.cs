// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Pirate.Shared.Atmos.HFR;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Machines.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Emp;
using Content.Shared.Machines.Components;
using Content.Shared.Machines.Events;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Pirate.Server.Atmos.HFR;

/// <summary>
///     Server-side processing for the Hyper-torus Fusion Reactor (HFR).
///     A 3x3 multipart machine. Mechanics ported from /tg/station's HFR,
///     adapted to the gases available on this server.
/// </summary>
public sealed partial class HFRSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly NodeContainerSystem _node = default!;
    [Dependency] private readonly MultipartMachineSystem _multipart = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly Gas[] AllGases = Enum.GetValues<Gas>();
    private static readonly string[] PipeNodeNames = ["pipe", "pipe1", "pipe2"];

    /// <summary>
    ///     Tier output rates per power level (per /tg/ hfr_main_processes.dm),
    ///     indexed by power level 0-6. Precomputed to avoid per-tick allocation.
    /// </summary>
    private static readonly float[][] TierRatesByPowerLevel =
    [
        [0f, 0f, 0f, 0f, 0f, 0f],       // PL0 (no reaction)
        [0.95f, 0.75f, 0f, 0f, 0f, 0f], // PL1
        [1.65f, 1f, 0f, 0f, 0f, 0f],    // PL2
        [0f, 0.5f, 0.45f, 0f, 0f, 0f],  // PL3
        [0f, 0f, 1.65f, 1.25f, 0f, 0f], // PL4
        [0f, 0f, 0f, 0.65f, 1f, 0.75f], // PL5
        [0f, 0f, 0f, 0f, 0.35f, 1f],    // PL6
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HFRComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<HFRComponent, MultipartMachineAssemblyStateChanged>(OnAssemblyChanged);
        SubscribeLocalEvent<HFRComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);

        Subs.BuiEvents<HFRComponent>(HFRUiKey.Key, subs =>
        {
            subs.Event<HFRSetRecipeMessage>(OnSetRecipe);
            subs.Event<HFRSetHeatingConductorMessage>(OnSetHeatingConductor);
            subs.Event<HFRSetMagneticConstrictorMessage>(OnSetMagneticConstrictor);
            subs.Event<HFRSetFuelInjectionRateMessage>(OnSetFuelInjectionRate);
            subs.Event<HFRSetCurrentDampenerMessage>(OnSetCurrentDampener);
            subs.Event<HFRSetModeratorInjectionRateMessage>(OnSetModeratorInjectionRate);
            subs.Event<HFRToggleWasteRemovalMessage>(OnToggleWasteRemoval);
            subs.Event<HFRSetPowerMessage>(OnSetPower);
            subs.Event<HFRSetCoolingMessage>(OnSetCooling);
            subs.Event<HFRSetFuelSwitchMessage>(OnSetFuelSwitch);
            subs.Event<HFRSetModeratorSwitchMessage>(OnSetModeratorSwitch);
            subs.Event<HFRSetModeratorFilteringRateMessage>(OnSetModeratorFilteringRate);
            subs.Event<HFRSetModeratorFilterMessage>(OnSetModeratorFilter);
            subs.Event<HFREmergencyShutdownMessage>(OnEmergencyShutdown);
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    private void OnAssemblyChanged(Entity<HFRComponent> ent, ref MultipartMachineAssemblyStateChanged args)
    {
        // If a part was removed while the panel is open, close it: an unassembled
        // reactor must not expose its controls.
        if (!_multipart.IsAssembled(new Entity<MultipartMachineComponent?>(ent.Owner, null)))
            _ui.CloseUi(ent.Owner, HFRUiKey.Key);

        UpdateUI(ent);
    }

    /// <summary>
    ///     The reactor panel cannot be opened until the machine is fully assembled
    ///     (all eight parts built around the core), and a broken reactor (cracked
    ///     sprite) cannot be operated at all until it heals.
    /// </summary>
    private void OnUiOpenAttempt(Entity<HFRComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_multipart.IsAssembled(new Entity<MultipartMachineComponent?>(ent.Owner, null)))
        {
            if (!args.Silent)
                _popup.PopupEntity(Loc.GetString("hfr-ui-not-assembled"), ent.Owner, args.User);

            args.Cancel();
            return;
        }

        // A broken reactor cannot be operated normally — BUT during a meltdown
        // countdown the panel must stay accessible: that's the player's only real
        // chance to hit Emergency Shutdown / dump Healium and save the station.
        // (Per /tg/, the countdown can always be cancelled if you act in time.)
        if (ent.Comp.Integrity <= HFRConstants.BrokenSpriteIntegrityThreshold
            && !ent.Comp.MeltdownCountdownActive)
        {
            if (!args.Silent)
                _popup.PopupEntity(Loc.GetString("hfr-ui-broken"), ent.Owner, args.User);

            args.Cancel();
        }
    }

    /// <summary>
    ///     Finds a pipe node on one of the HFR machine parts, resolved through the
    ///     multipart machine (the part the player built at that offset).
    /// </summary>
    private bool GetPipe(Entity<HFRComponent> ent, HFRParts part, [NotNullWhen(true)] out PipeNode? node)
    {
        node = null;
        var port = _multipart.GetPartEntity(new Entity<MultipartMachineComponent?>(ent.Owner, null), part);
        if (port is not { } portEnt || Deleted(portEnt) || !Transform(portEnt).Anchored)
            return false;

        return GetPipeFromEntity(portEnt, out node);
    }

    /// <summary>
    ///     Finds a pipe node on a raw entity (the internal coolant pipe). The ports
    ///     have three nodes (one per pipe layer), so we return the first one that has
    ///     gas — a port works regardless of which layer the player built pipes on.
    /// </summary>
    private bool GetPipeFromEntity(EntityUid? portEnt, [NotNullWhen(true)] out PipeNode? node)
    {
        node = null;
        if (portEnt is not { } port || Deleted(port) || !Transform(port).Anchored)
            return false;

        foreach (var nodeName in PipeNodeNames)
        {
            if (_node.TryGetNode(port, nodeName, out PipeNode? candidate) && candidate.Air.TotalMoles > 0)
            {
                node = candidate;
                return true;
            }
        }

        // No gas in any layer yet; fall back to the primary node so callers see
        // that the port exists but is empty.
        return _node.TryGetNode(port, "pipe", out node);
    }

    #region Main Loop

    private void OnAtmosUpdate(Entity<HFRComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;

        // The reactor only runs when all eight parts are in place.
        if (!_multipart.IsAssembled(new Entity<MultipartMachineComponent?>(ent.Owner, null)))
        {
            UpdateVisualState(ent, comp, 0);
            UpdateUiThrottled(ent, args.dt);
            return;
        }

        var dt = args.dt;
        var fusionMix = comp.FusionMix;
        var moderatorMix = comp.ModeratorMix;

        // Need all four connections present and anchored
        if (!GetPipe(ent, HFRParts.FuelInput, out var fuel)
            || !GetPipe(ent, HFRParts.ModeratorInput, out var moderator)
            || !GetPipe(ent, HFRParts.Interface, out var coolant)
            || !GetPipe(ent, HFRParts.WasteOutput, out var output))
        {
            // Still update the visual state (e.g. broken sprite) even without pipes.
            UpdateVisualState(ent, comp, 0);
            UpdateUiThrottled(ent, args.dt);
            return;
        }

        // --- Meltdown countdown (per /tg/ countdown proc) ---
        // Once integrity drops to the melting threshold the reactor starts a
        // countdown; when it runs out the machine melts down. The countdown can
        // be cancelled if the reactor is cooled back above the threshold
        // ("failsafe disengaged").
        if (comp.MeltdownCountdownActive)
        {
            comp.MeltdownCountdown -= args.dt;

            // The critical explosion warning plays once, 10 seconds before the
            // meltdown (per /tg/ countdown(), which plays HFR_critical_explosion.ogg
            // at 10 seconds for critical melts).
            if (!comp.MeltdownCriticalSoundPlayed
                && comp.MeltdownCountdown <= HFRConstants.MeltdownCriticalSoundAt)
            {
                comp.MeltdownCriticalSoundPlayed = true;
                _audio.PlayPvs(HFRConstants.MeltdownCriticalSound, ent.Owner);
            }

            // Radio message every MeltdownRadioInterval seconds.
            if (comp.MeltdownCountdown <= comp.LastMeltdownMessageAt - HFRConstants.MeltdownRadioInterval)
            {
                comp.LastMeltdownMessageAt = comp.MeltdownCountdown;
                SendMeltdownRadio(ent, Loc.GetString("hfr-meltdown-countdown",
                    ("seconds", Math.Max(0, (int) Math.Ceiling(comp.MeltdownCountdown)))));
            }

            // The reactor cooled back above the melting threshold: failsafe disengaged.
            if (comp.Integrity > HFRConstants.MeltdownIntegrityThreshold)
            {
                comp.MeltdownCountdownActive = false;
                comp.MeltdownCountdown = 0f;
                StopMeltdownSiren(comp);
                SendMeltdownRadio(ent, Loc.GetString("hfr-meltdown-cancelled"));
            }
            else if (comp.MeltdownCountdown <= 0f)
            {
                Meltdown(ent, comp);
                return;
            }

            // Keep running the normal loop below (heat exchange + integrity) while
            // counting down; we only special-case the shutdown logic after this.
        }
        else if (comp.Integrity <= HFRConstants.MeltdownIntegrityThreshold)
        {
            // Start the countdown the first time integrity crosses the threshold.
            comp.MeltdownCountdownActive = true;
            comp.MeltdownCountdown = HFRConstants.MeltdownCountdownTime;
            comp.LastMeltdownMessageAt = comp.MeltdownCountdown;
            comp.MeltdownCriticalSoundPlayed = false;
            StartMeltdownSiren(ent);
            _audio.PlayPvs(HFRConstants.MeltdownCriticalSound, ent.Owner);
            SendMeltdownRadio(ent, Loc.GetString("hfr-meltdown-start"));

            // A critical meltdown (Hyper-Noblium + Tritium) gets a station-wide
            // announcement warning about the explosion and EMP, per /tg/ countdown().
            var meltdownFlags = HfrRecipes.All[(int) comp.Recipe].MeltdownFlags;
            if (meltdownFlags.HasFlag(HFRMeltdownFlags.CriticalMeltdown))
            {
                _chat.DispatchStationAnnouncement(ent.Owner,
                    Loc.GetString("hfr-meltdown-critical-announcement"),
                    sender: Loc.GetString("hfr-meltdown-announcer"));
            }
        }

        // A broken reactor (cracked sprite) cannot be operated: force every switch
        // off so it stops burning fuel, producing gas and generating heat until it
        // heals back above the broken threshold.
        if (comp.Integrity <= HFRConstants.BrokenSpriteIntegrityThreshold && !comp.MeltdownCountdownActive)
        {
            if (comp.StartPower || comp.StartFuel || comp.StartModerator)
            {
                comp.StartPower = false;
                comp.StartFuel = false;
                comp.StartModerator = false;
                Dirty(ent);
            }

            comp.PowerLevel = 0f;
            comp.HeatOutput = 0f;
            comp.Endothermic = false;

            // Keep the heat exchange + integrity recovery running so a cooled,
            // undamaged reactor can heal back into service. Cooling is forced on:
            // the panel is blocked while broken, so the player couldn't turn it on
            // themselves, and a hot reactor must be able to shed heat to heal.
            TransferHeat(fusionMix, moderatorMix, coolant, args.dt, coolingOn: true);
            // A broken reactor never fuses, so let it shed residual heat passively
            // too — otherwise it can't cool (and thus can't heal) without a coolant loop. // Pirate
            PassiveShutdownCooling(fusionMix, moderatorMix, args.dt); // Pirate
            UpdateIntegrity(comp, fusionMix, moderatorMix, GetPowerLevel(fusionMix.Temperature), 0f, args.dt);
            comp.FusionTempArchived = fusionMix.Temperature;
            comp.ModeratorTempArchived = moderatorMix.Temperature;
            comp.CoolantTempArchived = coolant.Air.Temperature;
            comp.OutputTempArchived = output.Air.Temperature;

            UpdateVisualState(ent, comp, 0);
            UpdateUiThrottled(ent, args.dt);
            return;
        }

        // --- Temperature rate of change (K/s) for the monitoring chart ---
        comp.FusionTempDelta = dt > 0f ? (fusionMix.Temperature - comp.FusionTempArchived) / dt : 0f;
        comp.ModeratorTempDelta = dt > 0f ? (moderatorMix.Temperature - comp.ModeratorTempArchived) / dt : 0f;
        comp.CoolantTempDelta = dt > 0f ? (coolant.Air.Temperature - comp.CoolantTempArchived) / dt : 0f;
        comp.OutputTempDelta = dt > 0f ? (output.Air.Temperature - comp.OutputTempArchived) / dt : 0f;

        // --- Fuel / moderator injection (gated by their switches, per /tg/ inject_from_side_components) ---
        // /tg/ pulls fuel_injection_rate * seconds_per_tick mol/s of the recipe's required
        // gases only (remove_specific), gated on both fuels being present in the port.
        // NOTE: GasMixture.Remove(amount) removes a fraction amount/TotalMoles, so we use
        // RemoveRatio(min(amount/TotalMoles, 1)) to remove exactly `amount` moles.
        var fuelTransfer = comp.StartFuel ? comp.FuelInjectionRate * dt : 0f;
        var injectRecipe = HfrRecipes.All[(int) comp.Recipe];
        if (fuelTransfer > 0
            && fuel.Air.GetMoles(injectRecipe.PrimaryFuel) > 0
            && fuel.Air.GetMoles(injectRecipe.SecondaryFuel) > 0)
        {
            var removed = fuel.Air.RemoveRatio(Math.Min(fuelTransfer / fuel.Air.TotalMoles, 1f));

            // /tg/ remove_specific: only the required fuel gases enter the fusion mix;
            // any junk gas pulled in with the mix is discarded.
            foreach (var gas in AllGases)
            {
                if (gas != injectRecipe.PrimaryFuel && gas != injectRecipe.SecondaryFuel)
                    removed.SetMoles(gas, 0f);
            }

            removed.Volume = HFRConstants.FusionMixVolume;
            _atmos.Merge(fusionMix, removed);
        }

        var moderatorTransfer = comp.StartModerator ? comp.ModeratorInjectionRate * dt : 0f;
        if (moderatorTransfer > 0 && moderator.Air.TotalMoles > 0)
        {
            var removed = moderator.Air.RemoveRatio(Math.Min(moderatorTransfer / moderator.Air.TotalMoles, 1f));
            removed.Volume = HFRConstants.ModeratorMixVolume;
            _atmos.Merge(moderatorMix, removed);
        }

        // --- Power level from fusion mix temperature ---
        var powerLevel = GetPowerLevel(fusionMix.Temperature);
        comp.PowerLevel = powerLevel;

        // --- Key parameters: F and P ---
        // F = FIR * 0.01 * 5 * power_level, clamped like /tg/ (0.05-30).
        var f = Math.Clamp(
            comp.FuelInjectionRate * 0.01f * HFRConstants.FuelInjectionMultiplier * powerLevel,
            HFRConstants.FuelInjectionMinF,
            HFRConstants.FuelInjectionMaxF);

        // /tg/: heat_limiter_modifier = 5 * 10^power_level * conductor/100.
        var heatLimiter = 0.5f * MathF.Pow(10f, powerLevel - 1) * comp.HeatingConductor;
        comp.HeatLimiterModifier = heatLimiter;

        // The power switch gates the fusion reaction: with it off the reactor
        // does not burn fuel, produce gas or generate heat output.
        var p = 0f;
        var heatMultiplier = 1f;
        // /tg/ check_fuel(): both fuels must be above the mole threshold for the
        // reaction to run at all. Computed here so the passive shutdown cooling
        // below can reuse it. // Pirate
        var hasFuel = fusionMix.GetMoles(injectRecipe.PrimaryFuel) >= HFRConstants.FusionMoleThreshold
            && fusionMix.GetMoles(injectRecipe.SecondaryFuel) >= HFRConstants.FusionMoleThreshold; // Pirate
        if (comp.StartPower)
        {
            // Full /tg/ fusion heat pipeline (density-scaled, per hfr_main_processes.dm):
            // computes the energy balance, the exo/endothermic flip and clamps the heat
            // output to the limiter ([-limiter * 0.01 * neg_mult, limiter * pos_mult]).
            CalculateHeatOutput(comp, fusionMix, moderatorMix, heatLimiter);

            // Passive charge-to-ignition (QoL): with power on and fuel present the
            // core warms on its own below PL1 (500 K), where the fusion reaction
            // takes over — a freshly fueled reactor starts without perfect tuning.
            if (fusionMix.Temperature < HFRConstants.PowerLevel1MaxTemp)
            {
                var fuelMoles = fusionMix.GetMoles(injectRecipe.PrimaryFuel)
                    + fusionMix.GetMoles(injectRecipe.SecondaryFuel);
                if (fuelMoles > 0f)
                    comp.HeatOutput = Math.Max(comp.HeatOutput, Math.Min(
                        HFRConstants.PassiveChargeMaxRate,
                        HFRConstants.PassiveChargeRatePerMole * fuelMoles));
            }

            // Without both fuels above the threshold there is no reaction: the
            // conduction term would otherwise keep a hot, unfueled core self-heating —
            // it reads as "running" at PL1 forever and never cools down. // Pirate
            if (!hasFuel)
            {
                comp.HeatOutput = 0f; // Pirate
                comp.Endothermic = false; // Pirate
            }

            // P (production) per /tg/: PL3-4 uses heat_output/1000, the rest uses
            // heat_output * 2 / 10^(PL+1), always clamped between 0 and F.
            p = powerLevel is 3 or 4
                ? Math.Clamp(comp.HeatOutput / 1000f, 0f, Math.Max(f, 0f))
                : Math.Clamp(comp.HeatOutput * 2f / MathF.Pow(10f, powerLevel + 1), 0f, Math.Max(f, 0f));

            // /tg/: scaled_production = P * spt * gas_production_multiplier.
            var scaledProduction = p * dt * HfrRecipes.All[(int) comp.Recipe].GasProductionModifier;

            // --- Process recipe ---
            if (hasFuel && p > 0 && powerLevel > 0) // Pirate
                ProcessRecipe(comp, fusionMix, moderatorMix, f, scaledProduction, powerLevel, dt);

            // --- Special Anti-Noblium production ---
            if (hasFuel) // Pirate
                ProcessAntiNoblium(comp, fusionMix, moderatorMix, output, powerLevel, dt);

            // --- Moderator gas effects (returns the heat output multiplier) ---
            if (hasFuel) // Pirate
                heatMultiplier = ApplyModeratorEffects(comp, moderatorMix, output, scaledProduction, powerLevel, dt);
        }
        else
        {
            comp.HeatOutput = 0f;
            comp.Endothermic = false;
        }

        // --- Apply heat output to the fusion mix temperature (per /tg/) ---
        // Capped at the fusion maximum scaled by the recipe's temperature multiplier.
        var maxFusionTemp = HFRConstants.FusionMaxTemperature * HfrRecipes.All[(int) comp.Recipe].MaxTemperatureModifier;
        var heatChange = comp.HeatOutput * heatMultiplier * dt;
        if (fusionMix.Temperature <= maxFusionTemp)
            fusionMix.Temperature = Math.Clamp(fusionMix.Temperature + heatChange, HFRConstants.Tcmb, maxFusionTemp);
        else
            fusionMix.Temperature = Math.Max(HFRConstants.Tcmb, fusionMix.Temperature - comp.HeatLimiterModifier * 0.01f * dt);

        // --- Temperature transfer between mixes (cooling switch gates the fast exchange) ---
        TransferHeat(fusionMix, moderatorMix, coolant, dt, comp.StartCooling);

        // --- Passive shutdown cooling ---
        // A dead reaction (no fuel / power off) must shed its residual heat to the
        // environment: without this a reactor that ran out of fuel stays hot forever,
        // reads as "running", and the power switch (locked while hot) never unlocks. // Pirate
        if (!hasFuel || !comp.StartPower) // Pirate
            PassiveShutdownCooling(fusionMix, moderatorMix, dt); // Pirate

        // --- 0.05% moderator gas lost per second per power level ---
        if (powerLevel > 0 && moderatorMix.TotalMoles > 0)
            moderatorMix.Multiply(1f - HFRConstants.ModeratorLossPerPowerLevel * powerLevel * dt);

        // --- Integrity & iron content ---
        UpdateIntegrity(comp, fusionMix, moderatorMix, powerLevel, p, dt);

        // --- Waste removal ---
        ProcessWasteRemoval(comp, fusionMix, moderatorMix, output, powerLevel, dt);

        // --- Radiation ---
        UpdateRadiation(comp, moderatorMix);

        // Output gas takes 95% of the moderator mix temperature
        var outMix = output.Air;
        if (outMix.TotalMoles > 0)
            outMix.Temperature = Math.Max(outMix.Temperature, moderatorMix.Temperature * 0.95f);

        // Archive temperatures for next tick's rate-of-change calculation.
        comp.FusionTempArchived = fusionMix.Temperature;
        comp.ModeratorTempArchived = moderatorMix.Temperature;
        comp.CoolantTempArchived = coolant.Air.Temperature;
        comp.OutputTempArchived = output.Air.Temperature;

        UpdateVisualState(ent, comp, powerLevel);
        UpdateUiThrottled(ent, dt);
    }

    /// <summary>
    ///     Pushes the sprite state (idle / active / broken) to the client only when it changes.
    /// </summary>
    private void UpdateVisualState(Entity<HFRComponent> ent, HFRComponent comp, int powerLevel)
    {
        var state = comp.Integrity <= HFRConstants.BrokenSpriteIntegrityThreshold
            ? HFRVisualState.Broken
            : powerLevel > 0 && comp.StartPower
                ? HFRVisualState.Active
                : HFRVisualState.Idle;

        if (state == comp.VisualState)
            return;

        comp.VisualState = state;
        _appearance.SetData(ent.Owner, HFRVisuals.State, state);

        // Push the visual state to all eight parts too, so their *_active
        // overlays animate while the reactor is running.
        foreach (var partEnum in Enum.GetValues<HFRParts>())
        {
            var part = _multipart.GetPartEntity(new Entity<MultipartMachineComponent?>(ent.Owner, null), partEnum);
            if (part is { } partEnt && !Deleted(partEnt))
                _appearance.SetData(partEnt, HFRVisuals.State, state);
        }

        _ambientSound.SetAmbience(ent.Owner, state == HFRVisualState.Active);
    }

    private static int GetPowerLevel(float temperature)
    {
        return temperature switch
        {
            < HFRConstants.PowerLevel1MaxTemp => 0,
            < HFRConstants.PowerLevel2MaxTemp => 1,
            < HFRConstants.PowerLevel3MaxTemp => 2,
            < HFRConstants.PowerLevel4MaxTemp => 3,
            < HFRConstants.PowerLevel5MaxTemp => 4,
            < HFRConstants.PowerLevel6MaxTemp => 5,
            _ => 6
        };
    }

    /// <summary>
    ///     Full /tg/ fusion heat pipeline (hfr_main_processes.dm fusion_process):
    ///     density-scaled gas amounts -> E=mc² energy -> internal power -> core temperature
    ///     -> conduction/radiation losses -> power output -> heat output clamped to the limiter.
    ///     Because the power scales with the fourth power of the (scaled) fuel, the reactor
    ///     produces almost no heat until the core is dense with fuel — a gradual ignition
    ///     instead of an instant cap-slam.
    /// </summary>
    private void CalculateHeatOutput(HFRComponent comp, GasMixture fusionMix, GasMixture moderatorMix, float heatLimiter)
    {
        var recipe = HfrRecipes.All[(int) comp.Recipe];

        // /tg/: volume = internal_fusion.volume * (magnetic_constrictor * 0.01);
        // scale_factor = volume * 0.5; scaled = max((moles - threshold) / scale_factor, 0).
        // We use 0.25 instead of 0.5: /tg/ expects the fusion mix to be nearly full
        // (moles ~ 2*volume) before the reaction produces meaningful heat, which is
        // impractically slow with realistic canister/pipe fill rates. Halving the
        // scale factor makes scaled fuel = 1 at half the fuel (still > threshold).
        var volume = HFRConstants.FusionMixVolume * (comp.MagneticConstrictor * 0.01f);
        var scaleFactor = Math.Max(volume * 0.25f, 1f);

        float Scaled(float moles) => MathF.Max((moles - HFRConstants.FusionMoleThreshold) / scaleFactor, 0f);
        float Fuel(Gas gas) => Scaled(fusionMix.GetMoles(gas));
        float Mod(Gas gas) => Scaled(moderatorMix.GetMoles(gas));

        var fuel1 = Fuel(recipe.PrimaryFuel);
        var fuel2 = Fuel(recipe.SecondaryFuel);
        var byproduct = Fuel(recipe.MainByproduct);

        // --- Instability: decides if the reaction is exo- or endothermic ---
        // /tg/: toroidal_size = 2*PI + TORADIANS(arctan((volume - break-even) / break-even));
        // instability = MODULUS((gas_power * factor)^2, toroidal_size) + damper*0.01 - iron*0.05.
        // /tg/: BYOND's arctan() returns degrees and is passed through TORADIANS(),
        // which is exactly MathF.Atan()'s radian output in C# — so use it directly.
        var gasPower = GasFusionPowerSum(fusionMix) + 0.75f * GasFusionPowerSum(moderatorMix);
        var toroidalSize = (2f * MathF.PI)
            + MathF.Atan((volume - HFRConstants.ToroidVolumeBreakeven) / HFRConstants.ToroidVolumeBreakeven);
        var instability = Modulus(MathF.Pow(gasPower * HFRConstants.InstabilityGasPowerFactor, 2f), toroidalSize)
            + comp.CurrentDampener * 0.01f - comp.IronContent * 0.05f;
        var internalInstability = instability * 0.5f < HFRConstants.FusionInstabilityEndothermicity ? 1f : -1f;
        comp.Endothermic = internalInstability < 0f;

        // --- Modifiers from the scaled moderator & fuel gases (per /tg/) ---
        var energyModifiers = fuel1 + fuel2 - byproduct
            + Mod(Gas.Nitrogen) * 0.35f + Mod(Gas.CarbonDioxide) * 0.55f + Mod(Gas.NitrousOxide) * 0.95f
            + Mod(Gas.Zauker) * 1.55f + Mod(Gas.AntiNoblium) * 20f
            - Mod(Gas.HyperNoblium) * 10f - Mod(Gas.WaterVapor) * 0.75f - Mod(Gas.Nitrium) * 0.15f
            - Mod(Gas.Healium) * 0.45f - Mod(Gas.Frezon) * 1.15f;

        var powerModifier = Math.Clamp(
            Mod(Gas.Oxygen) * 0.55f + Mod(Gas.CarbonDioxide) * 0.95f + Mod(Gas.Nitrium) * 1.45f
            + Mod(Gas.Zauker) * 5.55f
            + Mod(Gas.Plasma) * 0.05f - Mod(Gas.NitrousOxide) * 0.05f - Mod(Gas.Frezon) * 0.75f
            + fuel2 * 1.05f - byproduct * 0.55f,
            0.25f, 100f);

        var heatModifier = Math.Clamp(
            Mod(Gas.Plasma) * 1.25f - Mod(Gas.Nitrogen) * 0.75f - Mod(Gas.NitrousOxide) * 1.45f
            - Mod(Gas.Frezon) * 0.95f
            + fuel1 * 1.15f + byproduct * 1.05f,
            0.25f, 100f);

        var radiationModifier = Math.Clamp(
            Mod(Gas.Frezon) * 1.15f - Mod(Gas.Nitrogen) * 0.45f - Mod(Gas.Plasma) * 0.95f
            + Mod(Gas.BZ) * 1.9f + Mod(Gas.ProtoNitrate) * 0.1f + Mod(Gas.AntiNoblium) * 10f
            + byproduct,
            0.005f, 1000f);

        // --- E=mc² energy (clamped to avoid NaN, per /tg/) ---
        // Passive charge floor: a fueled core always stores thermal energy, so the
        // metric builds up on its own and never drains to 0 just because moderator
        // gases drag the reaction modifier negative.
        var passiveEnergyModifier = MathF.Max(fuel1 + fuel2, 0f) * HFRConstants.PassiveEnergyFloorFactor;
        var energy = Math.Clamp(
            MathF.Max(energyModifiers, passiveEnergyModifier)
            * HFRConstants.LightSpeed * HFRConstants.LightSpeed
            * MathF.Max(fusionMix.Temperature * heatModifier / 100f, 1f)
            / recipe.EnergyModifier,
            0f, 1e35f);
        comp.Energy = energy;

        // --- Internal power, core temperature and conduction losses ---
        // /tg/: internal_power = (sf1*pm/100) * (sf2*pm/100) * (PI * (2*sf1*rH2*sf2*rTrit)^2) * energy.
        var internalPower = (fuel1 * powerModifier / 100f) * (fuel2 * powerModifier / 100f)
            * (MathF.PI * MathF.Pow(2f * fuel1 * HFRConstants.CalculatedH2Radius * fuel2 * HFRConstants.CalculatedTritRadius, 2f))
            * energy;

        var efficiency = HFRConstants.VoidConduction * Math.Clamp(byproduct, 1f, 100f);
        var coreTemp = MathF.Max(HFRConstants.Tcmb, internalPower * powerModifier / 1000f);
        var deltaTemp = fusionMix.Temperature - coreTemp;
        var conduction = -deltaTemp * (comp.MagneticConstrictor * 0.001f);
        var radiation = MathF.Max(-(HFRConstants.PlanckLightConstant / 5e-18f) * radiationModifier * deltaTemp, 0f);
        var powerOutput = efficiency * (internalPower - conduction - radiation);

        // --- Heat output, clamped to the limiter (per /tg/) ---
        // heat_output = clamp(instability * power_output * heat_modifier / 200, min, max).
        var heatMin = -heatLimiter * 0.01f * recipe.CoolingModifier;
        var heatMax = heatLimiter * recipe.HeatingModifier;
        comp.HeatOutput = Math.Clamp(internalInstability * powerOutput * heatModifier / 200f, heatMin, heatMax);
    }

    /// <summary>
    ///     BYOND's % operator for a non-integer divisor: a - b * round(a / b),
    ///     with rounding away from zero (matching BYOND round()).
    /// </summary>
    private static float Modulus(float a, float b)
    {
        if (b == 0f)
            return 0f;
        return a - b * MathF.Round(a / b, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     Fusion power of each gas for the instability calculation (per /tg/ gas_types.dm
    ///     META_GAS_FUSION_POWER). Gases without an entry contribute nothing.
    /// </summary>
    private static readonly Dictionary<Gas, float> GasFusionPower = new()
    {
        [Gas.Tritium] = 5f,
        [Gas.Hydrogen] = 2f,
        [Gas.Helium] = 7f,
        [Gas.HyperNoblium] = 10f,
        [Gas.AntiNoblium] = 20f,
        [Gas.NitrousOxide] = 10f,
        [Gas.Nitrium] = 7f,
        [Gas.BZ] = 8f,
        [Gas.Pluoxium] = -10f,
        [Gas.Frezon] = -5f,
        [Gas.WaterVapor] = 8f,
    };

    private float GasFusionPowerSum(GasMixture mix)
    {
        var sum = 0f;
        foreach (var (gas, power) in GasFusionPower)
            sum += mix.GetMoles(gas) * power;
        return sum;
    }

    /// <summary>
    ///     Consumes fuel and produces byproducts per the selected recipe (per /tg/ moderator_fuel_process).
    ///     Consumption = F * 0.85 * fuel_consumption_multiplier * spt (same amount from each fuel),
    ///     byproducts = 0.5 * consumption each, tier outputs = P * spt * gas_production_multiplier.
    /// </summary>
    private void ProcessRecipe(HFRComponent comp, GasMixture fusionMix, GasMixture moderatorMix, float f, float scaledProduction, int powerLevel, float dt)
    {
        var recipe = HfrRecipes.All[(int) comp.Recipe];

        // /tg/: fuel_consumption = consumption_amount * 0.85 * fuel_consumption_multiplier.
        // FuelConsumptionMultiplier is a server balance knob (x2) so the fusion mix
        // doesn't flood: injection runs at up to FIR=150 mol/s while /tg/ consumption
        // caps at 25.5 mol/s per gas. The per-power-level factor softens the burn at
        // PL3-6 (F scales with power level, so unmodified high levels gulp fuel).
        var consumption = f * 0.85f * recipe.FuelConsumptionModifier * HFRConstants.FuelConsumptionMultiplier
            * HFRConstants.FuelConsumptionPowerLevelFactor[Math.Clamp(powerLevel, 0, 6)] * dt;

        var primaryAvailable = fusionMix.GetMoles(recipe.PrimaryFuel);
        var secondaryAvailable = fusionMix.GetMoles(recipe.SecondaryFuel);

        // Scale down if we don't have enough fuel (min() per gas in /tg/).
        var scale = 1f;
        if (primaryAvailable < consumption)
            scale = Math.Min(scale, primaryAvailable / Math.Max(consumption, 1e-6f));
        if (secondaryAvailable < consumption)
            scale = Math.Min(scale, secondaryAvailable / Math.Max(consumption, 1e-6f));
        if (scale <= 0f)
            return;

        var consumed = consumption * scale;
        fusionMix.AdjustMoles(recipe.PrimaryFuel, -consumed);
        fusionMix.AdjustMoles(recipe.SecondaryFuel, -consumed);

        // Each primary product gets 0.5 * consumption (per /tg/).
        var byproduct = consumed * 0.5f;
        fusionMix.AdjustMoles(recipe.MainByproduct, byproduct);
        if (recipe.OtherByproduct is { } other)
            fusionMix.AdjustMoles(other, byproduct);

        // Tier outputs go to the moderator mix per power level (per /tg/).
        var tiers = HfrRecipes.Tiers[(int) comp.Recipe];
        var tierRates = TierRatesByPowerLevel[Math.Clamp(powerLevel, 0, 6)];

        // /tg/ bonuses: plasma in the moderator boosts the tier-3/5 outputs at PL2/PL4.
        var plasma = moderatorMix.GetMoles(Gas.Plasma);
        if (powerLevel == 2 && plasma > 50f && recipe.Tier3 is { } t3)
            moderatorMix.AdjustMoles(t3, 1.15f * scaledProduction);
        if (powerLevel == 4 && plasma > 50f && recipe.Tier5 is { } t5)
            moderatorMix.AdjustMoles(t5, 1.15f * scaledProduction);

        for (var i = 0; i < tiers.Length; i++)
        {
            if (tiers[i] is not { } tierGas || tierRates[i] <= 0f)
                continue;

            moderatorMix.AdjustMoles(tierGas, tierRates[i] * scaledProduction);
        }
    }

    /// <summary>
    ///     Special free Anti-Noblium production (per /tg/ moderator_common_process):
    ///     dirty_production_rate = scaled main byproduct / FIR.
    ///     PL5: to output (below 1e7 K, or with plasma+BZ in the moderator), rate 0.9/0.065.
    ///     PL6: to output when BZ is present (clamped, 10/s), rate 1/0.045; to fusion, rate 0.01/0.095.
    /// </summary>
    private void ProcessAntiNoblium(HFRComponent comp, GasMixture fusionMix, GasMixture moderatorMix, PipeNode output, int powerLevel, float dt)
    {
        var recipe = HfrRecipes.All[(int) comp.Recipe];
        var volume = HFRConstants.FusionMixVolume * (comp.MagneticConstrictor * 0.01f);
        var scaleFactor = Math.Max(volume * 0.25f, 1f);
        var dirty = Math.Max((fusionMix.GetMoles(recipe.MainByproduct) - HFRConstants.FusionMoleThreshold) / scaleFactor, 0f)
            / Math.Max(comp.FuelInjectionRate, 1f);
        if (dirty <= 0f)
            return;

        if (powerLevel == 5
            && (moderatorMix.Temperature < 1e7f
                || (moderatorMix.GetMoles(Gas.Plasma) >= 100f && moderatorMix.GetMoles(Gas.BZ) >= 50f)))
        {
            output.Air.AdjustMoles(Gas.AntiNoblium, dirty * HFRConstants.AntiNobliumOutputRate * dt);
        }

        if (powerLevel == 6)
        {
            if (moderatorMix.GetMoles(Gas.BZ) > 0f)
                output.Air.AdjustMoles(Gas.AntiNoblium,
                    Math.Min(dirty * HFRConstants.AntiNobliumBzRate, HFRConstants.AntiNobliumBzMaxPerSecond) * dt);

            fusionMix.AdjustMoles(Gas.AntiNoblium, dirty * HFRConstants.AntiNobliumFusionRate * dt);
        }
    }

    /// <summary>
    ///     Moderator gas effects — the production gases.
    ///     Levels and thresholds match /tg/ moderator_common_process(); returns the
    ///     heat output multiplier applied after the heat limiter clamp.
    /// </summary>
    private float ApplyModeratorEffects(HFRComponent comp, GasMixture moderatorMix, PipeNode output, float p, int powerLevel, float dt)
    {
        var heatMultiplier = 1f;

        void Consume(Gas gas, float amount)
        {
            moderatorMix.AdjustMoles(gas, -Math.Min(moderatorMix.GetMoles(gas), amount));
        }

        switch (powerLevel)
        {
            case 1:
                if (moderatorMix.GetMoles(Gas.Plasma) > 100f)
                {
                    Consume(Gas.Plasma, 0.85f * p);
                    output.Air.AdjustMoles(Gas.NitrousOxide, 0.5f * p);
                }
                // /tg/: BZ > 150 produces Halon at PL1.
                if (moderatorMix.GetMoles(Gas.BZ) > 150f)
                {
                    Consume(Gas.BZ, 0.95f * p);
                    output.Air.AdjustMoles(Gas.Halon, 0.55f * p);
                }
                break;
            case 2:
                if (moderatorMix.GetMoles(Gas.Plasma) > 50f)
                {
                    Consume(Gas.Plasma, 1.75f * p);
                    output.Air.AdjustMoles(Gas.BZ, 1.8f * p);
                }
                // /tg/: Proto-Nitrate > 20 boosts PL2 output slightly.
                if (moderatorMix.GetMoles(Gas.ProtoNitrate) > 20f)
                {
                    Consume(Gas.ProtoNitrate, 1.35f * p);
                    output.Air.AdjustMoles(Gas.Nitrium, 1.05f * p);
                    heatMultiplier *= 1.025f;
                }
                break;
            case 3:
            case 4:
                if (moderatorMix.GetMoles(Gas.Plasma) > 10f)
                {
                    Consume(Gas.Plasma, 0.45f * p);
                    output.Air.AdjustMoles(Gas.Frezon, 0.15f * p);
                    output.Air.AdjustMoles(Gas.Nitrium, 1.05f * p);
                }
                if (moderatorMix.GetMoles(Gas.Frezon) > 50f)
                    heatMultiplier *= 0.9f;
                // /tg/: Proto-Nitrate > 15 at PL3-4: +25% heat, produces Nitrium + Halon.
                if (moderatorMix.GetMoles(Gas.ProtoNitrate) > 15f)
                {
                    Consume(Gas.ProtoNitrate, 1.55f * p);
                    output.Air.AdjustMoles(Gas.Nitrium, 1.25f * p);
                    output.Air.AdjustMoles(Gas.Halon, 1.15f * p);
                    heatMultiplier *= 1.25f;
                }
                if (moderatorMix.GetMoles(Gas.BZ) > 100f)
                    output.Air.AdjustMoles(Gas.Healium, 1.5f * p);
                break;
            case 5:
                if (moderatorMix.GetMoles(Gas.Plasma) > 15f)
                {
                    Consume(Gas.Plasma, 1.45f * p);
                    output.Air.AdjustMoles(Gas.Frezon, 0.25f * p);
                }
                if (moderatorMix.GetMoles(Gas.Frezon) > 500f)
                    heatMultiplier *= 0.5f;
                // /tg/: Proto-Nitrate > 50 at PL5: +25% heat, produces Nitrium + Pluoxium.
                if (moderatorMix.GetMoles(Gas.ProtoNitrate) > 50f)
                {
                    Consume(Gas.ProtoNitrate, 1.35f * p);
                    output.Air.AdjustMoles(Gas.Nitrium, 1.95f * p);
                    output.Air.AdjustMoles(Gas.Pluoxium, 1f * p);
                    heatMultiplier *= 1.25f;
                }
                if (moderatorMix.GetMoles(Gas.BZ) > 100f)
                {
                    output.Air.AdjustMoles(Gas.Healium, 1f * p);
                    output.Air.AdjustMoles(Gas.Frezon, 1.15f * p);
                }
                break;
            case 6:
                if (moderatorMix.GetMoles(Gas.Plasma) > 30f)
                {
                    Consume(Gas.Plasma, 1.45f * p);
                    output.Air.AdjustMoles(Gas.BZ, 1.15f * p);
                }
                // /tg/: any Proto-Nitrate at PL6: +125% heat, produces Zauker + Nitrium.
                if (moderatorMix.GetMoles(Gas.ProtoNitrate) > 0f)
                {
                    Consume(Gas.ProtoNitrate, 3.35f * p);
                    output.Air.AdjustMoles(Gas.Zauker, 5.35f * p);
                    output.Air.AdjustMoles(Gas.Nitrium, 2.15f * p);
                    heatMultiplier *= 2.25f;
                }
                break;
        }

        // Healium: directly heals a damaged core at high power levels (per /tg/).
        // /tg/: if critical_threshold_proximity > 400, heal healium/100 * seconds_per_tick,
        // consuming scaled_production * 20 healium.
        var healiumMoles = moderatorMix.GetMoles(Gas.Healium);
        if (powerLevel is 5 or 6
            && healiumMoles > 100f
            && comp.Integrity < HFRConstants.HealiumIntegrityThreshold)
        {
            Consume(Gas.Healium, HFRConstants.HealiumConsumptionFactor * p);
            // /tg/ heals healium/100 per second against the melting-point scale (900),
            // which is 0.11% integrity per 100 moles per tick here.
            comp.Integrity = Math.Min(HFRConstants.MaxIntegrity,
                comp.Integrity + HFRConstants.HealiumIntegrityRestorePerHundredMoles * (healiumMoles / 100f) * dt);
        }

        return heatMultiplier;
    }

    /// <summary>
    ///     Heat exchange per /tg/ process_internal_cooling(): exponential approach to
    ///     equilibrium, gated by the cooling switch. Both the fusion <-> moderator and
    ///     moderator <-> coolant exchanges live under the same switch, as in /tg/.
    ///     heat_amount = (1 - (1 - conductivity)^dt) * delta * (C1*C2/(C1+C2)).
    /// </summary>
    private void TransferHeat(GasMixture fusionMix, GasMixture moderatorMix, PipeNode coolant, float dt, bool coolingOn)
    {
        if (!coolingOn)
            return;

        // Fusion <-> moderator (metallic void conduction).
        if (fusionMix.TotalMoles > 0f && moderatorMix.TotalMoles > 0f)
        {
            var fusionCap = _atmos.GetHeatCapacity(fusionMix, true);
            var modCap = _atmos.GetHeatCapacity(moderatorMix, true);
            if (fusionCap > Atmospherics.MinimumHeatCapacity && modCap > Atmospherics.MinimumHeatCapacity)
            {
                var delta = fusionMix.Temperature - moderatorMix.Temperature;
                var heat = (1f - MathF.Pow(1f - HFRConstants.MetallicVoidConductivity, dt)) * delta
                    * (fusionCap * modCap / (fusionCap + modCap));
                fusionMix.Temperature = MathF.Max(fusionMix.Temperature - heat / fusionCap, HFRConstants.Tcmb);
                moderatorMix.Temperature = MathF.Max(moderatorMix.Temperature + heat / modCap, HFRConstants.Tcmb);
            }
        }

        // Moderator <-> coolant loop (high-efficiency conductivity).
        var coolantAir = coolant.Air;
        if (moderatorMix.TotalMoles > 0f && coolantAir.TotalMoles > 0f)
        {
            var modCap = _atmos.GetHeatCapacity(moderatorMix, true);
            var coolantCap = _atmos.GetHeatCapacity(coolantAir, true);
            if (modCap > Atmospherics.MinimumHeatCapacity && coolantCap > Atmospherics.MinimumHeatCapacity)
            {
                var delta = coolantAir.Temperature - moderatorMix.Temperature;
                var heat = (1f - MathF.Pow(1f - HFRConstants.HighEfficiencyConductivity, dt)) * delta
                    * (coolantCap * modCap / (coolantCap + modCap));
                coolantAir.Temperature = MathF.Max(coolantAir.Temperature - heat / coolantCap, HFRConstants.Tcmb);
                moderatorMix.Temperature = MathF.Max(moderatorMix.Temperature + heat / modCap, HFRConstants.Tcmb);
            }
        }
        // /tg/ process_internal_cooling(): with an empty moderator mix the coolant
        // exchanges heat directly with the fusion mix. Without this fallback a reactor
        // run without moderator gas could never shed heat and would stay hot forever. // Pirate
        else if (fusionMix.TotalMoles > 0f && coolantAir.TotalMoles > 0f)
        {
            var fusionCap = _atmos.GetHeatCapacity(fusionMix, true);
            var coolantCap = _atmos.GetHeatCapacity(coolantAir, true);
            if (fusionCap > Atmospherics.MinimumHeatCapacity && coolantCap > Atmospherics.MinimumHeatCapacity)
            {
                var delta = coolantAir.Temperature - fusionMix.Temperature;
                var heat = (1f - MathF.Pow(1f - HFRConstants.MetallicVoidConductivity, dt)) * delta
                    * (coolantCap * fusionCap / (coolantCap + fusionCap));
                coolantAir.Temperature = MathF.Max(coolantAir.Temperature - heat / coolantCap, HFRConstants.Tcmb);
                fusionMix.Temperature = MathF.Max(fusionMix.Temperature + heat / fusionCap, HFRConstants.Tcmb);
            }
        }
    }

    /// <summary>
    ///     Passive shutdown cooling: the core sheds residual heat to the environment
    ///     when the reaction is dead (no fuel / power off / broken). Exponential
    ///     approach to room temperature, ~5% of the delta per second, so a hot
    ///     unfueled reactor cools to ambient in about a minute and the power switch
    ///     (locked while hot) unlocks again.
    /// </summary>
    private static void PassiveShutdownCooling(GasMixture fusionMix, GasMixture moderatorMix, float dt)
    {
        var cooling = 1f - MathF.Pow(1f - HFRConstants.PassiveCoolingConductivity, dt);
        var ambient = Atmospherics.T20C;
        fusionMix.Temperature = MathF.Max(fusionMix.Temperature - cooling * (fusionMix.Temperature - ambient), HFRConstants.Tcmb);
        moderatorMix.Temperature = MathF.Max(moderatorMix.Temperature - cooling * (moderatorMix.Temperature - ambient), HFRConstants.Tcmb);
    }

    /// <summary>
    ///     Integrity and iron content, per the guide's damage/healing formulas.
    /// </summary>
    private void UpdateIntegrity(HFRComponent comp, GasMixture fusionMix, GasMixture moderatorMix, int powerLevel, float p, float dt)
    {
        var damage = 0f;

        if (powerLevel >= 5)
        {
            // Damage from volume and temperature.
            damage += (((fusionMix.TotalMoles * 1e5f + fusionMix.Temperature) / 1e5f) - 2500f) / 200f * dt;
            damage += Math.Max(0f, MathF.Log10(fusionMix.Temperature) - 5f) * dt;

            // Iron content damage.
            damage += (MathF.Round(comp.IronContent / 100f) - 1f) * dt;
        }

        // Healing: only ever reduces damage, never adds it (per /tg/ min(restore, 0)).
        // The (800 - moles) term must be clamped at 0: above 800 moles an unclamped
        // formula turns into damage and the reactor bleeds integrity for no reason.
        if (fusionMix.TotalMoles < 1200f || powerLevel <= 4)
            damage -= MathF.Max(0f, (800f - fusionMix.TotalMoles) / 150f) * dt;

        if (fusionMix.Temperature < 5e5f && powerLevel <= 4 && fusionMix.TotalMoles > 0)
            damage -= Math.Max(0f, MathF.Log10(fusionMix.Temperature) - 5.5f) * dt;

        // Cap: lose at most 0.5% integrity per tick.
        comp.Integrity = Math.Clamp(comp.Integrity - damage * HFRConstants.MaxIntegrityDamagePerTick, 0f, 100f);

        // Iron content (0-300 scale, x100 of /tg/'s 0-1). Per /tg/ process_damageheal:
        // PL>4 accumulates (prob 17*PL per second — PL5 ~85%, PL6 100%), PL<=4 recovers
        // 1 point per second with prob 25/(PL+1) — PL0 25%, PL1 12.5%, ..., PL4 5%.
        var iron = comp.IronContent;
        if (powerLevel >= 6)
        {
            iron += HFRConstants.IronContentDamagePerSecond * dt;
        }
        else if (powerLevel == 5)
        {
            // ~85% per second: modulo of a 1/0.85-period counter.
            if ((iron % (100f / (HFRConstants.IronChancePerFusionLevel * powerLevel))) < dt)
                iron += HFRConstants.IronContentDamagePerSecond * dt;
        }
        else if (iron > 0f)
        {
            // Recovery prob 25/(PL+1) per second: modulo period = 4*(PL+1).
            if ((iron % (4f * (powerLevel + 1))) < dt)
                iron -= 1f * dt;
        }

        // Oxygen burns away iron content rapidly (per /tg/ moderator_common_process):
        // with >150 mol of O2 in the moderator mix, iron is removed at a fixed rate
        // and the oxygen is consumed. This is the intended way to clean "metal
        // fragments" out of a running reactor.
        if (moderatorMix.GetMoles(Gas.Oxygen) > 150f && iron > 0f)
        {
            var ironRemoved = Math.Min(HFRConstants.IronOxygenHealPerSecond * dt, iron);
            iron -= ironRemoved;
            moderatorMix.AdjustMoles(Gas.Oxygen, -ironRemoved * HFRConstants.OxygenMolesConsumedPerIronHeal);
        }
        comp.IronContent = Math.Clamp(iron, 0f, 300f);
    }

    /// <summary>
    ///     Sends a meltdown warning over the engineering radio channel
    ///     (per /tg/ radio.talk_into in the HFR core).
    /// </summary>
    private void SendMeltdownRadio(Entity<HFRComponent> ent, string message)
    {
        _radio.SendRadioMessage(ent.Owner, message, HFRConstants.MeltdownRadioChannel, ent.Owner);
    }

    /// <summary>
    ///     Resets a reactor to a pristine, non-meltdown state: stops the siren,
    ///     clears the countdown, restores full integrity and removes iron buildup.
    ///     Used by the admin command to re-test the meltdown loop.
    /// </summary>
    public void ResetReactor(Entity<HFRComponent> ent)
    {
        var comp = ent.Comp;
        StopMeltdownSiren(comp);
        comp.MeltdownCountdownActive = false;
        comp.MeltdownCountdown = 0f;
        comp.MeltdownCriticalSoundPlayed = false;
        comp.Integrity = HFRConstants.MaxIntegrity;
        comp.IronContent = 0f;
        Dirty(ent);
        UpdateVisualState(ent, comp, 0);
        UpdateUI(ent);
    }

    /// <summary>
    ///     Starts the looping meltdown siren played from the reactor while the
    ///     countdown runs (per /tg/ SFX_HYPERTORUS_MELTING accent sound).
    /// </summary>
    private void StartMeltdownSiren(Entity<HFRComponent> ent)
    {
        if (ent.Comp.MeltdownSirenStream != null)
            return;

        var stream = _audio.PlayPvs(HFRConstants.MeltdownSirenSound, ent.Owner,
            AudioParams.Default.WithLoop(true).WithVolume(2f));
        ent.Comp.MeltdownSirenStream = stream?.Entity;
    }

    /// <summary>
    ///     Stops the looping meltdown siren, if one is playing.
    /// </summary>
    private void StopMeltdownSiren(HFRComponent comp)
    {
        comp.MeltdownSirenStream = _audio.Stop(comp.MeltdownSirenStream);
    }

    /// <summary>
    ///     The reactor has run out of countdown time: it melts down.
    ///     Explosion size, EMP, radiation pulse and gas spread all follow the
    ///     selected fuel's meltdown flags (ported from /tg/ meltdown()).
    /// </summary>
    private void Meltdown(Entity<HFRComponent> ent, HFRComponent comp)
    {
        var flags = HfrRecipes.All[(int) comp.Recipe].MeltdownFlags;
        var powerLevel = Math.Max(1, (int) MathF.Round(comp.PowerLevel));
        var critical = flags.HasFlag(HFRMeltdownFlags.CriticalMeltdown);

        // --- Explosion radii, per /tg/ meltdown() ---
        var flash = 0f;
        var light = 0f;
        var heavy = 0f;
        var devastating = 0f;

        if (flags.HasFlag(HFRMeltdownFlags.DevastatingExplosion))
        {
            flash = powerLevel * 8;
            light = powerLevel * 7;
            heavy = powerLevel * 2;
            devastating = powerLevel;
        }
        else if (flags.HasFlag(HFRMeltdownFlags.MediumExplosion))
        {
            flash = powerLevel * 6;
            light = powerLevel * 5;
            heavy = powerLevel * 0.5f;
        }
        else // BaseExplosion
        {
            flash = powerLevel * 3;
            light = powerLevel * 2;
        }

        if (critical)
        {
            devastating *= 2;
            heavy *= 2;
        }

        var coords = _xform.GetMapCoordinates(ent.Owner);

        // Convert the /tg/ light-impact radius into an SS14 explosion intensity,
        // with a slope of 1 (light falloff).
        if (light > 0f)
        {
            var totalIntensity = _explosion.RadiusToIntensity(light, 1f);
            _explosion.QueueExplosion(coords, ExplosionSystem.DefaultExplosionPrototypeId,
                totalIntensity, 1f, light, ent.Owner);
        }

        // --- Radiation pulse (per /tg/ radiation_pulse) ---
        if (flags.HasFlag(HFRMeltdownFlags.RadiationPulse))
        {
            var radSize = flags switch
            {
                _ when flags.HasFlag(HFRMeltdownFlags.MassiveSpread) => powerLevel + 44,
                _ when flags.HasFlag(HFRMeltdownFlags.BigSpread) => powerLevel + 34,
                _ when flags.HasFlag(HFRMeltdownFlags.MediumSpread) => powerLevel + 24,
                _ => powerLevel * 2 + 8,
            };

            // A temporary radiation source that decays quickly, creating a pulse.
            // RadiationSourceComponent is server-side only (not networked): Dirty() on
            // it asserts in debug builds and would crash the server, so just set the
            // field — the radiation system reads it directly (same as ReactorPartSystem). // Pirate
            var radEnt = Spawn(null, coords);
            var source = EnsureComp<RadiationSourceComponent>(radEnt);
            source.Intensity = Math.Max(radSize * 2f, 50f);
            var despawn = EnsureComp<TimedDespawnComponent>(radEnt);
            despawn.Lifetime = 10f;
        }

        // --- EMP pulse (per /tg/ empulse) ---
        if (flags.HasFlag(HFRMeltdownFlags.Emp))
        {
            var (empLight, empHeavy) = flags switch
            {
                _ when flags.HasFlag(HFRMeltdownFlags.MassiveSpread) => (powerLevel * 9, powerLevel * 7),
                _ when flags.HasFlag(HFRMeltdownFlags.BigSpread) => (powerLevel * 7, powerLevel * 5),
                _ when flags.HasFlag(HFRMeltdownFlags.MediumSpread) => (powerLevel * 5, powerLevel * 3),
                _ => (powerLevel * 3, powerLevel * 1),
            };
            _emp.EmpPulse(coords, empLight, empHeavy * 500f, TimeSpan.FromSeconds(30));
        }

        // --- Gas spread: 20% of both mixes dumped into the surrounding atmosphere ---
        SpreadMeltdownGas(ent, comp);

        // --- Stop the siren and destroy the machine ---
        StopMeltdownSiren(comp);
        DeleteMachine(ent);
    }

    /// <summary>
    ///     Dumps 20% of the fusion and moderator mixes into the surrounding tiles
    ///     (per /tg/ meltdown gas_spread).
    /// </summary>
    private void SpreadMeltdownGas(Entity<HFRComponent> ent, HFRComponent comp)
    {
        var surround = _atmos.GetContainingMixture(ent.Owner, false, true);
        if (surround == null)
            return;

        void Dump(GasMixture mix)
        {
            if (mix.TotalMoles <= 0f)
                return;

            var dumped = mix.RemoveRatio(0.2f);
            _atmos.Merge(surround, dumped);
        }

        Dump(comp.FusionMix);
        Dump(comp.ModeratorMix);
    }

    /// <summary>
    ///     Removes the core and all eight parts, plus the internal pipes.
    /// </summary>
    private void DeleteMachine(Entity<HFRComponent> ent)
    {
        var core = ent.Owner;
        var multipart = new Entity<MultipartMachineComponent?>(core, null);
        if (Resolve(multipart, ref multipart.Comp, false))
        {
            foreach (var partEnum in Enum.GetValues<HFRParts>())
            {
                var part = _multipart.GetPartEntity(multipart, partEnum);
                if (part is { } partEnt && !Deleted(partEnt))
                    QueueDel(partEnt);
            }
        }

        QueueDel(core);
    }

    /// <summary>
    ///     Waste removal: 50% of byproducts, 5% of fusion Anti-Noblium and up to 20 mol/s of the
    ///     moderator filter gas moved to output. Forcibly disabled at power level 6.
    /// </summary>
    private void ProcessWasteRemoval(HFRComponent comp, GasMixture fusionMix, GasMixture moderatorMix, PipeNode output, int powerLevel, float dt)
    {
        if (!comp.WasteRemoval || powerLevel >= HFRConstants.WasteRemovalForceOffPowerLevel)
            return;

        var recipe = HfrRecipes.All[(int) comp.Recipe];

        // 50% of byproducts per second.
        var main = fusionMix.GetMoles(recipe.MainByproduct) * HFRConstants.WasteRemovalByproductFraction * dt;
        if (main > 0f)
        {
            fusionMix.AdjustMoles(recipe.MainByproduct, -main);
            output.Air.AdjustMoles(recipe.MainByproduct, main);
        }

        if (recipe.OtherByproduct is { } other)
        {
            var otherMoles = fusionMix.GetMoles(other) * HFRConstants.WasteRemovalByproductFraction * dt;
            if (otherMoles > 0f)
            {
                fusionMix.AdjustMoles(other, -otherMoles);
                output.Air.AdjustMoles(other, otherMoles);
            }
        }

        // 5% of fusion Anti-Noblium per second.
        var anti = fusionMix.GetMoles(Gas.AntiNoblium) * HFRConstants.WasteRemovalAntiNobliumFraction * dt;
        if (anti > 0f)
        {
            fusionMix.AdjustMoles(Gas.AntiNoblium, -anti);
            output.Air.AdjustMoles(Gas.AntiNoblium, anti);
        }

        // Moderator filter gas, at the user-set filtering rate.
        if (comp.ModeratorFilter is { } filterGas)
        {
            var filterAmount = Math.Min(moderatorMix.GetMoles(filterGas), comp.ModeratorFilteringRate * dt);
            if (filterAmount > 0f)
            {
                moderatorMix.AdjustMoles(filterGas, -filterAmount);
                output.Air.AdjustMoles(filterGas, filterAmount);
            }
        }
    }

    private void UpdateRadiation(HFRComponent comp, GasMixture moderatorMix)
    {
        var source = EnsureComp<RadiationSourceComponent>(comp.Owner);
        // BZ massively increases radiation; Anti-Noblium adds radiation too.
        var rads = moderatorMix.GetMoles(Gas.BZ) * 0.05f
            + moderatorMix.GetMoles(Gas.AntiNoblium) * 0.1f;
        source.Intensity = Math.Clamp(rads, 0f, 100f);
    }

    #endregion

    #region UI

    private void OnUiOpened(Entity<HFRComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(HFRUiKey.Key))
            UpdateUI(ent);
    }

    private void OnSetRecipe(Entity<HFRComponent> ent, ref HFRSetRecipeMessage args)
    {
        if (args.Recipe < 0 || args.Recipe >= HfrRecipes.All.Length)
            return;
        ent.Comp.Recipe = (HfrRecipe) args.Recipe;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetHeatingConductor(Entity<HFRComponent> ent, ref HFRSetHeatingConductorMessage args)
    {
        ent.Comp.HeatingConductor = Math.Clamp(args.Value, 50f, 500f);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetMagneticConstrictor(Entity<HFRComponent> ent, ref HFRSetMagneticConstrictorMessage args)
    {
        ent.Comp.MagneticConstrictor = Math.Clamp(args.Value, 50f, 1000f);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetFuelInjectionRate(Entity<HFRComponent> ent, ref HFRSetFuelInjectionRateMessage args)
    {
        // /tg/ range: 0.5-150 mol/s.
        ent.Comp.FuelInjectionRate = Math.Clamp(args.Value, 0.5f, 150f);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetCurrentDampener(Entity<HFRComponent> ent, ref HFRSetCurrentDampenerMessage args)
    {
        ent.Comp.CurrentDampener = Math.Clamp(args.Value, 0f, 1000f);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetModeratorInjectionRate(Entity<HFRComponent> ent, ref HFRSetModeratorInjectionRateMessage args)
    {
        ent.Comp.ModeratorInjectionRate = Math.Clamp(args.Value, 0.5f, 150f);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnToggleWasteRemoval(Entity<HFRComponent> ent, ref HFRToggleWasteRemovalMessage args)
    {
        ent.Comp.WasteRemoval = args.Enabled;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetPower(Entity<HFRComponent> ent, ref HFRSetPowerMessage args)
    {
        // A broken reactor cannot be powered back on.
        if (args.On && ent.Comp.Integrity <= HFRConstants.BrokenSpriteIntegrityThreshold)
            return;

        ent.Comp.StartPower = args.On;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetCooling(Entity<HFRComponent> ent, ref HFRSetCoolingMessage args)
    {
        ent.Comp.StartCooling = args.On;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetFuelSwitch(Entity<HFRComponent> ent, ref HFRSetFuelSwitchMessage args)
    {
        // Fuel injection is gated by the power switch anyway, but refuse outright
        // on a broken reactor.
        if (args.On && ent.Comp.Integrity <= HFRConstants.BrokenSpriteIntegrityThreshold)
            return;

        ent.Comp.StartFuel = args.On;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetModeratorSwitch(Entity<HFRComponent> ent, ref HFRSetModeratorSwitchMessage args)
    {
        ent.Comp.StartModerator = args.On;
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetModeratorFilteringRate(Entity<HFRComponent> ent, ref HFRSetModeratorFilteringRateMessage args)
    {
        ent.Comp.ModeratorFilteringRate = Math.Clamp(args.Value, 5f, 200f);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnSetModeratorFilter(Entity<HFRComponent> ent, ref HFRSetModeratorFilterMessage args)
    {
        // GasId 0 = None, otherwise a Gas enum value + 1 to allow 0 to be "none" and Oxygen=1.
        // Cast to Gas first: Enum.IsDefined throws ArgumentException when passed an int while
        // the Gas enum's underlying type is sbyte.
        ent.Comp.ModeratorFilter = args.GasId switch
        {
            0 => null,
            var id when Enum.IsDefined(typeof(Gas), (Gas) (id - 1)) => (Gas) (id - 1),
            _ => null
        };
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnEmergencyShutdown(Entity<HFRComponent> ent, ref HFREmergencyShutdownMessage args)
    {
        var comp = ent.Comp;
        // Minimum of the valid 50-500 range: a 0 conductor would leave the reactor
        // unable to heat at all when restarted (heat limiter = 0).
        comp.HeatingConductor = 50f;
        comp.FuelInjectionRate = 5f;
        comp.CurrentDampener = 0f;
        comp.ModeratorInjectionRate = 0f;
        comp.WasteRemoval = false;
        comp.Endothermic = true;
        comp.StartPower = false;
        comp.StartFuel = false;
        comp.StartModerator = false;
        // Force cooling on: the whole point of an emergency shutdown is to shed
        // heat. Without it a stopped reactor stays hot, integrity never recovers,
        // and a meltdown countdown (if one is running) can never be cancelled.
        comp.StartCooling = true;
        Dirty(ent);
        UpdateUI(ent);
    }

    /// <summary>
    ///     Pushes the UI state at most once per <see cref="HFRConstants.UiUpdateInterval"/>.
    ///     Used from the per-tick update so a running reactor doesn't rebuild the client
    ///     window every atmos tick (up to 20x/s), which stutters the client near the
    ///     reactor. Event handlers (switches, recipe, emergency shutdown) still call
    ///     <see cref="UpdateUI"/> directly for instant feedback.
    /// </summary>
    private void UpdateUiThrottled(Entity<HFRComponent> ent, float dt)
    {
        var comp = ent.Comp;
        comp.UiUpdateAccumulator += dt;
        if (comp.UiUpdateAccumulator < HFRConstants.UiUpdateInterval)
            return;

        comp.UiUpdateAccumulator = 0f;
        UpdateUI(ent);
    }

    private void UpdateUI(Entity<HFRComponent> ent)
    {
        var comp = ent.Comp;
        var fusionMix = comp.FusionMix;
        var moderatorMix = comp.ModeratorMix;

        var coolantTemp = GetPipe(ent, HFRParts.Interface, out var coolant) ? coolant.Air.Temperature : 0f;
        var outputConnected = GetPipe(ent, HFRParts.WasteOutput, out var output);
        var outputTemp = output is { } outPipe ? outPipe.Air.Temperature : 0f;

        var state = new HFRBoundUserInterfaceState
        {
            FusionTemperature = fusionMix.Temperature,
            FusionMoles = fusionMix.TotalMoles,
            ModeratorTemperature = moderatorMix.Temperature,
            ModeratorMoles = moderatorMix.TotalMoles,
            CoolantTemperature = coolantTemp,
            OutputTemperature = outputTemp,
            OutputConnected = outputConnected,
            PowerLevel = comp.PowerLevel,
            Integrity = comp.Integrity,
            IronContent = comp.IronContent,
            HeatOutput = comp.HeatOutput,
            HeatLimiter = comp.HeatLimiterModifier,
            Energy = comp.Energy,
            Endothermic = comp.Endothermic,
            MeltdownActive = comp.MeltdownCountdownActive,
            MeltdownCountdown = comp.MeltdownCountdown,
            FusionTempDelta = comp.FusionTempDelta,
            ModeratorTempDelta = comp.ModeratorTempDelta,
            CoolantTempDelta = comp.CoolantTempDelta,
            OutputTempDelta = comp.OutputTempDelta,
            FusionGases = GetGasBreakdown(fusionMix),
            ModeratorGases = GetGasBreakdown(moderatorMix),
            HeatingConductor = comp.HeatingConductor,
            MagneticConstrictor = comp.MagneticConstrictor,
            FuelInjectionRate = comp.FuelInjectionRate,
            CurrentDampener = comp.CurrentDampener,
            ModeratorInjectionRate = comp.ModeratorInjectionRate,
            WasteRemoval = comp.WasteRemoval,
            StartPower = comp.StartPower,
            StartCooling = comp.StartCooling,
            StartFuel = comp.StartFuel,
            StartModerator = comp.StartModerator,
            ModeratorFilteringRate = comp.ModeratorFilteringRate,
            Recipe = (byte) comp.Recipe,
            ModeratorFilterId = comp.ModeratorFilter is { } filter ? (int) filter + 1 : 0,
        };

        _ui.SetUiState(ent.Owner, HFRUiKey.Key, state);
    }

    /// <summary>
    ///     Full gas breakdown of a mix for the UI, skipping trace amounts.
    /// </summary>
    private static Dictionary<Gas, float> GetGasBreakdown(GasMixture mix)
    {
        var breakdown = new Dictionary<Gas, float>();
        foreach (var gas in AllGases)
        {
            var moles = mix.GetMoles(gas);
            if (moles > 0.005f)
                breakdown[gas] = moles;
        }

        return breakdown;
    }

    #endregion
}

/// <summary>
///     Pirate-side helper mirroring /tg/ radiation_pulse(), used by the HFR gas
///     reactions (proto-nitrate tritium de-irradiation and BZase). Spawns a
///     short-lived radiation source on a tile, creating a decaying radiation pulse.
/// </summary>
public static class HFRRadiation
{
    public static void Pulse(TileAtmosphere tile, float maxRange)
    {
        var entManager = IoCManager.Resolve<IEntityManager>();

        if (!entManager.TryGetComponent(tile.GridIndex, out MapGridComponent? grid))
            return;

        var coords = grid.GridTileToLocal(tile.GridIndices);
        var radEnt = entManager.SpawnEntity(null, coords);
        var source = entManager.EnsureComponent<RadiationSourceComponent>(radEnt);
        source.Intensity = Math.Max(maxRange * 2f, 50f);
        var despawn = entManager.EnsureComponent<TimedDespawnComponent>(radEnt);
        despawn.Lifetime = 5f;
        // No Dirty() here: RadiationSourceComponent is not networked and Dirty() on it
        // asserts in debug builds (crashes the server). The system reads the field directly. // Pirate
    }
}
