// SPDX-License-Identifier: MIT

using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Ghost;

public sealed class ReturnToBodyEui : BaseEui
{
    private readonly GhostSystem _ghostSystem; // Pirate
    private readonly ISharedPlayerManager _player;
    private readonly NetUserId? _userId;

    public ReturnToBodyEui(MindComponent mind, GhostSystem ghostSystem, ISharedPlayerManager player)
    {
        _ghostSystem = ghostSystem;
        _player = player;
        _userId = mind.UserId;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not ReturnToBodyMessage choice ||
            !choice.Accepted)
        {
            Close();
            return;
        }

        if (_userId is { } userId && _player.TryGetSessionById(userId, out var session))
            _ghostSystem.TryReturnToBody(session); // Pirate

        Close();
    }
}
