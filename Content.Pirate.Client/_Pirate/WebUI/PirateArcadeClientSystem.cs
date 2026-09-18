// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Pirate.Shared.Arcade;
using Robust.Shared.Player;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     WebArcade client half: the server's per-cabinet seat state mirrors
///     into <see cref="WebArcadeBackend"/>; frame relays from a seated
///     player get forwarded into the right spectator window through the
///     per-cabinet receiver set.
/// </summary>
public sealed class PirateArcadeClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateArcadeStateEvent>(OnState);
        SubscribeNetworkEvent<PirateArcadeFrameEvent>(OnFrame);
    }

    private void OnState(PirateArcadeStateEvent msg, EntitySessionEventArgs _)
    {
        WebArcadeBackend.Apply(msg.Cab, s =>
        {
            s.Taken = msg.Taken;
            s.PlayerName = msg.PlayerName;
        });
    }

    private void OnFrame(PirateArcadeFrameEvent msg, EntitySessionEventArgs _)
    {
        WebArcadeBackend.DispatchFrame(msg.Cab, msg.Frame);
    }
}
