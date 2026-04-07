using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Tag;
using Content.Goobstation.Common.Barks;
using Robust.Shared.Maths;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Alchemy.Components;

[RegisterComponent]
public sealed partial class PhilosopherStoneSpeciesMemoryComponent : Component
{
    [DataField]
    public string? OriginalSpecies;

    [DataField]
    public Color OriginalSkinColor;

    [DataField]
    public bool HasOriginalSkinColor;

    [DataField]
    public bool TriggerReady = true;

    [DataField]
    public float AccumulatedIntake;

    [DataField]
    public float LastObservedQuantity;

    public bool HasOriginalAppearance;
    public MarkingSet? OriginalMarkings;
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>? OriginalCustomBaseLayers;
    public Color OriginalEyeColor;
    public ProtoId<BarkPrototype>? OriginalBarkVoice;
    public Sex OriginalSex;
    public Gender OriginalGender;
    public int OriginalAge;
    public float OriginalHeight;
    public float OriginalWidth;
    public ProtoId<SpeechSoundsPrototype>? OriginalSpeechSounds;
    public ProtoId<SpeechVerbPrototype>? OriginalSpeechVerb;
    public Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>? OriginalVocalSounds;
    public ProtoId<EmoteSoundsPrototype>? OriginalEmoteSounds;
    public bool HadVulpEmotesTag;
}
