using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;

namespace Content.Pirate.Shared._Pirate.MartialArts;

[RegisterComponent]
public sealed partial class GrantUnathiTailComponent : GrantMartialArtKnowledgeComponent
{
    [DataField]
    public override MartialArtsForms MartialArtsForm { get; set; } = MartialArtsForms.UnathiTailArts;

    public override LocId? LearnMessage { get; set; } = "unathi-tail-success-learned";
}
