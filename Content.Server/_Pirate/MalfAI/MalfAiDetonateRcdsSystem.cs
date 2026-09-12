// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Server._Pirate.MalfAI;

public sealed class MalfAiDetonateRcdsSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosions = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly TimeSpan RcdDetonationDelay = TimeSpan.FromSeconds(5);
    private static readonly SoundSpecifier RcdBeepSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<Content.Shared.Store.Components.StoreComponent, MalfAiDetonateRcdsActionEvent>(OnDetonateAllRcdsAction);
    }

    private void OnDetonateAllRcdsAction(EntityUid uid, Content.Shared.Store.Components.StoreComponent comp, ref MalfAiDetonateRcdsActionEvent args)
    {
        var origin = args.Performer != default ? args.Performer : uid;
        // Defer until the next tick so entities spawned in the same action callback
        // have completed component initialization and are visible to the query.
        Timer.Spawn(TimeSpan.FromSeconds(1.0 / 30.0), () => ArmRcdsOnGrid(origin));
        args.Handled = true;
    }

    private void ArmRcdsOnGrid(EntityUid origin)
    {
        if (!Exists(origin))
            return;

        var perfXform = Transform(origin);
        var gridUid = perfXform.GridUid;
        var mapId = perfXform.MapID;
        if (gridUid == null && mapId == MapId.Nullspace)
            return;

        var query = EntityManager.AllEntityQueryEnumerator<RCDComponent, TransformComponent>();
        while (query.MoveNext(out var rcdUid, out _, out var xform))
        {
            if (gridUid != null
                ? xform.GridUid != gridUid
                : xform.MapID != mapId)
                continue;
            if (HasComp<BorgModuleComponent>(rcdUid))
                continue;

            if (_containers.TryGetContainingContainer((rcdUid, xform, null), out var container) && TryComp<HandsComponent>(container.Owner, out var hands) && _hands.IsHolding((container.Owner, hands), rcdUid))
                _popup.PopupEntity(Loc.GetString("detonate_rcd_warning"), container.Owner, container.Owner, PopupType.LargeCaution);

            var targetRcd = rcdUid;
            for (var s = 1; s < RcdDetonationDelay.TotalSeconds; s++)
            {
                Timer.Spawn(TimeSpan.FromSeconds(s), () =>
                {
                    if (Exists(targetRcd))
                        _audio.PlayPvs(RcdBeepSound, targetRcd);
                });
            }

            Timer.Spawn(RcdDetonationDelay, () =>
            {
                if (!Exists(targetRcd))
                    return;
                var coords = _xform.GetMapCoordinates(targetRcd, Transform(targetRcd));
                Del(targetRcd);
                _explosions.QueueExplosion(coords, ExplosionSystem.DefaultExplosionPrototypeId,
                    totalIntensity: 4f, slope: 1f, maxTileIntensity: 2f, cause: origin, maxTileBreak: 0);
            });
        }
    }
}
