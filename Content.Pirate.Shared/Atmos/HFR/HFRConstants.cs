// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos;
using Robust.Shared.Audio;

namespace Content.Pirate.Shared.Atmos.HFR;

/// <summary>
///     All constants for the Hyper-torus Fusion Reactor (HFR).
///     Values ported from /tg/station's HFR with the gas list adapted to
///     the gases available on this server (Tritium acts as hydrogen fuel; Helium exists).
/// </summary>
public static class HFRConstants
{
    // Fusion power level temperature thresholds (fusion mix temperature, Kelvin)
    public const float PowerLevel1MaxTemp = 500f;
    public const float PowerLevel2MaxTemp = 1000f;
    public const float PowerLevel3MaxTemp = 10000f;
    public const float PowerLevel4MaxTemp = 100000f;
    public const float PowerLevel5MaxTemp = 1000000f;
    public const float PowerLevel6MaxTemp = 10000000f;

    /// <summary>Maximum temperature the fusion mix can achieve (per /tg/ FUSION_MAXIMUM_TEMPERATURE).</summary>
    public const float FusionMaxTemperature = 1e8f;

    /// <summary>Cosmic microwave background floor, in Kelvin (per /tg/ TCMB).</summary>
    public const float Tcmb = 2.7f;

    /// <summary>Volume of the fusion mix in liters (per /tg/ core: internal_fusion.volume = 5000).</summary>
    public const float FusionMixVolume = 5000f;

    /// <summary>Volume of the moderator mix in liters (per /tg/ core: moderator_internal.volume = 10000).</summary>
    public const float ModeratorMixVolume = 10000f;

    // --- /tg/ fusion physics constants (code/modules/atmospherics/machinery/components/fusion/_hfr_defines.dm) ---

    /// <summary>Speed of light, in m/s.</summary>
    public const float LightSpeed = 299792458f;

    /// <summary>Calculation between the Planck constant and the lambda of the lightwave.</summary>
    public const float PlanckLightConstant = 2e-16f;

    /// <summary>Calculated radius of H2, based on the amount of atoms in a mole (plus balancing).</summary>
    public const float CalculatedH2Radius = 120e-4f;

    /// <summary>Calculated radius of tritium, based on the amount of atoms in a mole (plus balancing).</summary>
    public const float CalculatedTritRadius = 230e-3f;

    /// <summary>Power conduction in the void, used to calculate the efficiency of the reaction.</summary>
    public const float VoidConduction = 1e-2f;

    /// <summary>Mole count required (per fuel gas) to start a fusion reaction.</summary>
    public const float FusionMoleThreshold = 25f;

    /// <summary>Used to reduce the gas power to a more useful amount.</summary>
    public const float InstabilityGasPowerFactor = 0.003f;

    /// <summary>Used to calculate the toroidal size for the instability.</summary>
    public const float ToroidVolumeBreakeven = 1000f;

    /// <summary>Instability threshold at which the reaction flips endothermic (per /tg/).</summary>
    public const float FusionInstabilityEndothermicity = 4f;

    // Passive charging (QoL): with power on and fuel present the core slowly warms
    // on its own below PL1 so a freshly fueled reactor self-ignites, and the energy
    // metric never drains to 0 on a fueled core.
    public const float PassiveChargeRatePerMole = 0.008f; // K/s per mole of fuel
    public const float PassiveChargeMaxRate = 10f; // K/s cap (below PL1 only)
    // Energy floor = 100% of the scaled fuel: as the reactor burns, the main
    // byproduct (e.g. CO2) accumulates in the fusion mix and drags energyModifiers
    // down through "fuel + fuel - byproduct". At ~400+ moles of fuel that used to
    // collapse the Energy readout. With a full-strength fuel floor the metric always
    // grows with fuel and temperature instead of "discharging".
    public const float PassiveEnergyFloorFactor = 1f;

    /// <summary>Conduction of heat inside the fusion reactor.</summary>
    public const float MetallicVoidConductivity = 0.38f;

    /// <summary>Conduction of heat near the external cooling loop.</summary>
    public const float HighEfficiencyConductivity = 0.975f;

    /// <summary>
    ///     Passive shutdown cooling: how fast the core sheds residual heat to the
    ///     environment when the reaction is dead (no fuel / power off). ~5% of the
    ///     temperature delta per second — a hot, unfueled reactor cools to room
    ///     temperature in about a minute, so the power switch (locked while hot)
    ///     unlocks again instead of the reactor reading as "running" forever.
    /// </summary>
    public const float PassiveCoolingConductivity = 0.05f;

