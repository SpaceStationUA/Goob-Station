/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Planets.Shields;

/// <summary>
/// The ground-side planetary shield generator: a 5x5 monolith that chews a
/// <see cref="DrawRate"/> of grid power into an internal buffer, spins up once the
/// buffer is full (playing <see cref="ChargeSound"/>), then fires the sky beam and
/// raises the planet's <see cref="CEPlanetShieldComponent"/>. Lifecycle:
/// <list type="number">
/// <item>Charging — accumulates <see cref="Charge"/> from received grid power until
/// <see cref="MaxCharge"/>.</item>
/// <item>SpinningUp — fixed <see cref="SpinupTime"/> countdown matched to the chargeup
/// sound. Taking more than <see cref="SpinupDamageThreshold"/> total damage in this
/// window detonates the generator in a conventional blast. EMP pulses landing on
/// the generator mid-spinup also set it off.</item>
/// <item>Active — beam up, shield up, continuous full draw; grid shortfalls drain the
/// buffer instead (240 GJ ≈ 120 s of grace) and an empty buffer fails the field.</item>
/// <item>Cooldown — post-failure lockout for <see cref="CooldownTime"/>, drawing
/// nothing, then back to Charging.</item>
/// </list>
/// Driven entirely by the server <c>CEShieldGeneratorSystem</c>; only
/// <see cref="Stage"/> networks, for client visuals/examine.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEShieldGeneratorComponent : Component
{
    /// <summary>Where in the charge/spinup/fire/cooldown cycle this generator is.</summary>
    [DataField, AutoNetworkedField]
    public CEShieldGeneratorStage Stage = CEShieldGeneratorStage.Charging;

    /// <summary>
    /// Master switch (toggled from the generator window). When off the generator draws
    /// nothing from the grid and the cycle is parked in Charging; turning it off mid-spinup
    /// or mid-field drops the shield immediately.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>Joules currently banked in the internal accumulator.</summary>
    [DataField]
    public float Charge;

    /// <summary>
    /// Joules needed to begin spinup, and the grace buffer burned while Active if the
    /// grid can't cover <see cref="DrawRate"/>. 120 MJ = 120 s of grace at full draw.
    /// </summary>
    [DataField]
    public float MaxCharge = 120_000_000f;

    /// <summary>
    /// Grid power wanted at all times outside Cooldown, in watts. Charging banks it,
    /// Active burns it to sustain the field.
    /// </summary>
    [DataField]
    public float DrawRate = 1_000_000f;

    /// <summary>
    /// How long spinup takes once the buffer is full.
    /// </summary>
    [DataField]
    public TimeSpan SpinupTime = TimeSpan.FromSeconds(79);

    /// <summary>Server curtime at which spinup completes and the beam fires.</summary>
    [DataField]
    public TimeSpan SpinupEndsAt;

    /// <summary>
    /// Total damage the generator may soak during SpinningUp before it detonates
    /// instead of firing.
    /// </summary>
    [DataField]
    public float SpinupDamageThreshold = 100f;

    /// <summary>Damage accumulated so far during the current spinup.</summary>
    [DataField]
    public float SpinupDamageTaken;

    /// <summary>
    /// Detonation is in flight. The blast's own EMP washes back over the generator
    /// before it's deleted; this keeps that echo from re-triggering the detonation.
    /// </summary>
    public bool Detonating;

    /// <summary>Lockout after the field collapses before recharging may resume.</summary>
    [DataField]
    public TimeSpan CooldownTime = TimeSpan.FromMinutes(5);

    /// <summary>Server curtime at which Cooldown ends.</summary>
    [DataField]
    public TimeSpan CooldownEndsAt;

    /// <summary>Long chargeup track played over the whole spinup window.</summary>
    [DataField]
    public SoundSpecifier ChargeSound = new SoundPathSpecifier("/Audio/_FarHorizons/Explosions/shieldcharge.ogg");

    /// <summary>Played when spinup completes and the beam erupts.</summary>
    [DataField]
    public SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_FarHorizons/Explosions/shieldgenfire.ogg");

    /// <summary>Played when the field collapses from power starvation.</summary>
    [DataField]
    public SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/_FarHorizons/Explosions/shieldfail.ogg");

    /// <summary>
    /// Looped track played for <see cref="ActiveSoundDuration"/> when the field goes up,
    /// then cut — the generator runs a loud start and then just hums along silently.
    /// </summary>
    [DataField]
    public SoundSpecifier ActiveSound = new SoundPathSpecifier("/Audio/_FarHorizons/Explosions/shieldcharge.ogg");

    /// <summary>How long the active-stage loop plays before going silent.</summary>
    [DataField]
    public TimeSpan ActiveSoundDuration = TimeSpan.FromSeconds(8);

    /// <summary>How long the beam-fire camera shake lasts for everyone on the planet.</summary>
    [DataField]
    public float FireShakeDuration = 4f;

    /// <summary>Peak beam-fire shake amplitude, in tiles.</summary>
    [DataField]
    public float FireShakeAmplitude = 1.5f;

    /// <summary>
    /// Length of the whiteout applied to every player on the planet when the beam
    /// fires. Purely cinematic: no slow, no stun.
    /// </summary>
    [DataField]
    public TimeSpan FireFlashDuration = TimeSpan.FromSeconds(3);

    /// <summary>The chargeup audio stream entity, so detonation can cut it short.</summary>
    [ViewVariables]
    public EntityUid? ChargeSoundStream;

    /// <summary>The active-stage loop stream, cut once its duration elapses or the field drops.</summary>
    [ViewVariables]
    public EntityUid? ActiveSoundStream;

    /// <summary>Server curtime at which the active-stage loop is cut.</summary>
    [ViewVariables]
    public TimeSpan ActiveSoundEndsAt;

    /// <summary>Next server curtime the open control window (if any) gets a state push.</summary>
    [ViewVariables]
    public TimeSpan NextUiUpdate;
}

/// <seealso cref="CEShieldGeneratorComponent.Stage"/>
[Serializable, NetSerializable]
public enum CEShieldGeneratorStage : byte
{
    Charging,
    SpinningUp,
    Active,
    Cooldown,
}

/// <summary>Appearance keys for the generator's client visuals (visuals task).</summary>
[Serializable, NetSerializable]
public enum CEShieldGeneratorVisuals : byte
{
    Stage,
}
