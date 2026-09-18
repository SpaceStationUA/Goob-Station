// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using Content.Pirate.Shared.TV;
using Robust.Shared.Player;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     TV-2 client half: the server's room clock and playlist mirror into
///     the static <see cref="WebTvWindow.Backend"/> hub every TV and
///     picker window already follows. Picks, queue actions, lock toggles
///     and remote controls go out as network events; the authoritative
///     state lands back as broadcasts.
/// </summary>
public sealed class PirateTvClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateTvStateEvent>(OnState);
    }

    private void OnState(PirateTvStateEvent msg, EntitySessionEventArgs _)
    {
        WebTvWindow.Backend.Apply(s =>
        {
            s.Url = msg.Url;
            s.Kind = (WebTvChannel.WebTvKind)msg.Kind;
            s.Label = msg.Label;
            s.Playing = msg.Playing;
            s.Pos = msg.Pos;
            s.Stamp = msg.Stamp;
            s.Locked = msg.Locked;
            s.QueueNow = msg.Now;
            var list = new List<WebTvQueueEntry>();
            foreach (var it in msg.Items)
                list.Add(new WebTvQueueEntry
                {
                    Url = it.Url,
                    Kind = (WebTvChannel.WebTvKind)it.Kind,
                    Label = it.Label,
                    Title = it.Title,
                });
            s.Queue = list;
        });
    }
}
