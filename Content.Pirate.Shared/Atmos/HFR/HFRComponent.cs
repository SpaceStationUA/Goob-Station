// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Atmos.HFR;

/// <summary>
///     Shared component for the Hyper-torus Fusion Reactor (HFR).
///     A 3x3 multipart machine with three gas loops: Fusion Mix, Moderator Mix and Coolant.
///     Mechanics ported from /tg/station's HFR (see the wiki guide).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HFRComponent : Component
{
    // --- Tunable settings ---

    /// <summary>
    ///     Sets the maximum internal rate of change in the Fusion Mix's temperature (50-500, per /tg/).
    ///     Default 200: TG's default is 100 (too slow to ignite on this server), but 300 made the
    ///     reactor shoot straight to maximum temperature, which read as runaway numbers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeatingConductor = 200f;

    /// <summary>
    ///     Magnetic constrictor (50-1000, per /tg/). Higher values reduce the magnitude of the reaction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MagneticConstrictor = 100f;

    /// <summary>
    ///     Fuel injection rate (0.5-150 mol/s, per /tg/). Sets the rate gas is pulled from the
    ///     fuel port and the scale at which fuel is consumed (F = FIR * 0.01 * 5 * power level).
    ///     Default 25 (per /tg/ hfr_core.dm).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FuelInjectionRate = 25f;

    /// <summary>
    ///     Current dampener (0-1000). A sufficiently unstable reaction flips from
    ///     exothermic (heating) to endothermic (cooling).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentDampener = 0f;

    /// <summary>
    ///     Moderator injection rate (0.5-150 mol/s, per /tg/).
    ///     Default 25 (per /tg/ hfr_core.dm).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ModeratorInjectionRate = 25f;

    /// <summary>
    ///     If enabled, moves byproducts and moderator filter gas to the output port.
    ///     Default on: without it the main byproduct (e.g. CO2) accumulates inside the
    ///     fusion mix and drains the E=mc² energy metric, which reads as the reactor
    ///     "discharging". TG defaults it off, but the extra manual step confused players.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WasteRemoval = true;

    /// <summary>
    ///     Master power switch (per /tg/). Gates the fusion reaction: with it off the reactor
    ///     does not burn fuel, produce gas or generate heat output.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartPower;

    /// <summary>
    ///     Cooling switch (per /tg/). Gates heat exchange between the moderator mix and the coolant.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartCooling;

    /// <summary>
    ///     Fuel injection switch (per /tg/). Gates gas pulled from the fuel port.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartFuel;

    /// <summary>
    ///     Moderator injection switch (per /tg/). Gates gas pulled from the moderator port.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StartModerator;

    /// <summary>
    ///     Rate at which the moderator filter gas is moved to the output (5-200 mol/s).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ModeratorFilteringRate = 20f;

    /// <summary>
    ///     Gas removed from the moderator mix into output when waste removal is enabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Gas? ModeratorFilter;

    /// <summary>
    ///     Currently selected recipe. Defaults to Plasma + Oxygen: it is the only recipe
    ///     whose fuels (Plasma, Oxygen) are common gases, so a freshly-built reactor can
    ///     actually be started. TG defaults to no recipe at all and forces the player to
    ///     pick one, but that left our default (HyperNoblium + Tritium) unstartable.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HfrRecipe Recipe = HfrRecipe.PlasmaOxygen;

    // --- State ---

    /// <summary>
    ///     Current fusion power level (0-6).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PowerLevel;

    /// <summary>
    ///     Integrity of the reactor (0-100).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Integrity = 100f;

    /// <summary>
    ///     Iron content (%). Worsens at high power levels, damages integrity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IronContent;

    /// <summary>
    ///     Amount the fusion mix temperature changes in one update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeatOutput;

    /// <summary>
    ///     Heat limiter modifier: 1e[PowerLevel - 1] * HeatingConductor.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeatLimiterModifier;

    /// <summary>
    ///     E=MC^2 style energy metric.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Energy;

    /// <summary>
    ///     Whether the reaction is currently endothermic (cooling).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Endothermic;

    /// <summary>
    ///     Whether a meltdown countdown is currently running. Once integrity drops
    ///     to <see cref="HFRConstants.MeltdownIntegrityThreshold"/> the reactor starts
    ///     a countdown and will explode when it runs out.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool MeltdownCountdownActive;

    /// <summary>
    ///     Seconds remaining in the meltdown countdown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeltdownCountdown;

    /// <summary>
    ///     Tracks the last countdown second a radio message was sent for, so the
    ///     warning only goes out once per interval.
    /// </summary>
    public float LastMeltdownMessageAt;

    /// <summary>
    ///     Looping siren stream played while the meltdown countdown is running
    ///     (stopped when the countdown is cancelled or the reactor melts down).
    /// </summary>
    public EntityUid? MeltdownSirenStream;

    /// <summary>
    ///     Whether the critical explosion warning (HFR_critical_explosion) has
    ///     already been played once at the 10-seconds-to-go mark.
    /// </summary>
    public bool MeltdownCriticalSoundPlayed;

    // --- Gas mixes (server-authoritative) ---

    /// <summary>
    ///     The fusion mix — where the fusion reaction happens. Persisted so the
    ///     reactor state survives map saves. // Pirate
    /// </summary>
    [DataField]
    public GasMixture FusionMix = new(HFRConstants.FusionMixVolume)
    {
        Temperature = Atmospherics.T20C
    };

    /// <summary>
    ///     The moderator mix — controls the reaction. Persisted so the reactor
    ///     state survives map saves. // Pirate
    /// </summary>
    [DataField]
    public GasMixture ModeratorMix = new(HFRConstants.ModeratorMixVolume)
    {
        Temperature = Atmospherics.T20C
    };

    // --- Temperature rate-of-change tracking (server-only, for the monitoring chart) ---

    /// <summary>Temperature of the previous atmos tick, K/s deltas derived from these.</summary>
    public float FusionTempArchived;
    public float ModeratorTempArchived;
    public float CoolantTempArchived;
    public float OutputTempArchived;

    public float FusionTempDelta;
    public float ModeratorTempDelta;
    public float CoolantTempDelta;
    public float OutputTempDelta;

    /// <summary>
    ///     Accumulated time since the last UI state push (server-only). The UI is
    ///     throttled to <see cref="HFRConstants.UiUpdateInterval"/> so a running
    ///     reactor doesn't rebuild the client window every atmos tick.
    /// </summary>
    public float UiUpdateAccumulator;

    /// <summary>
    ///     Cached visual state so we only push appearance changes when it actually changes.
    /// </summary>
    [DataField]
    public HFRVisualState VisualState = HFRVisualState.Idle;
}

