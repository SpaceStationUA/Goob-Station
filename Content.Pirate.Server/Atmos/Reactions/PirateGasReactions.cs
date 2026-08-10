// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Atmos.HFR;
using Content.Pirate.Shared.Atmos;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Pirate.Server.Atmos.Reactions;

/// <summary>
///     From /tg/ gas_mixture.react(): Hyper-Nobilium suppresses ALL other gas
///     reactions when it is present in sufficient quantity and the mix is warm.
///     Runs at high priority (see the <c>priority</c> field in reactions.yml) so it
///     is evaluated before any other reaction, then returns <see cref="ReactionResult.StopReactions"/>
///     which breaks the reaction loop.
/// </summary>
[UsedImplicitly]
public sealed partial class HyperNobliumSuppressionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (mixture.GetMoles(Gas.HyperNoblium) < NobliumAtmospherics.HyperNobliumSuppressionThreshold)
            return ReactionResult.NoReaction;

        if (mixture.Temperature <= NobliumAtmospherics.HyperNobliumSuppressionMinTemperature)
            return ReactionResult.NoReaction;

        return ReactionResult.StopReactions;
    }
}

/// <summary>
///     From /tg/ gases
///     Hyper-Noblium Condensation: forms Hyper-Noblium from Nitrogen and Tritium at extremely low temperatures (2.7-15K).
///     Extremely exothermic. BZ acts as a catalyst to reduce the energy release.
///     10 moles of Nitrogen are consumed per mole of Hyper-Noblium synthesized.
///     5 moles of Tritium is the minimum required, but BZ reduces Tritium consumption.
///     The energy released = nob_formed * HYPERNOBLIUM_FORMATION_ENERGY / max(bz_moles, 1)
///     Without BZ, this is extremely explosive!
/// </summary>
[UsedImplicitly]
public sealed partial class HyperNobliumFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var initNitrogen = mixture.GetMoles(Gas.Nitrogen);
        var initTritium = mixture.GetMoles(Gas.Tritium);
        var initBZ = mixture.GetMoles(Gas.BZ);

        // Minimum requirements: 10 N2, 5 Tritium
        if (initNitrogen < 10f || initTritium < 5f)
            return ReactionResult.NoReaction;

        // BZ acts as a catalyst — reduces tritium consumption proportionally
        // reduction_factor = tritium / (tritium + bz), clamped between 0.001 and 1
        var reductionFactor = Math.Clamp(initTritium / (initTritium + initBZ), 0.001f, 1f);

        // Hyper-Noblium formed is limited by both reactants
        // Uses 10 N2 and 5 * reduction_factor Tritium per Noblium
        var nobFormed = Math.Min((initNitrogen + initTritium) * 0.01f,
            Math.Min(initTritium / (5f * reductionFactor), initNitrogen / 10f));

        // Check we won't go negative
        var tritiumConsumed = 5f * nobFormed * reductionFactor;
        var nitrogenConsumed = 10f * nobFormed;

        if (nobFormed <= 0f || initTritium - tritiumConsumed < 0f || initNitrogen - nitrogenConsumed < 0f)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Tritium, -tritiumConsumed);
        mixture.AdjustMoles(Gas.Nitrogen, -nitrogenConsumed);
        mixture.AdjustMoles(Gas.HyperNoblium, nobFormed);

        // Energy released: NOBLIUM_FORMATION_ENERGY / max(bz_moles, 1)
        // Without BZ this is EXTREMELY energetic — 2e7 J per mole!
        var energyReleased = nobFormed * NobliumAtmospherics.HyperNobliumFormationEnergy / Math.Max(initBZ, 1f);

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gases
///     Anti-Noblium Replication: breaks down all other gases into more Anti-Noblium.
///     Converts all gases proportionally to Anti-Noblium at a rate of antinoblium_moles / 90 per tick.
///     Temperature is conserved (adiabatic process).
///     When total non-antinoblium moles are below the minimum threshold, clears them all at once.
/// </summary>
[UsedImplicitly]
public sealed partial class AntiNobliumReplicationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var heatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var totalMoles = mixture.TotalMoles;
        var antiNobliumMoles = mixture.GetMoles(Gas.AntiNoblium);
        var totalNotAntiNoblium = totalMoles - antiNobliumMoles;

        // Minimum amount of Anti-Noblium required to sustain the reaction
        if (antiNobliumMoles < 0.25f)
            return ReactionResult.NoReaction;

        // If there's nothing left to convert, stop
        if (totalNotAntiNoblium < Atmospherics.GasMinMoles)
            return ReactionResult.NoReaction;

        // Calculate reaction rate: antinoblium_moles / 90
        var reactionRate = Math.Min(antiNobliumMoles / NobliumAtmospherics.AntiNobliumConversionDivisor, totalNotAntiNoblium);

        // Near-zero: clear remaining gases
        var clearRemaining = totalNotAntiNoblium < Atmospherics.MinimumMolesDeltaToMove;

        float converted = 0f;

        // Iterate over all gases and convert proportionally
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var gas = (Gas)i;
            if (gas == Gas.AntiNoblium)
                continue;

            var moles = mixture.GetMoles(gas);
            if (moles <= 0f)
                continue;

            if (clearRemaining)
            {
                // Clear all remaining non-antinoblium gases
                mixture.SetMoles(gas, 0f);
                converted += moles;
            }
            else
            {
                // Convert proportionally
                var toConvert = reactionRate * moles / totalNotAntiNoblium;
                if (toConvert > 0f)
                {
                    mixture.AdjustMoles(gas, -toConvert);
                    converted += toConvert;
                }
            }
        }

        if (converted > 0f)
            mixture.AdjustMoles(Gas.AntiNoblium, converted);

        // Temperature is conserved (adiabatic): T * heatCapacity_old / heatCapacity_new
        // This is handled automatically by the mixture system, but we ensure consistency
        // by recalculating temperature if heat capacity changed significantly
        var newHeatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCap > Atmospherics.MinimumHeatCapacity && heatCapacity > Atmospherics.MinimumHeatCapacity)
        {
            mixture.Temperature = Math.Max(mixture.Temperature * heatCapacity / newHeatCap, Atmospherics.TCMB);
        }

        return converted > 0f ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }
}

