// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Pirate.Shared.TV;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Gives television entities the Pirate TV verbs: right-click a TV in
///     world and choose the viewer (watch the room channel) or the browser
///     (pick what the room watches). Television prototypes get a
///     <see cref="WebTvComponent"/> client-side by prototype name, so the
///     server layer stays untouched.
/// </summary>
public sealed class WebTvStationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WebTvComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Cheap poll: attach the television marker when the entity first
        // becomes animated (seen-set keeps it a no-op thereafter).
        var query = AllEntityQuery<SpriteComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<WebTvComponent>(uid))
                continue;

            var proto = MetaData(uid).EntityPrototype;
            if (proto == null)
                continue;

            if (proto.ID.Contains("Television", StringComparison.OrdinalIgnoreCase))
                EnsureComp<WebTvComponent>(uid);
        }
    }

    private void OnGetVerbs(Entity<WebTvComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Телевізор",
            IconEntity = default,
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

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Браузер",
            Act = () =>
            {
                if (!DebounceVerify())
                    return;
                var window = new WebTvPickerWindow();
                window.TvUid = entity;
                window.OpenCenteredPicker();
            },
            Priority = 11,
        });

        // Room lock, presented like the standard lock verbs of other
        // station hardware (same icons, locked/unlocked wording).
        var roomLocked = WebTvWindow.Backend.Snapshot().Locked;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = roomLocked ? "Розблокувати ТБ" : "Замкнути ТБ",
            Icon = !roomLocked
                ? new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/lock.svg.192dpi.png"))
                : new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/unlock.svg.192dpi.png")),
            Act = () =>
            {
                if (!DebounceVerify())
                    return;
                var net = Robust.Shared.IoC.IoCManager
                    .Resolve<Robust.Shared.GameObjects.IEntityNetworkManager>();
                net.SendSystemNetworkMessage(new PirateTvLockEvent { Locked = !roomLocked });
            },
            Priority = 10,
        });

        // Admin-only: the same browser interface, openable any time.
        var admin = _admin.IsAdmin(args.User);

        if (admin)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = "Браузер (admin)",
                Act = () =>
                {
                    if (!DebounceVerify())
                        return;
                    var window = new WebTvPickerWindow();
                    window.TvUid = entity;
                    window.OpenCenteredPicker();
                },
                Priority = 9,
            });
        }
    }


    [Dependency] private readonly ISharedAdminManager _admin = default!;

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
