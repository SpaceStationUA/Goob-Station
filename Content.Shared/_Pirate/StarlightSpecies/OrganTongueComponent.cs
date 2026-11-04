using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.StarlightSpecies;

/// <summary>Starlight species voice data, attached through the native organ lifecycle.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganTongueComponent : Component
{
    [DataField]
    public bool IsMuted;

    [DataField]
    public List<ProtoId<EmotePrototype>> AllowedEmotes = new();

    [DataField]
    public Dictionary<Sex, ProtoId<EmoteSoundsPrototype>>? Sounds;

    [DataField]
    public bool AllowAllVocalEmotes;
}
