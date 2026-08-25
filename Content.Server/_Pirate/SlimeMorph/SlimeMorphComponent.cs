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
    /// Structural body-part layers worth copying on mimic (baked shapes markings can't reproduce,
    /// like a muzzle head or digitigrade legs). Unlisted layers always keep the slime's own sprite.
    /// </summary>
    [DataField]
    public HumanoidVisualLayers[] CopyableLayers =
    {
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LFoot,
        HumanoidVisualLayers.RFoot,
    };

    /// <summary>
    /// Base sprites worth copying on mimic, keyed by the species' prototype id for that body part
    /// (e.g. "MobVulpkaninHead", "MobVulpkaninLLeg"). Visible baked shapes have a brightness
    /// multiplier that normalizes them to the slime body; an empty base may also be listed when a
    /// structural marking must replace rather than overlay the slime's own sprite. A body part whose
    /// base id isn't listed here keeps the slime's own sprite for that layer.
    /// </summary>
    [DataField]
    public Dictionary<string, float> CopyableLayerFactors = new()
    {
        ["MobVulpkaninHead"] = 0.84f,
        ["MobVulpkaninLLeg"] = 0.84f,
        ["MobVulpkaninRLeg"] = 0.84f,
        ["MobVulpkaninLFoot"] = 0.84f,
        ["MobVulpkaninRFoot"] = 0.84f,
        ["MobTajaranHead"] = 0.86f,
        ["MobTajaranLLeg"] = 0.86f,
        ["MobTajaranRLeg"] = 0.86f,
        ["MobTajaranLFoot"] = 0.86f,
        ["MobTajaranRFoot"] = 0.86f,
        ["MobFeroxiHead"] = 0.92f,
        ["MobFeroxiLLeg"] = 0.92f,
        ["MobFeroxiRLeg"] = 0.92f,
        ["MobFeroxiLFoot"] = 0.92f,
        ["MobFeroxiRFoot"] = 0.92f,
        // Reptilian has no baked head worth copying (its snout is a marking), but its digitigrade
        // legs are a distinct baked shape. Scales read close to base color, so no darkening yet -
        // tune this once someone eyeballs it in-game.
        ["MobReptilianLLegDigi"] = 1f,
        ["MobReptilianRLegDigi"] = 1f,
        ["MobReptilianLFootDigi"] = 1f,
        ["MobReptilianRFootDigi"] = 1f,
    };

    /// <summary>
    /// Alpha applied to copied structural layers. Slime body art has about 0.66 average pixel alpha,
    /// while the copied species parts and Unathi head markings are opaque.
    /// </summary>
    [DataField]
    public float CopiedLayerAlpha = 0.66f;

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

    /// <summary>Base-sprite overrides for copied structural layers (baked heads, digitigrade legs, ...), keyed by layer. A layer missing here keeps the slime's own sprite.</summary>
    public Dictionary<HumanoidVisualLayers, string> BodyLayers = new();

    /// <summary>Species used to populate marking pickers.</summary>
    public string? PickerSpecies;

    /// <summary>True when this buffer holds a look derived from a studied target (mimic), not free self-edits.</summary>
    public bool FromTarget;

    /// <summary>The studied target this buffer was loaded from, for the list highlight.</summary>
    public NetEntity? SelectedTarget;
}
