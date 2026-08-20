using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared._DV.Psionics.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AntiPsionicWeaponComponent : Component
{
    /// <summary>
    /// The DamageModifiers for each DamageType.
    /// </summary>
    [DataField(required: true)]
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// The additional stamina damage dealt by anti-psionic weaponry.
    /// </summary>
    [DataField]
    public float StaminaDamageMultiplier = 1f;

    /// <summary>
    /// The chance to disable the target's psionic abilities on hit.
    /// </summary>
    [DataField]
    public float DisableChance = 0.3f;

    /// <summary>
    /// Punish the user when used against a non-psionic target.
    /// </summary>
    [DataField]
    public bool Punish;

    /// <summary>
    /// The chance for the weapon to punish the user when used against a non-psionic target.
    /// </summary>
    [DataField]
    public float PunishChance = 0.5f;

    /// <summary>
    /// Message shown to the wielder when the weapon is used against a non-psionic target.
    /// Used for the mantis pendulum - the "test" feedback when the subject lacks the gift.
    /// </summary>
    [DataField]
    public LocId? PunishMessage;

    /// <summary>
    /// Whether the punish applies stutter/jitter/knockdown status effects to the user.
    /// The mantis pendulum only shows the message - it doesn't shake or knock you over.
    /// </summary>
    [DataField]
    public bool PunishStatusEffects = true;

    /// <summary>
    /// The sound created when hitting a psionic user with the weapon or being punished.
    /// </summary>
    [DataField]
    public SoundSpecifier? HitSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
}
