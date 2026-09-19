// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Pirate.Shared.TV;
using Content.Shared.Administration.Managers;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Gives television entities the Pirate TV verbs: right-click a TV to
///     open the viewer (which offers "Обрати відео" to open the YouTube
///     picker) or lock/unlock it for the room. The picker is no longer a
///     separate entity verb — it opens from inside the viewer.
/// </summary>
public sealed class WebTvStationSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PirateTvComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<PirateTvComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Телевізор",
            Act = () =>
            {
                if (!DebounceVerify())
                    return;
                var window = new WebTvWindow();
                window.TvUid = entity;
                window.OpenCenteredTv();
            },
            Priority = 12,
        });

        // Room lock, presented like the standard lock verbs of other
        // station hardware (same icons, locked/unlocked wording).
        var locked = entity.Comp.Locked;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = locked ? "Розблокувати ТБ" : "Замкнути ТБ",
            Icon = !locked
                ? new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/lock.svg.192dpi.png"))
                : new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png")),
            Act = () =>
            {
                if (!DebounceVerify())
                    return;
                PirateTvClientState.Send(new PirateTvLockEvent
                {
                    Tv = PirateTvClientState.Net(entity.Owner),
                    Locked = !locked,
                });
            },
            Priority = 10,
        });
    }

    private static long _lastWindowStamp;

    private static bool DebounceVerify()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now - _lastWindowStamp < 500)
            return false;
        _lastWindowStamp = now;
        return true;
    }
}
