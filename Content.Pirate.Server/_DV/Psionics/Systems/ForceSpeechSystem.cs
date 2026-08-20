using Content.Server.Chat.Systems;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Events;
using Content.Shared.Chat;

namespace Content.Server._DV.Psionics.Systems;

/// <summary>
///     Makes an entity speak a configured set of words whenever one of its psionic
///     power actions is used successfully. The words live on a
///     <see cref="ForceSpeechComponent"/> attached to either the action entity
///     (data-driven, via the action prototype) or the psionic source itself.
///     <see cref="PsionicPowerUsedEvent"/> is only raised when a power actually
///     succeeds (blocked/refunded powers return before <c>AfterPowerUsed</c>),
///     so this only fires on a successful action.
/// </summary>
public sealed class ForceSpeechSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsionicPowerUsedEvent>(OnPowerUsed);
    }

    private void OnPowerUsed(PsionicPowerUsedEvent ev)
    {
        // Prefer the component on the action entity, falling back to the psionic source.
        if (ev.ActionEntity is { } action && TryComp<ForceSpeechComponent>(action, out var comp))
        {
        }
        else if (!TryComp<ForceSpeechComponent>(ev.PsionicSource, out comp))
        {
            return;
        }

        var message = Loc.GetString(comp.SpeechText, ("entity", ev.User));
        var type = comp.SpeechType == ForceSpeechType.Emote
            ? InGameICChatType.Emote
            : InGameICChatType.Speak;

        // ignoreActionBlocker: true - the words are forced out, even if the caster is silenced.
        _chat.TrySendInGameICMessage(ev.User, message, type, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }
}
