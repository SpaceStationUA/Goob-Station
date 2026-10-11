using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Voidwalker.GameTicking.Rules;

[RegisterComponent]
public sealed partial class VoidwalkerRuleComponent : Component
{
    [DataField]
    public SoundPathSpecifier BriefingSound = new("/Audio/_Pirate/Voidwalker/Ambience/Antag/voidwalker_start.ogg");

    [DataField]
    public string VoidFaction = "Void"; // Pirate: match the source faction prototype ID.

    [DataField]
    public string NanotrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId VoidwalkerMindRole = "MindRoleVoidwalker";
}