/// <summary>
///     From /tg/ gases
///     Hydrogen Combustion: H2 + 0.5 O2 → H2O. Highly exothermic.
///     burned_fuel = min(H2 / FIRE_HYDROGEN_BURN_RATE_DELTA,
///                       O2 / (FIRE_HYDROGEN_BURN_RATE_DELTA * HYDROGEN_OXYGEN_FULLBURN),
///                       H2, O2 * 2)
///     Creates hotspots (fires) and releases FIRE_HYDROGEN_ENERGY_RELEASED J per mole of H2.
/// </summary>
[UsedImplicitly]
public sealed partial class HydrogenFireReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var energyReleased = 0f;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;

        var initialHydrogen = mixture.GetMoles(Gas.Hydrogen);
        var initialOxygen = mixture.GetMoles(Gas.Oxygen);

        // /tg/ h2fire: min(H2 / 2, O2 / (2 * 10), H2, O2 * 2)
        var burnedFuel = Math.Min(initialHydrogen / 2f, initialOxygen / (2f * 10f));
        burnedFuel = Math.Min(burnedFuel, initialHydrogen);
        burnedFuel = Math.Min(burnedFuel, initialOxygen * 2f);

        if (burnedFuel > 0f)
        {
            mixture.AdjustMoles(Gas.Hydrogen, -burnedFuel);
            mixture.AdjustMoles(Gas.Oxygen, -burnedFuel * 0.5f);
            mixture.AdjustMoles(Gas.WaterVapor, burnedFuel);

            energyReleased += Atmospherics.FireHydrogenEnergyReleased * burnedFuel;
            mixture.ReactionResults[(byte)GasReaction.Fire] += burnedFuel;
        }

        energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
        if (energyReleased > 0)
        {
            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
        }

        if (location != null)
        {
            temperature = mixture.Temperature;
            if (temperature > Atmospherics.FireMinimumTemperatureToExist)
                atmosphereSystem.HotspotExpose(location, temperature, mixture.Volume);
        }

        return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (halon_o2removal): Halon Oxygen Absorption.
