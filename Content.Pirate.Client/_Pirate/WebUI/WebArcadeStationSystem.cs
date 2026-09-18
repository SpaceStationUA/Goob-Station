// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Gives arcade machines the Pirate WebArcade verb ("Игровой автомат"):
///     right-click a machine in world and open the CEF game in a window.
///     Arcade prototypes get a <see cref="WebArcadeComponent"/> client-side
///     by prototype name (contains "Arcade"), so the server layer stays
///     untouched.
/// </summary>
public sealed class WebArcadeStationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WebArcadeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Cheap poll: attach the arcade marker when the entity first becomes
        // animated (seen-set keeps it a no-op thereafter).
        var query = AllEntityQuery<SpriteComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<WebArcadeComponent>(uid))
                continue;

            var proto = MetaData(uid).EntityPrototype;
            if (proto == null)
                continue;

            // Real arcade machines: SpaceVillainArcade*, ArcadeMachine,
            // BlockGame* — anything with "Arcade" in the prototype id.
            if (proto.ID.Contains("Arcade", StringComparison.OrdinalIgnoreCase))
                EnsureComp<WebArcadeComponent>(uid);
        }
    }

    private void OnGetVerbs(Entity<WebArcadeComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Гральний автомат",
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/buckle.svg.192dpi.png")),
            Act = () =>
            {
                if (!DebounceVerify())
                    return;
                if (WebArcadeWindow.TryGetOpen(NetEntityWithContext(entity), out _))
                    return; // already in a window on this cabinet
                var window = new WebArcadeWindow();
                window.CabUid = entity;
                window.OpenCenteredArcade();
            },
            Priority = 12,
        });
    }

    private static Robust.Shared.GameObjects.NetEntity NetEntityWithContext(Entity<WebArcadeComponent> entity)
    {
        return Robust.Shared.IoC.IoCManager
            .Resolve<Robust.Shared.GameObjects.IEntityManager>().GetNetEntity(entity);
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
