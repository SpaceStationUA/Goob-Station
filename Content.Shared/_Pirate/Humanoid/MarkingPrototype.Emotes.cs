// Pirate: Starlight shell markings grant their corresponding emotes.
using Content.Shared._Pirate.StarlightSpecies.Markings;

namespace Content.Shared.Humanoid.Markings;

public sealed partial class MarkingPrototype
{
    [DataField]
    public List<MarkingEmoteData>? Emotes;
}