    // Key parameters: F = FIR * 0.01 * 5 * power_level, clamped to 0.05-30 (per /tg/).
    public const float FuelInjectionMultiplier = 5f;
    public const float FuelInjectionMinF = 0.05f;
    public const float FuelInjectionMaxF = 30f;

    /// <summary>
    ///     Server balance: fuel is burned this many times faster than /tg/'s
    ///     consumption_amount * 0.85 formula, so the fusion mix doesn't flood
    ///     (injection runs at up to FIR=150 mol/s while /tg/ consumption caps
    ///     at 30*0.85 = 25.5 mol/s per gas). Doubling consumption keeps the
    ///     mix leaner without touching heat output or gas production.
    /// </summary>
    public const float FuelConsumptionMultiplier = 2f;

    /// <summary>
    ///     Per-power-level fuel burn factor (server balance). F itself scales
    ///     linearly with power level (per /tg/), so PL3-6 would otherwise gulp
    ///     fuel 3-4x faster than PL1-2 (at FIR=150: ~38 mol/s at PL3, ~51 at
    ///     PL4+). These factors soften the burn at high levels — PL3-4 land at
    ///     ~23 mol/s and PL5-6 fall further, so higher levels no longer drain
    ///     the fusion mix faster than the injectors can refill it.
    ///     Index = power level (0-6).
    /// </summary>
    public static readonly float[] FuelConsumptionPowerLevelFactor =
    [
        1f,    // PL0 (no reaction)
        1f,    // PL1
        1f,    // PL2
        0.6f,  // PL3
        0.45f, // PL4
        0.35f, // PL5
        0.3f,  // PL6
    ];

    // Anti-Noblium production (per /tg/ moderator_common_process):
    // dirty_production_rate = scaled main byproduct / fuel injection rate.
    // PL5: Output += dirty * 0.9/0.065 (below 1e7 K, or with plasma+BZ in the moderator).
    // PL6: Output += clamp(dirty / 0.045, 0, 10) when BZ is present; Fusion += dirty * 0.01/0.095.
    public const float AntiNobliumOutputRate = 0.9f / 0.065f;
    public const float AntiNobliumBzRate = 1f / 0.045f;
    public const float AntiNobliumBzMaxPerSecond = 10f;
    public const float AntiNobliumFusionRate = 0.01f / 0.095f;

    /// <summary>0.05% of all moderator gas is lost per second per power level.</summary>
    public const float ModeratorLossPerPowerLevel = 0.0005f;

    // Integrity & iron content
    public const float MaxIntegrity = 100f;
    public const float BrokenSpriteIntegrityThreshold = 25f; // show cracked sprite below this
    public const float MaxIntegrityDamagePerTick = 0.5f; // at most 0.5% integrity (4.5 damage) lost per tick
    public const float IronContentDamagePerSecond = 0.5f; // 0-300 scale (x100 of /tg/'s 0-1)

    // /tg/ defines: IRON_CHANCE_PER_FUSION_LEVEL = 17 (PL5 ~85%/s, PL6 100%/s),
    // and moderator oxygen >150 mol burns iron away (moderator_common_process).
    public const float IronChancePerFusionLevel = 17f;
    /// <summary>Iron removed per second while O2 > 150 mol is in the moderator (0-300 scale).</summary>
    public const float IronOxygenHealPerSecond = IronContentDamagePerSecond * (100f - IronChancePerFusionLevel) / 100f;
    /// <summary>Moles of moderator oxygen consumed per iron point removed (per /tg/ 10 / heal).</summary>
    public const float OxygenMolesConsumedPerIronHeal = 10f / IronOxygenHealPerSecond;

    // Meltdown (per /tg/ process_damageheal + countdown + meltdown procs)
    /// <summary>Integrity (0-100) at which the reactor begins melting down.</summary>
    public const float MeltdownIntegrityThreshold = 5f; // /tg/ HYPERTORUS_MELTING_PERCENT
    /// <summary>Seconds of countdown before the meltdown explosion (per /tg/ HYPERTORUS_COUNTDOWN_TIME).</summary>
    public const float MeltdownCountdownTime = 30f;
    /// <summary>
    ///     Minimum seconds between UI state pushes while the reactor runs. Without
    ///     this the server rebuilds the client window every atmos tick (up to 20x/s),
    ///     which stutters the client near the reactor. 4 Hz is smooth enough for the
    ///     temperature chart and gas bars; switch/recipe changes still push instantly.
    /// </summary>
    public const float UiUpdateInterval = 0.25f;