///     A potent fire suppressant — above 343K it consumes a large amount of oxygen
///     (20 mol O2 per mol halon) relative to the halon used, produces pluoxium and
///     is endothermic, so it both starves and cools fires.
/// </summary>
[UsedImplicitly]
public sealed partial class HalonOxygenAbsorptionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < PirateAtmospherics.HalonCombustionMinTemperature)
            return ReactionResult.NoReaction;

        var halon = mixture.GetMoles(Gas.Halon);
        var oxygen = mixture.GetMoles(Gas.Oxygen);
        if (halon <= 0f || oxygen <= 0f)
            return ReactionResult.NoReaction;

        var heatEfficiency = Math.Min(temperature / PirateAtmospherics.HalonCombustionTemperatureScale,
            Math.Min(halon, oxygen * (1f / 20f)));
        if (heatEfficiency <= 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.Halon, -heatEfficiency);
        mixture.AdjustMoles(Gas.Oxygen, -heatEfficiency * 20f);
        mixture.AdjustMoles(Gas.Pluoxium, heatEfficiency * 2.5f);

        // Endothermic: absorbs heat, cooling the fire down.
        var energyUsed = heatEfficiency * PirateAtmospherics.HalonCombustionEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity - energyUsed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (miaster): dry heat sterilization.
///     Pathogens cannot survive in a hot, dry environment — miasma decomposes
///     into oxygen above 443K as long as the air is less than 10% humid.
/// </summary>
[UsedImplicitly]
public sealed partial class MiasmaSterilizationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var miasma = mixture.GetMoles(Gas.Miasma);
        if (miasma <= 0f)
            return ReactionResult.NoReaction;

        var temperature = mixture.Temperature;
        if (temperature < PirateAtmospherics.MiasmaSterilizationTemperature)
            return ReactionResult.NoReaction;

        // Needs to be dry: water vapor may not exceed 10% of the mixture.
        var total = mixture.TotalMoles;
        if (total > 0f && mixture.GetMoles(Gas.WaterVapor) / total > PirateAtmospherics.MiasmaSterilizationMaxHumidity)
            return ReactionResult.NoReaction;

        // Replace miasma with oxygen. The hotter it is, the faster it sterilizes.
        var cleaned = Math.Min(miasma,
            PirateAtmospherics.MiasmaSterilizationRateBase
            + (temperature - PirateAtmospherics.MiasmaSterilizationTemperature) / PirateAtmospherics.MiasmaSterilizationRateScale);

        mixture.AdjustMoles(Gas.Miasma, -cleaned);
        mixture.AdjustMoles(Gas.Oxygen, cleaned);

        // A tiny bit of extra heat from the maillard reaction.
        // /tg/ adds this directly to the temperature (not via heat capacity):
        // air.temperature += cleaned_air * MIASTER_STERILIZATION_ENERGY
        mixture.Temperature += cleaned * PirateAtmospherics.MiasmaSterilizationEnergy;

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (proto_nitrate_formation): Proto-Nitrate Formation.
///     Production of proto-nitrate from pluoxium and hydrogen under high temperatures
///     (5000-10000K). Exothermic. 2 hydrogen + 0.2 pluoxium per 2.2 mol of PN.
/// </summary>
[UsedImplicitly]
public sealed partial class ProtoNitrateFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < PirateAtmospherics.PNFormationMinTemperature || temperature > PirateAtmospherics.PNFormationMaxTemperature)
            return ReactionResult.NoReaction;

        var pluoxium = mixture.GetMoles(Gas.Pluoxium);
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);
        if (pluoxium <= 0f || hydrogen <= 0f)
            return ReactionResult.NoReaction;

        var heatEfficiency = Math.Min(temperature * PirateAtmospherics.PNFormationTemperatureScale,
            Math.Min(pluoxium / 0.2f, hydrogen / 2f));
        if (heatEfficiency <= 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.Hydrogen, -heatEfficiency * 2f);
        mixture.AdjustMoles(Gas.Pluoxium, -heatEfficiency * 0.2f);
        mixture.AdjustMoles(Gas.ProtoNitrate, heatEfficiency * 2.2f);

        // Exothermic.
        var energyReleased = heatEfficiency * PirateAtmospherics.PNFormationEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (proto_nitrate_hydrogen_response): Proto-Nitrate
