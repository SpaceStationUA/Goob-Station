namespace Content.Server._Pirate.StarlightSpecies;

/// <summary>Starlight lagomorph hearing ranges, measured in tiles.</summary>
[RegisterComponent]
public sealed partial class ChatListenerRangeComponent : Component
{
    [DataField] public int VoiceRange = 10;
    [DataField] public int WhisperClearRange = 2;
    [DataField] public int WhisperMuffledRange = 5;
    [DataField] public bool AllowExtendListenRange;
}