    /// <summary>Seconds between radio countdown messages (per /tg/ 5-second interval).</summary>
    public const float MeltdownRadioInterval = 5f;
    /// <summary>Radio channel used for meltdown warnings.</summary>
    public const string MeltdownRadioChannel = "Engineering";
    /// <summary>
    ///     Looping siren played while the meltdown countdown runs
    ///     (the air-raid siren used by the supermatter cascade, instead of the
    ///     nuclear bomb alarm).
    /// </summary>
    public static readonly SoundSpecifier MeltdownSirenSound =
        new SoundPathSpecifier("/Audio/_Pirate/Machines/HFR/airraid.ogg");

    /// <summary>
    ///     One-shot critical explosion warning, played at the start of the countdown
    ///     and again 10 seconds before the meltdown (per /tg/ countdown(), which
    ///     plays sound/machines/hypertorus/HFR_critical_explosion.ogg at 10 seconds).
    /// </summary>
    public static readonly SoundSpecifier MeltdownCriticalSound =
        new SoundPathSpecifier("/Audio/_Pirate/Machines/HFR/HFR_critical_explosion.wav");

    /// <summary>Seconds before the meltdown at which the critical warning sound plays again.</summary>
    public const float MeltdownCriticalSoundAt = 10f;
    /// <summary>Healium heals the core when integrity drops below this (0-100 scale).
    /// /tg/ uses critical_threshold_proximity > 400 with melting_point 900 -> ~56%.</summary>
    public const float HealiumIntegrityThreshold = 56f;
    public const float HealiumIntegrityRestorePerHundredMoles = 0.11f;
    public const float HealiumConsumptionFactor = 20f; // consumed x20P

    // Waste removal
    public const float WasteRemovalByproductFraction = 0.5f; // 50% of byproducts
    public const float WasteRemovalAntiNobliumFraction = 0.05f; // 5% of fusion Anti-Noblium

    /// <summary>Forcibly disabled waste removal at this power level.</summary>
    public const int WasteRemovalForceOffPowerLevel = 6;
}

/// <summary>
///     Selectable HFR recipes, matching the /tg/ hfr_fuels list.
/// </summary>
public enum HfrRecipe : byte
{
    /// <summary>Plasma + Oxygen -> CO2 (+H2O). Produces Frezon, N2O, Pluoxium at higher tiers.</summary>
    PlasmaOxygen = 0,

    /// <summary>Tritium + Oxygen -> Pluoxium + Helium (per /tg/).</summary>
    TritiumOxygen = 1,

    /// <summary>Hyper-Noblium + Tritium -> Anti-Noblium. The key recipe for Anti-Noblium production.</summary>
    HyperNobliumTritium = 2,

    /// <summary>Hydrogen + Oxygen -> Helium + Nitrogen. Classic water-splitting fusion (per /tg/).</summary>
    HydrogenOxygen = 3,

    /// <summary>Hydrogen + Tritium -> Helium. The classic thermonuclear fuel (per /tg/).</summary>
    HydrogenTritium = 4,

    /// <summary>Hyper-Noblium + Hydrogen -> Anti-Noblium + Helium + Proto-Nitrate + Zauker (per /tg/).</summary>
    HyperNobliumHydrogen = 5,

    /// <summary>Hyper-Noblium + Anti-Noblium -> Helium + top-tier gases. Highest recipe, with CRITICAL MELTDOWN (per /tg/).</summary>
    HyperNobliumAntiNoblium = 6,
}

/// <summary>
///     Meltdown behaviour flags, ported from /tg/ HYPERTORUS_FLAG_* defines.
/// </summary>
[Flags]
public enum HFRMeltdownFlags
{
    None = 0,
    BaseExplosion = 1 << 0, // flash = PL*3, light = PL*2
    MediumExplosion = 1 << 1, // flash = PL*6, light = PL*5, heavy = PL*0.5
    DevastatingExplosion = 1 << 2, // flash = PL*8, light = PL*7, heavy = PL*2, devastation = PL
    RadiationPulse = 1 << 3,
    Emp = 1 << 4,
    MinimumSpread = 1 << 5, // emp light = PL*3, heavy = PL*1; rad = 2*PL+8; pockets 5, spread PL*2
    MediumSpread = 1 << 6, // emp light = PL*5, heavy = PL*3; rad = PL+24; pockets 7, spread PL*4
    BigSpread = 1 << 7, // emp light = PL*7, heavy = PL*5; rad = PL+34; pockets 10, spread PL*6
    MassiveSpread = 1 << 8, // emp light = PL*9, heavy = PL*7; rad = PL+44; pockets 15, spread PL*8
    CriticalMeltdown = 1 << 9, // doubles devastation + heavy radii, station-wide warning
}

