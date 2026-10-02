// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Administration.MentorHelp;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Administration.MentorHelp;

public sealed class MentorHelpSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public event Action<MentorHelpTextMessage>? MessageReceived;
    public event Action<MentorHelpPlayerTypingUpdated>? TypingReceived;

    private (TimeSpan Timestamp, bool Typing) _lastTypingUpdateSent;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MentorHelpTextMessage>(msg => MessageReceived?.Invoke(msg));
        SubscribeNetworkEvent<MentorHelpPlayerTypingUpdated>(msg => TypingReceived?.Invoke(msg));
    }

    public void Send(NetUserId channel, string text, bool playSound, bool responderOnly = false)
    {
        // The server sets the sender from the authenticated session.
        RaiseNetworkEvent(new MentorHelpTextMessage(channel, channel, text, playSound: playSound, responderOnly: responderOnly));
        SendTyping(channel, false);
    }

    public void SendTyping(NetUserId channel, bool typing)
    {
        if (_lastTypingUpdateSent.Typing == typing &&
            _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
        {
            return;
        }

        _lastTypingUpdateSent = (_timing.RealTime, typing);
        RaiseNetworkEvent(new MentorHelpClientTypingUpdated(channel, typing));
    }
}
