// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Caduceus;    /// <summary>
    ///     The Caduceus: a slime weapon of the Index. It starts inert (a slime vial) and, while held
    ///     by an Index member, can be toggled into a weapon form by the "toggle" action or by using
    ///     it in hand (Z) - both share the toggle action's 10s useDelay cooldown. The "swap"
    ///     action shifts it into a random other form on a 30s cooldown.
    ///     Once it shifts into the <see cref="CaduceusForm.Fpoon"/>, the form is permanent: it can no
    ///     longer be toggled, swapped or dropped - the only escape is suicide.
    /// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CaduceusComponent : Component
{
    /// <summary>Current form. <see cref="CaduceusForm.Inactive"/> means the weapon is an inert slime vial.</summary>
    [DataField, AutoNetworkedField]
    public CaduceusForm CurrentForm = CaduceusForm.Inactive;

    /// <summary>
    ///     Per-form configuration (damage, attack rate, range, sprite...). Loaded from the prototype on
    ///     both client and server; not networked.
    /// </summary>
    [DataField]
    public Dictionary<CaduceusForm, CaduceusFormEntry> Forms = new();

    /// <summary>Whether the weapon is currently toggled into a weapon form (only possible while held by an Index member).</summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    ///     Original entity name/description captured at map init (from the prototype), restored
    ///     whenever the Caduceus is deactivated back into the inert slime. Transient server state
    ///     - never networked.
    /// </summary>
    public string? BaseName;
    public string? BaseDescription;

    /// <summary>Entity currently holding this weapon in a hand, if any.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Holder;

    /// <summary>
    ///     Form change requested while the wielder was mid-attack. Applied (server-side) once the
    ///     current attack finishes. Transient server state - never networked.
    /// </summary>
    public CaduceusForm? PendingForm;

    /// <summary>Whether the pending form change should play the swap sound once applied.</summary>
    public bool PendingPlaySwap;

    /// <summary>
    ///     "Toggle" action: activates the Caduceus (transforms into a random weapon form) or
    ///     deactivates it (collapses back into the inert slime). Its 10s useDelay cooldown gates
    ///     how often the weapon can change state - the same cooldown is shared by the Z use-in-hand
    ///     toggle so it cannot be bypassed.
    /// </summary>
    [DataField]
    public EntProtoId ToggleAction = "ActionCaduceusToggle";

    [DataField]
    public EntityUid? ToggleActionEntity;

    /// <summary>"Swap" action: instantly shifts into a random other form.</summary>
    [DataField]
    public EntProtoId SwapAction = "ActionCaduceusSwap";

    [DataField]
    public EntityUid? SwapActionEntity;

    /// <summary>"Hold" action: doubles the remaining hits for the current form (capped at 2x its max) and grants +1 KARMIC CONSEQUENCE.</summary>
    [DataField]
    public EntProtoId HoldAction = "ActionCaduceusHold";

    [DataField]
    public EntityUid? HoldActionEntity;

    /// <summary>
    ///     Hits remaining before the weapon shifts into a random other form. Server-side transient
    ///     state - reset whenever a form is entered (see <see cref="CaduceusFormEntry.MaxHits"/>).
    /// </summary>
    public int HitsLeft;

    /// <summary>Sound played when the "swap" action is used (falls back to the form's transform sound).</summary>
    [DataField]
    public SoundSpecifier SwapSound = new SoundPathSpecifier("/Audio/_Pirate/Weapons/Melee/Caduceus/vial_swap.ogg");
}

/// <summary>All Caduceus weapon forms.</summary>
[Serializable, NetSerializable]
public enum CaduceusForm : byte
{
    /// <summary>The weapon is an inert slime vial - no damage, slime visuals.</summary>
    Inactive,

    Hatchet,
    Stiletto,
    BastardSword,
    Rapier,
    Hammer,
    Greatsword,
    Lance,
    Whip,
    Scythe,

    /// <summary>Permanent form that only serves suicide. Cannot be toggled away, swapped or dropped.</summary>
    Fpoon,
}

/// <summary>Configuration for a single Caduceus form.</summary>
[DataDefinition]
public sealed partial class CaduceusFormEntry
{
    /// <summary>Damage dealt by this form. Null/empty = no damage (inactive).</summary>
    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>Melee attack rate (attacks per second).</summary>
    [DataField]
    public float AttackRate = 1f;

    /// <summary>Melee attack range.</summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>Attack animation override for this form.</summary>
    [DataField]
    public EntProtoId? Animation;

    /// <summary>Wide swing animation override for this form.</summary>
    [DataField]
    public EntProtoId? WideAnimation;

    /// <summary>
    ///     Hits this form can land before the Caduceus shifts into a random other weapon.
    ///     0 disables hit-based form changing (inactive slime, permanent fpoon).
    /// </summary>
    [DataField]
    public int MaxHits;

    /// <summary>Wide attack arc width. 0 makes the attack a precise straight stab (like a spear).</summary>
    [DataField]
    public Angle Angle = Angle.FromDegrees(60);

    /// <summary>Rotation of the light attack animation.</summary>
    [DataField]
    public Angle AnimationRotation = Angle.Zero;

    /// <summary>Rotation of the wide attack animation.</summary>
    [DataField]
    public Angle WideAnimationRotation = Angle.Zero;

    /// <summary>Whether this form can perform wide (heavy) swings.</summary>
    [DataField]
    public bool CanWideSwing = true;

    /// <summary>World icon state (from icons.rsi).</summary>
    [DataField]
    public string IconState = "inactive";

    /// <summary>In-hand RSI used while this form is held.</summary>
    [DataField]
    public ResPath InhandRsi = new("_Pirate/Objects/Weapons/Melee/Caduceus/vial_inactive.rsi");

    /// <summary>World sprite scale for this form.</summary>
    [DataField]
    public float Scale = 1f;

    /// <summary>Sound played when the weapon transforms into this form.</summary>
    [DataField]
    public SoundSpecifier? TransformSound;
}

/// <summary>Appearance keys pushed by the server for the Caduceus.</summary>
[Serializable, NetSerializable]
public enum CaduceusVisuals : byte
{
    /// <summary>Current effective form (<see cref="CaduceusForm.Inactive"/> when the weapon is inert).</summary>
    Form,
}

/// <summary>Sprite layer map keys used by the Caduceus.</summary>
[Serializable, NetSerializable]
public enum CaduceusVisualLayers : byte
{
    Icon,
}