/// <summary>
///     Data for a single HFR recipe.
/// </summary>
public sealed record HfrRecipeData(
    Gas PrimaryFuel,
    Gas SecondaryFuel,
    Gas MainByproduct,
    Gas? OtherByproduct,
    Gas? Tier1,
    Gas? Tier2,
    Gas? Tier3,
    Gas? Tier4,
    Gas? Tier5,
    Gas? Tier6,
    float CoolingModifier,
    float HeatingModifier,
    float EnergyModifier,
    float FuelConsumptionModifier,
    float GasProductionModifier,
    float MaxTemperatureModifier,
    HFRMeltdownFlags MeltdownFlags);

/// <summary>
///     All HFR recipes, indexed by <see cref="HfrRecipe"/>.
///     Modifiers ported from /tg/station (see the HFR wiki guide).
///     Note: all GasProductionModifier values are halved vs /tg/ so the reactor
///     doesn't flood the output with gases (server balance).
/// </summary>
public static class HfrRecipes
{
    static HfrRecipes()
    {
        // All must have one entry per HfrRecipe value (no sentinel is defined
        // today, but if one is ever added this fails loudly instead of letting
        // the reactor index out of bounds at runtime). // Pirate
        var recipeCount = Enum.GetValues<HfrRecipe>().Length;
        if (All.Length != recipeCount)
            throw new InvalidOperationException(
                $"HfrRecipes.All contains {All.Length} entries but HfrRecipe defines {recipeCount} values; fix the recipe table or the enum.");
    }

