using Content.Shared.Chat;
using Content.Shared.Speech;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private SpeechVerbPrototype GetSpeechVerbWithOverride(EntityUid source, string message, Content.Shared._Pirate.VoiceMask.SpeakerNameContext context = default) // Pirate: voice mask modes
    {
        var nameEv = new TransformSpeakerNameEvent(source, Name(source)) { Context = context }; // Pirate: voice mask modes
        RaiseLocalEvent(source, nameEv);

        if (nameEv.SpeechVerb != null && _prototypeManager.Resolve(nameEv.SpeechVerb, out var proto))
            return proto;

        return GetSpeechVerb(source, message);
    }
}
