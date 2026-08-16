// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.SlimeMorph;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.SlimeMorph;

/// <summary>
/// Grants a slimeperson the "Morph" action (self-customization + mimic menu) and the "Study Appearance"
/// verb, and stores the appearances they have remembered.
/// </summary>
[RegisterComponent]
public sealed partial class SlimeMorphComponent : Component
{
    [DataField]
    public EntProtoId MorphAction = "ActionSlimeMorph";

    [DataField]
    public EntityUid? MorphActionEntity;

    /// <summary>
    /// Organic humanoid species whose appearance can be studied and mimicked. Acts as a whitelist:
    /// anything not listed (Vox, Diona, IPC, Plasmaman, Skeleton, ...) cannot be sampled.
    /// </summary>
    [DataField]
    public List<ProtoId<SpeciesPrototype>> MorphableSpecies = new()
    {
        "Human",
        "SlimePerson",
        "Felinid",
        "Reptilian",
        "Vulpkanin",
        "Harpy",
        "Oni",
        "Moth",
        "Dwarf",
        "Tajaran",
        "Rodentia",
        "Feroxi",
        "Shadowkin",
    };

    /// <summary>
    /// How strongly copied marking colors are pulled toward the slime's own skin color.
    /// 0 = keep the target's original colors, 1 = fully recolored to slime skin.
    /// </summary>
    [DataField]
    public float TintFactor = 0.8f;

    /// <summary>
    /// Alpha applied to copied marking layers so mimicked parts read as translucent slime jelly.
    /// </summary>
    [DataField]
    public float TintAlpha = 0.85f;

    /// <summary>
    /// Head bases worth copying on mimic, keyed by the species' head prototype id. Visible baked
    /// muzzles/noses have a brightness multiplier that normalizes them to the slime body; an empty
    /// base may also be listed when a structural Head marking must replace rather than overlay the
    /// slime head. Unlisted species keep the slime's own head.
    /// </summary>
    [DataField]
    public Dictionary<string, float> HeadColorFactors = new()
    {
        ["MobVulpkaninHead"] = 0.84f,
        ["MobTajaranHead"] = 0.86f,
        ["MobFeroxiHead"] = 0.92f,
    };

    /// <summary>
    /// Alpha applied to copied structural faces. Slime body art has about 0.66 average pixel alpha,
    /// while the copied species heads and Unathi head markings are opaque.
    /// </summary>
    [DataField]
    public float HeadColorAlpha = 0.66f;

    /// <summary>
    /// Fraction of current nutrition consumed when committing a mimic. Self-edits are free.
    /// </summary>
    [DataField]
    public float NutritionCostFraction = 0.15f;

    /// <summary>
    /// Sound played when the slime reshapes itself - the squish ("Хлюп") from the Squish emote.
    /// Played directly; we do not force the emote itself.
    /// </summary>
    [DataField]
    public SoundSpecifier MorphSound = new SoundCollectionSpecifier("Squishes");

    /// <summary>
    /// Appearances the slime has studied, keyed by the sampled entity.
    /// </summary>
    [ViewVariables]
    public Dictionary<NetEntity, SlimeMorphAppearance> Remembered = new();

    /// <summary>
    /// Looks the slime has saved from the menu under a name, so they can be reloaded later. Keyed by
    /// (name, xenotype); saving with a matching key overwrites. Shown in the right-side list.
    /// </summary>
    [ViewVariables]
    public List<SlimeMorphAppearance> Saved = new();

    /// <summary>
    /// The slime's own look, captured just before the first mimic so "Revert to self" can restore it.
    /// </summary>
    [ViewVariables]
    public SlimeMorphAppearance? OriginalAppearance;

    /// <summary>
    /// Pending self-customization edits. Nothing on the body changes until the player commits them,
    /// so this accumulates changes made in the menu.
    /// </summary>
    [ViewVariables]
    public SlimeMorphWorking? Staged;

    /// <summary>
    /// The look at the moment the menu was opened, used by the "Reset" button.
    /// </summary>
    [ViewVariables]
    public SlimeMorphWorking? Opened;
}

/// <summary>
/// A mutable working copy of a slime's editable appearance while the morph menu is open.
/// </summary>
public sealed class SlimeMorphWorking
{
    public Sex Sex;
    public Gender Gender;
    public Color SkinColor;
    public Color EyeColor;
    public float Height = 1f;
    public float Width = 1f;
    public MarkingSet Markings = new();

    /// <summary>Head base-sprite override (baked head shapes like muzzles); null = slime's own head.</summary>
    public string? HeadLayer;

    /// <summary>Species used to populate marking pickers.</summary>
    public string? PickerSpecies;

    /// <summary>True when this buffer holds a look derived from a studied target (mimic), not free self-edits.</summary>
    public bool FromTarget;

    /// <summary>The studied target this buffer was loaded from, for the list highlight.</summary>
    public NetEntity? SelectedTarget;
}