    public static readonly HfrRecipeData[] All =
    [
        // Plasma + Oxygen: main CO2, other H2O; tiers CO2/H2O/Frezon/N2O/Pluoxium/Halon (per /tg/).
        // GasProductionModifier halved (1.4 -> 0.7) from /tg/ for server balance.
        // Meltdown: /tg/ plasma_oxy_fuel = BASE_EXPLOSION | MINIMUM_SPREAD.
        new(
            Gas.Plasma, Gas.Oxygen, Gas.CarbonDioxide, Gas.WaterVapor,
            Gas.CarbonDioxide, Gas.WaterVapor, Gas.Frezon, Gas.NitrousOxide, Gas.Pluoxium, Gas.Halon,
            2.5f, 0.1f, 10f, 3.3f, 0.7f, 0.6f,
            HFRMeltdownFlags.BaseExplosion | HFRMeltdownFlags.MinimumSpread),

        // Tritium + Oxygen: main Pluoxium, other Helium; tiers Helium/Plasma/Oxygen/Nitrogen/BZ/HyperNoblium.
        // Modifiers per /tg/ tritium_oxy_fuel: neg=2.1, pos=0.5, energy=2, consumption=1.2, production=0.8, temp=0.8.
        // GasProductionModifier halved (0.8 -> 0.4) from /tg/ for server balance.
        // Meltdown: /tg/ tritium_oxy_fuel = BASE_EXPLOSION | RADIATION_PULSE | MEDIUM_SPREAD.
        new(
            Gas.Tritium, Gas.Oxygen, Gas.Pluoxium, Gas.Helium,
            Gas.Helium, Gas.Plasma, Gas.Oxygen, Gas.Nitrogen, Gas.BZ, Gas.HyperNoblium,
            2.1f, 0.5f, 2f, 1.2f, 0.4f, 0.8f,
            HFRMeltdownFlags.BaseExplosion | HFRMeltdownFlags.RadiationPulse | HFRMeltdownFlags.MediumSpread),

        // Hyper-Noblium + Tritium: main Anti-Noblium; tiers Anti-Noblium/Helium/Proto-Nitrate/Zauker/Healium/Miasma (per /tg/).
        // GasProductionModifier halved (1.7 -> 0.85) from /tg/ for server balance.
        // Meltdown: /tg/ hypernoblium_tritium_fuel = DEVASTATING | RADIATION_PULSE | EMP | BIG_SPREAD.
        new(
            Gas.HyperNoblium, Gas.Tritium, Gas.AntiNoblium, null,
            Gas.AntiNoblium, Gas.Helium, Gas.ProtoNitrate, Gas.Zauker, Gas.Healium, Gas.Miasma,
            0.1f, 2.5f, 0.1f, 0.45f, 0.85f, 0.95f,
            HFRMeltdownFlags.DevastatingExplosion | HFRMeltdownFlags.RadiationPulse | HFRMeltdownFlags.Emp | HFRMeltdownFlags.BigSpread),

        // Hydrogen + Oxygen: main Helium, other Nitrogen; tiers Helium/Plasma/Oxygen/Nitrogen/BZ/HyperNoblium (per /tg/).
        // GasProductionModifier halved (0.9 -> 0.45) from /tg/ for server balance.
        // Meltdown: /tg/ hydrogen_oxy_fuel = BASE_EXPLOSION | EMP | MEDIUM_SPREAD.
        new(
            Gas.Hydrogen, Gas.Oxygen, Gas.Helium, Gas.Nitrogen,
            Gas.Helium, Gas.Plasma, Gas.Oxygen, Gas.Nitrogen, Gas.BZ, Gas.HyperNoblium,
            2f, 0.6f, 3f, 1.1f, 0.45f, 0.75f,
            HFRMeltdownFlags.BaseExplosion | HFRMeltdownFlags.Emp | HFRMeltdownFlags.MediumSpread),

        // Hydrogen + Tritium: main Helium; tiers Helium/Plasma/Oxygen/Nitrogen/BZ/HyperNoblium (per /tg/).
        // GasProductionModifier halved (1 -> 0.5) from /tg/ for server balance.
        // Meltdown: /tg/ hydrogen_tritium_fuel = MEDIUM_EXPLOSION | RADIATION_PULSE | EMP | MEDIUM_SPREAD.
        new(
            Gas.Hydrogen, Gas.Tritium, Gas.Helium, null,
            Gas.Helium, Gas.Plasma, Gas.Oxygen, Gas.Nitrogen, Gas.BZ, Gas.HyperNoblium,
            1f, 1f, 1f, 1f, 0.5f, 0.85f,
            HFRMeltdownFlags.MediumExplosion | HFRMeltdownFlags.RadiationPulse | HFRMeltdownFlags.Emp | HFRMeltdownFlags.MediumSpread),

        // Hyper-Noblium + Hydrogen: main Anti-Noblium; tiers Anti-Noblium/Helium/Proto-Nitrate/Zauker/Healium/Miasma (per /tg/).
        // GasProductionModifier halved (1.4 -> 0.7) from /tg/ for server balance.
        // Meltdown: /tg/ hypernob_hydrogen_fuel = DEVASTATING | RADIATION_PULSE | EMP | BIG_SPREAD.
        new(
            Gas.HyperNoblium, Gas.Hydrogen, Gas.AntiNoblium, null,
            Gas.AntiNoblium, Gas.Helium, Gas.ProtoNitrate, Gas.Zauker, Gas.Healium, Gas.Miasma,
            0.2f, 2.2f, 0.2f, 0.55f, 0.7f, 0.9f,
            HFRMeltdownFlags.DevastatingExplosion | HFRMeltdownFlags.RadiationPulse | HFRMeltdownFlags.Emp | HFRMeltdownFlags.BigSpread),

        // Hyper-Noblium + Anti-Noblium: main Helium; tiers Plasma/Oxygen/Nitrogen/Proto-Nitrate/Nitrium/Miasma (per /tg/).
        // GasProductionModifier halved (3 -> 1.5) from /tg/ for server balance.
        // Meltdown: /tg/ hypernob_antinob_fuel = DEVASTATING | RADIATION_PULSE | EMP | MASSIVE_SPREAD | CRITICAL_MELTDOWN.
        new(
            Gas.HyperNoblium, Gas.AntiNoblium, Gas.Helium, null,
            Gas.Plasma, Gas.Oxygen, Gas.Nitrogen, Gas.ProtoNitrate, Gas.Nitrium, Gas.Miasma,
            0.01f, 3.5f, 2f, 0.01f, 1.5f, 1f,
            HFRMeltdownFlags.DevastatingExplosion | HFRMeltdownFlags.RadiationPulse | HFRMeltdownFlags.Emp | HFRMeltdownFlags.MassiveSpread | HFRMeltdownFlags.CriticalMeltdown),
    ];

    /// <summary>
    ///     Per-recipe tier output gases (Tier1..Tier6), precomputed so the per-tick
    ///     recipe processing doesn't allocate a new array every update.
    /// </summary>
    public static readonly Gas?[][] Tiers = BuildTiers();

    private static Gas?[][] BuildTiers()
    {
        var tiers = new Gas?[All.Length][];
        for (var i = 0; i < All.Length; i++)
        {
            var r = All[i];
            tiers[i] = [r.Tier1, r.Tier2, r.Tier3, r.Tier4, r.Tier5, r.Tier6];
        }

        return tiers;
    }
}