///     Hydrogen Conversion. Converts hydrogen into proto-nitrate. Endothermic.
///     Only fires while more than 150 mol of hydrogen is present, capped at 5 mol/s.
/// </summary>
[UsedImplicitly]
public sealed partial class ProtoNitrateHydrogenResponseReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var hydrogen = mixture.GetMoles(Gas.Hydrogen);
        if (hydrogen < PirateAtmospherics.PNHydrogenConversionThreshold || protoNitrate <= 0f)
            return ReactionResult.NoReaction;

        var produced = Math.Min(PirateAtmospherics.PNHydrogenConversionMaxRate, Math.Min(hydrogen, protoNitrate));
        if (produced <= 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.Hydrogen, -produced);
        mixture.AdjustMoles(Gas.ProtoNitrate, produced * 0.5f);

        // Endothermic.
        var energyUsed = produced * PirateAtmospherics.PNHydrogenConversionEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity - energyUsed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (proto_nitrate_bz_response): Proto-Nitrate BZase.
///     Breaks BZ down into nitrogen, helium, and plasma in the presence of proto-nitrate
///     at low temperatures (260-280K). Exothermic. Releases a radiation pulse.
/// </summary>
[UsedImplicitly]
public sealed partial class ProtoNitrateBzaseReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < PirateAtmospherics.PNBzaseMinTemperature || temperature > PirateAtmospherics.PNBzaseMaxTemperature)
            return ReactionResult.NoReaction;

        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var bz = mixture.GetMoles(Gas.BZ);
        if (protoNitrate <= 0f || bz <= 0f)
            return ReactionResult.NoReaction;

        var consumed = Math.Min(temperature / 2240f * bz * protoNitrate / (bz + protoNitrate), Math.Min(bz, protoNitrate));
        if (consumed <= 0f || bz - consumed < 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.BZ, -consumed);
        mixture.AdjustMoles(Gas.Nitrogen, consumed * 0.4f);
        mixture.AdjustMoles(Gas.Helium, consumed * 1.6f);
        mixture.AdjustMoles(Gas.Plasma, consumed * 0.8f);

        // Exothermic.
        var energyReleased = consumed * PirateAtmospherics.PNBzaseEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        // Radiation pulse (per /tg/ radiation_pulse). The nuclear-particle and
        // hallucination effects of /tg/ have no equivalent systems here, so only
        // the radiation pulse is ported.
        var volumeScale = MathF.Pow(mixture.Volume / Atmospherics.CellVolume, PirateAtmospherics.AtmosRadiationVolumeExp);
        if (energyReleased > PirateAtmospherics.PNBzaseRadReleaseThreshold * volumeScale)
        {
            var maxRange = MathF.Min(MathF.Sqrt(consumed) / PirateAtmospherics.PNBzaseRadRangeDivisor, PirateAtmospherics.GasReactionMaximumRadiationPulseRange);
            if (holder is TileAtmosphere tile)
                HFRRadiation.Pulse(tile, maxRange);
        }

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (proto_nitrate_tritium_response): Proto-Nitrate
///     Tritium De-irradiation. Converts tritium into hydrogen while consuming a small
///     amount of proto-nitrate. Exothermic. Releases a radiation pulse at high output.
///     Only fires between 150K and 340K.
/// </summary>
[UsedImplicitly]
public sealed partial class ProtoNitrateTritiumResponseReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < PirateAtmospherics.PNTritiumConversionMinTemperature || temperature > PirateAtmospherics.PNTritiumConversionMaxTemperature)
            return ReactionResult.NoReaction;

        var protoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var tritium = mixture.GetMoles(Gas.Tritium);
        if (protoNitrate <= 0f || tritium <= 0f)
            return ReactionResult.NoReaction;

        var produced = Math.Min(temperature / 34f * (tritium * protoNitrate) / (tritium + 10f * protoNitrate),
            Math.Min(tritium, protoNitrate * 100f));
        if (produced <= 0f || tritium - produced < 0f || protoNitrate - produced * 0.01f < 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.ProtoNitrate, -produced * 0.01f);
        mixture.AdjustMoles(Gas.Tritium, -produced);
        mixture.AdjustMoles(Gas.Hydrogen, produced);

        // Exothermic.
        var energyReleased = produced * PirateAtmospherics.PNTritiumConversionEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        // Radiation pulse (per /tg/ radiation_pulse).
        var volumeScale = MathF.Pow(mixture.Volume / Atmospherics.CellVolume, PirateAtmospherics.AtmosRadiationVolumeExp);
        if (energyReleased > PirateAtmospherics.PNTritiumConversionRadReleaseThreshold * volumeScale)
        {
            var maxRange = MathF.Min(MathF.Sqrt(produced) / PirateAtmospherics.PNTritiumRadRangeDivisor, PirateAtmospherics.GasReactionMaximumRadiationPulseRange);
            if (holder is TileAtmosphere tile)
                HFRRadiation.Pulse(tile, maxRange);
        }

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (zauker_formation): Zauker Formation.
///     Production of zauker using hyper-noblium and nitrium under very high
///     temperatures (50000-75000K). Endothermic (consumes heat, like /tg/).
///     0.01 hyper-noblium + 0.5 nitrium per mol of zauker produced.
/// </summary>
[UsedImplicitly]
public sealed partial class ZaukerFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var temperature = mixture.Temperature;
        if (temperature < PirateAtmospherics.ZaukerFormationMinTemperature || temperature > PirateAtmospherics.ZaukerFormationMaxTemperature)
            return ReactionResult.NoReaction;

        var hypernoblium = mixture.GetMoles(Gas.HyperNoblium);
        var nitrium = mixture.GetMoles(Gas.Nitrium);
        if (hypernoblium <= 0f || nitrium <= 0f)
            return ReactionResult.NoReaction;

        var heatEfficiency = Math.Min(temperature * PirateAtmospherics.ZaukerFormationTemperatureScale,
            Math.Min(hypernoblium / 0.01f, nitrium / 0.5f));
        if (heatEfficiency <= 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.HyperNoblium, -heatEfficiency * 0.01f);
        mixture.AdjustMoles(Gas.Nitrium, -heatEfficiency * 0.5f);
        mixture.AdjustMoles(Gas.Zauker, heatEfficiency * 0.5f);

        // Endothermic (matches /tg/ zauker_formation: energy is subtracted).
        var energyUsed = heatEfficiency * PirateAtmospherics.ZaukerFormationEnergy;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity - energyUsed) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}

/// <summary>
///     From /tg/ gas_reactions.dm (zauker_decomp): Zauker Decomposition.
///     Occurs in the presence of nitrogen to prevent zauker floods.
///     Exothermic. Each mol of zauker decomposes into 0.3 oxygen and 0.7 nitrogen,
///     capped at 20 mol/s.
/// </summary>
[UsedImplicitly]
public sealed partial class ZaukerDecompositionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);
        var zauker = mixture.GetMoles(Gas.Zauker);
        var burned = Math.Min(PirateAtmospherics.ZaukerDecompositionMaxRate, Math.Min(nitrogen, zauker));
        if (burned <= 0f)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.Zauker, -burned);
        mixture.AdjustMoles(Gas.Oxygen, burned * 0.3f);
        mixture.AdjustMoles(Gas.Nitrogen, burned * 0.7f);

        // Exothermic.
        var energyReleased = PirateAtmospherics.ZaukerDecompositionEnergy * burned;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
