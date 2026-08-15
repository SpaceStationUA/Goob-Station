// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server._Pirate.Instruments;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared._DV.NanoChat;
using Content.Shared._Pirate.CartridgeLoader.Cartridges;
using Content.Shared._Pirate.Instruments;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.CartridgeLoader.Cartridges;

/// <summary>Handles D.E.T.O.M.A.T.I.X. targeting and detonation.</summary>
public sealed class DetomatixCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridge = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SampledSongSystem _song = default!;
    [Dependency] private readonly SharedNanoChatSystem _nanoChat = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetomatixCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<DetomatixCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var detonations = EntityQueryEnumerator<DetomatixDetonationComponent>();
        while (detonations.MoveNext(out var uid, out var detonation))
        {
            if (now < detonation.DetonateAt || TerminatingOrDeleted(uid))
                continue;

            Detonate((uid, detonation));
        }

        var cartridges = EntityQueryEnumerator<DetomatixCartridgeComponent, CartridgeComponent>();
        while (cartridges.MoveNext(out var uid, out var comp, out var cartridge))
        {
            if (cartridge.LoaderUid is not { } loader || now < comp.NextRefresh)
                continue;

            comp.NextRefresh = now + RefreshInterval;

            if (!TryComp(loader, out CartridgeLoaderComponent? loaderComp) || loaderComp.ActiveProgram != uid)
                continue;

            UpdateUi((uid, comp), loader);
        }
    }

    private void OnUiReady(Entity<DetomatixCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(ent, args.Loader);
    }

    private void OnMessage(Entity<DetomatixCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not DetomatixUiMessageEvent msg)
            return;

        var loader = GetEntity(args.LoaderUid);
        TryBomb(ent, loader, args.Actor, msg.TargetNumber);
        UpdateUi(ent, loader);
    }

    private void TryBomb(Entity<DetomatixCartridgeComponent> ent, EntityUid loader, EntityUid actor, uint targetNumber)
    {
        if (ent.Comp.Charges <= 0)
        {
            Deny(loader, actor, "detomatix-error-no-charges");
            return;
        }

        if (!TryFindDevice(targetNumber, out var device))
        {
            Deny(loader, actor, "detomatix-error-unreachable");
            return;
        }

        if (HasComp<DetomatixDetonationComponent>(device))
        {
            Deny(loader, actor, "detomatix-error-already-armed");
            return;
        }

        if (!TryPickSong(ent, out var song, out var duration) ||
            !_song.TryPlaySong(device, song, ent.Comp.SongRange, ent.Comp.SongVolume))
        {
            Deny(loader, actor, "detomatix-error-unreachable");
            return;
        }

        var detonation = EnsureComp<DetomatixDetonationComponent>(device);
        detonation.DetonateAt = _timing.CurTime + TimeSpan.FromSeconds(duration) + ent.Comp.DetonationDelay;
        detonation.Bomber = actor;

        // Keep the countdown server-side.
        EnsureComp<DetomatixArmedComponent>(device);

        _popup.PopupEntity(
            Loc.GetString("detomatix-device-armed-warning", ("device", Name(device))),
            device,
            PopupType.LargeCaution);

        ent.Comp.Charges--;

        _popup.PopupEntity(Loc.GetString("detomatix-armed"), loader, actor);
        _adminLogger.Add(LogType.Action,
            LogImpact.Extreme,
            $"{ToPrettyString(actor):user} armed {ToPrettyString(device):entity} (NanoChat #{targetNumber:D4}) with {ToPrettyString(ent):tool}, playing {song} before detonation");
    }

    private bool TryFindDevice(uint targetNumber, out EntityUid device)
    {
        device = default;

        var query = EntityQueryEnumerator<NanoChatCardComponent>();
        while (query.MoveNext(out var cardUid, out var card))
        {
            if (!_nanoChat.GetListNumber((cardUid, card)) ||
                !HasComp<IdCardComponent>(cardUid) ||
                _nanoChat.GetNumber((cardUid, card)) != targetNumber)
            {
                continue;
            }

            if (card.PdaUid is not { } pda || TerminatingOrDeleted(pda))
                continue;

            device = pda;
            return true;
        }

        return false;
    }

    private bool TryPickSong(Entity<DetomatixCartridgeComponent> ent,
        out ProtoId<SampledSongPrototype> song,
        out float duration)
    {
        song = default;
        duration = 0f;

        if (ent.Comp.Songs.Count == 0)
            return false;

        song = _random.Pick(ent.Comp.Songs);
        if (!_proto.TryIndex(song, out var proto))
            return false;

        duration = proto.Duration;
        return true;
    }

    private void Detonate(Entity<DetomatixDetonationComponent> ent)
    {
        // This runs inside the component query.
        var comp = ent.Comp;
        RemCompDeferred<DetomatixDetonationComponent>(ent);

        EntityUid? bomber = comp.Bomber is { } uid && !Deleted(uid) ? uid : null;

        _popup.PopupEntity(Loc.GetString("detomatix-device-explodes", ("device", Name(ent))), ent);

        _explosion.QueueExplosion(ent,
            comp.ExplosionType,
            comp.TotalIntensity,
            comp.IntensitySlope,
            comp.MaxTileIntensity,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            user: bomber);

        QueueDel(ent);
    }

    private void Deny(EntityUid loader, EntityUid actor, string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), loader, actor);
    }

    private void UpdateUi(Entity<DetomatixCartridgeComponent> ent, EntityUid loader)
    {
        var targets = new List<DetomatixTarget>();

        var query = AllEntityQuery<NanoChatCardComponent, IdCardComponent>();
        while (query.MoveNext(out var cardUid, out var card, out var idCard))
        {
            if (!_nanoChat.GetListNumber((cardUid, card)) ||
                _nanoChat.GetNumber((cardUid, card)) is not { } number ||
                idCard.FullName is not { } fullName)
            {
                continue;
            }

            if (card.PdaUid is not { } pda || TerminatingOrDeleted(pda))
                continue;

            targets.Add(new DetomatixTarget(number,
                fullName,
                idCard.LocalizedJobTitle,
                GetLocation(pda),
                HasComp<DetomatixDetonationComponent>(pda)));
        }

        targets.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        _cartridge.UpdateCartridgeUiState(loader,
            new DetomatixUiState(targets, ent.Comp.Charges, ent.Comp.MaxCharges));
    }

    private string GetLocation(EntityUid uid)
    {
        var xform = Transform(uid);
        var vessel = xform.GridUid is { } grid ? Name(grid) : null;

        // Beacons are map-scoped, so require the same grid.
        string? area = null;
        if (xform.GridUid != null &&
            _navMap.TryGetNearestBeacon(uid, out var beacon, out _) &&
            Transform(beacon.Value).GridUid == xform.GridUid)
        {
            area = beacon.Value.Comp.Text;
        }

        return (vessel, area) switch
        {
            (not null, not null) => Loc.GetString("detomatix-location", ("vessel", vessel), ("area", area)),
            (not null, null) => vessel,
            (null, not null) => area,
            _ => Loc.GetString("detomatix-location-unknown"),
        };
    }
}
