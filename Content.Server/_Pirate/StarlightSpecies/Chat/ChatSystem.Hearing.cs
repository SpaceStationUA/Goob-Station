using Content.Server._Pirate.StarlightSpecies;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private float ListenerRange(EntityUid listener, float range)
    {
        if (!TryComp<ChatListenerRangeComponent>(listener, out var hearing) || !hearing.AllowExtendListenRange)
            return range;
        if (range == WhisperMuffledRange)
            return hearing.WhisperMuffledRange;
        if (range == WhisperClearRange)
            return hearing.WhisperClearRange;
        return range == VoiceRange ? hearing.VoiceRange : range;
    }
}
