// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Chat.Systems;

/// <summary>Checks player input before chat, speech, TTS or PDA delivery. Help channels bypass this system.</summary>
public sealed class PirateChatFilterSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;

    public static bool ContainsBlockedCharacters(string message) => message.AsSpan().IndexOfAny("ъыэЪЫЭ") >= 0;

    public bool Reject(ICommonSession? player, string message)
    {
        if (player == null || !ContainsBlockedCharacters(message))
            return false;

        _chat.DispatchServerMessage(player, Loc.GetString("pirate-chat-disallowed-letters"), suppressLog: true);
        return true;
    }

    public bool Reject(EntityUid actor, string message)
    {
        return TryComp<ActorComponent>(actor, out var player) && Reject(player.PlayerSession, message);
    }
}
