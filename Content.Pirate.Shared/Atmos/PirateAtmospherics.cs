// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;
// ReSharper disable InconsistentNaming

namespace Content.Pirate.Shared.Atmos;

/// <summary>
///     Reaction constants for the /tg/ gases ported by Pirate (code/__DEFINES/reactions.dm).
///     Kept in Pirate content so the core game stays untouched.
/// </summary>
public static class PirateAtmospherics
{
    // Miasma

    /// <summary>Miasma sterilization: converts miasma to oxygen in hot, dry air (&gt;443K, &lt;10% humidity).</summary>
    public const float MiasmaSterilizationTemperature = 443.149f; // FireMinimumTemperatureToExist + 70
    public const float MiasmaSterilizationMaxHumidity = 0.1f;
    public const float MiasmaSterilizationRateBase = 20f;
    public const float MiasmaSterilizationRateScale = 20f;
    public const float MiasmaSterilizationEnergy = 2e-3f;

    // Halon

    /// <summary>Halon oxygen absorption: halon + O2 at &gt;343K removes oxygen, produces pluoxium, cools the air.</summary>
    public const float HalonCombustionEnergy = 2500f;
    public const float HalonCombustionMinTemperature = 343.149f; // T0C + 70
    public const float HalonCombustionTemperatureScale = 3731.49f; // FireMinimumTemperatureToExist * 10

    // Zauker

    /// <summary>Zauker formation from hyper-noblium + nitrium at 50000-75000K.</summary>
    public const float ZaukerFormationMinTemperature = 50000f;
    public const float ZaukerFormationMaxTemperature = 75000f;
    public const float ZaukerFormationTemperatureScale = 5e-6f;
    public const float ZaukerFormationEnergy = 5000f;

    /// <summary>Zauker decomposition when exposed to nitrogen (anti-flood).</summary>
    public const float ZaukerDecompositionMaxRate = 20f;
    public const float ZaukerDecompositionEnergy = 460f;

    // Proto-Nitrate

    /// <summary>Proto-nitrate formation from pluoxium + hydrogen at 5000-10000K.</summary>
    public const float PNFormationMinTemperature = 5000f;
    public const float PNFormationMaxTemperature = 10000f;
    public const float PNFormationTemperatureScale = 5e-3f;
    public const float PNFormationEnergy = 650f;

    /// <summary>Proto-nitrate hydrogen conversion (endothermic, converts H2 to PN).</summary>
    public const float PNHydrogenConversionThreshold = 150f;
    public const float PNHydrogenConversionMaxRate = 5f;
    public const float PNHydrogenConversionEnergy = 2500f;

    /// <summary>Proto-nitrate tritium de-irradiation (exothermic, converts tritium to hydrogen, releases radiation).</summary>
    public const float PNTritiumConversionMinTemperature = 150f;
    public const float PNTritiumConversionMaxTemperature = 340f;
    public const float PNTritiumConversionEnergy = 10000f;
    public const float PNTritiumConversionRadReleaseThreshold = 10000f;
    public const float PNTritiumRadRangeDivisor = 0.5f;
    public const float PNTritiumRadThreshold = 0.3f;

    /// <summary>Proto-nitrate BZase (exothermic, breaks BZ into N2, helium and plasma, releases radiation).</summary>
    public const float PNBzaseMinTemperature = 260f;
    public const float PNBzaseMaxTemperature = 280f;
    public const float PNBzaseEnergy = 60000f;
    public const float PNBzaseRadReleaseThreshold = 60000f;
    public const float PNBzaseRadRangeDivisor = 1.5f;
    public const float PNBzaseRadThreshold = 0.3f;
    public const float PNBzaseNuclearParticleDivisor = 5f;
    public const float PNBzaseNuclearParticleMaximum = 6f;
    public const float PNBzaseNuclearParticleRadiationEnergyConversion = 2.5f;

    // Radiation pulses

    /// <summary>Exponent used to scale radiation release thresholds with mixture volume (/tg/ ATMOS_RADIATION_VOLUME_EXP).</summary>
    public const float AtmosRadiationVolumeExp = 3f;

    /// <summary>Maximum range of a gas-reaction radiation pulse (/tg/ GAS_REACTION_MAXIMUM_RADIATION_PULSE_RANGE).</summary>
    public const float GasReactionMaximumRadiationPulseRange = 20f;
}

/// <summary>
///     Constants for the Hyper-Noblium / Anti-Noblium gas mechanics (ported from /tg/station).
/// </summary>
public static class NobliumAtmospherics
{
    // Hyper-Nobilium

    /// <summary>
    ///     Minimum temperature hyper-noblium can form at (TCMB = 2.7K).
    /// </summary>
    public const float HyperNobliumFormationMinTemperature = 2.7f; // NOBLIUM_FORMATION_MIN_TEMP

    /// <summary>
    ///     Maximum temperature hyper-noblium can form at.
    /// </summary>
    public const float HyperNobliumFormationMaxTemperature = 15f; // NOBLIUM_FORMATION_MAX_TEMP

    /// <summary>
    ///     Amount of energy released when 1 mole of hyper-noblium forms from nitrogen and tritium.
    ///     This is divided by the number of BZ moles present, so without BZ this is extremely explosive!
    /// </summary>
    public const float HyperNobliumFormationEnergy = 2e7f; // NOBLIUM_FORMATION_ENERGY

    /// <summary>
    ///     Minimum moles of hyper-noblium required to suppress other gas reactions.
    /// </summary>
    public const float HyperNobliumSuppressionThreshold = 5f; // REACTION_OPPRESSION_THRESHOLD

    /// <summary>
    ///     Minimum temperature required for hyper-noblium to suppress other gas reactions.
    /// </summary>
    public const float HyperNobliumSuppressionMinTemperature = 20f; // REACTION_OPPRESSION_MIN_TEMP

    // Anti-Nobilium

    /// <summary>
    ///     Divisor for the maximum antinoblium conversion rate.
    ///     1/90 of the antinoblium converts other gases to antinoblium per tick.
    /// </summary>
    public const float AntiNobliumConversionDivisor = 90f; // ANTINOBLIUM_CONVERSION_DIVISOR
}
