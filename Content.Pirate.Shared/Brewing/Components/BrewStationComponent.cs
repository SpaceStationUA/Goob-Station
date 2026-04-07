using System.Collections.Generic;
using Content.Shared.Chat;
using Content.Shared.Speech.Components;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Brewing.Components;

[RegisterComponent]
public sealed partial class BrewStationComponent : Component
{
    [DataField(required: true)]
    public List<string> VoiceCommands = new();

    [DataField]
    public float ListenRange = SharedChatSystem.VoiceRange;

    // Shown in the UI so players know what to say near the station.
    [DataField]
    public string VoiceHint = string.Empty;

    [ViewVariables]
    public bool LastMixingState;
}