/// <summary>
///     UI key for the HFR interface.
/// </summary>
[Serializable, NetSerializable]
public enum HFRUiKey : byte
{
    Key
}

/// <summary>
///     Identifiers for each of the eight parts surrounding the HFR core.
///     Used as keys in the <c>MultipartMachine</c> parts dictionary.
/// </summary>
[Serializable, NetSerializable]
public enum HFRParts : byte
{
    FuelInput,
    ModeratorInput,
    WasteOutput,
    Interface,
    CornerNW,
    CornerNE,
    CornerSW,
    CornerSE,
}

/// <summary>
///     Marker component for the HFR fuel input port part.
/// </summary>
[RegisterComponent]
public sealed partial class HFRFuelInputComponent : Component;

/// <summary>
///     Marker component for the HFR moderator input port part.
/// </summary>
[RegisterComponent]
public sealed partial class HFRModeratorInputComponent : Component;

/// <summary>
///     Marker component for the HFR waste output port part.
/// </summary>
[RegisterComponent]
public sealed partial class HFRWasteOutputComponent : Component;

/// <summary>
///     Marker component for the HFR interface part.
/// </summary>
[RegisterComponent]
public sealed partial class HFRInterfaceComponent : Component;

/// <summary>
///     Marker component for the HFR structural corner parts.
/// </summary>
[RegisterComponent]
public sealed partial class HFRCornerComponent : Component;

/// <summary>
///     Sprite layers of the HFR core (master entity).
/// </summary>
[Serializable, NetSerializable]
public enum HFRVisualLayers : byte
{
    /// <summary>
    ///     The base core sprite.
    /// </summary>
    Base,

    /// <summary>
    ///     Animated overlay shown while the reactor is running.
    /// </summary>
    Active,

    /// <summary>
    ///     Cracked overlay shown when the reactor is heavily damaged.
    /// </summary>
    Crack,
}
