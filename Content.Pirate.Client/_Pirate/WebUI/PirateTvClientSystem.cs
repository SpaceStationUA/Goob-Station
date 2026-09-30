// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.TV;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     TV client half: routes the server's per-TV state pushes into the
///     static <see cref="PirateTvClientState"/> registry every TV window
///     reads.
/// </summary>
public sealed class PirateTvClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateTvStateEvent>(OnState);
    }

    private void OnState(PirateTvStateEvent msg, EntitySessionEventArgs _)
        => PirateTvClientState.OnState(msg);
}
