// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using Content.Server._Pirate.Photo;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._Pirate.RoundEnd;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Pirate.RoundEnd.PhotoAlbum;
public sealed class PhotoAlbumSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    private readonly HashSet<EntityUid> _unsignedAutoSignAlbums = new();
    private readonly Dictionary<Guid, byte[]> _roundEndImageData = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeNetworkEvent<PhotoAlbumImageRequestEvent>(OnPhotoAlbumImageRequest);

        SubscribeLocalEvent<AutoSignPhotoAlbumComponent, ComponentStartup>(OnAutoSignStartup);
        SubscribeLocalEvent<AutoSignPhotoAlbumComponent, ComponentShutdown>(OnAutoSignShutdown);

        SubscribeLocalEvent<PhotoAlbumComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<PhotoAlbumComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PhotoAlbumComponent, ComponentStartup>(OnPhotoAlbumStartup);
        SubscribeLocalEvent<PhotoAlbumComponent, ComponentShutdown>(OnPhotoAlbumShutdown);
        SubscribeLocalEvent<PhotoAlbumComponent, EntInsertedIntoContainerMessage>(OnPhotoAlbumInserted);
        SubscribeLocalEvent<PhotoAlbumComponent, EntRemovedFromContainerMessage>(OnPhotoAlbumRemoved);
    }

    private void OnPhotoAlbumImageRequest(PhotoAlbumImageRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_roundEndImageData.TryGetValue(ev.ImageId, out var imageData))
        {
            Log.Warning($"Round-end photo image cache miss for ImageId {ev.ImageId} requested by session {args.SenderSession}.");
        }

        RaiseNetworkEvent(new PhotoAlbumImageResponseEvent(ev.ImageId, imageData), args.SenderSession);
    }

    private void OnPhotoAlbumStartup(Entity<PhotoAlbumComponent> entity, ref ComponentStartup args)
    {
        RefreshUnsignedAutoSignTracking(entity.Owner);

        if (entity.Comp.IsSigned)
            UpdateSignedAlbumName(entity);
    }

    private void OnPhotoAlbumShutdown(Entity<PhotoAlbumComponent> entity, ref ComponentShutdown args)
    {
        _unsignedAutoSignAlbums.Remove(entity.Owner);
    }

    private void OnAutoSignStartup(EntityUid uid, AutoSignPhotoAlbumComponent component, ref ComponentStartup args)
    {
        RefreshUnsignedAutoSignTracking(uid);
    }

    private void OnAutoSignShutdown(EntityUid uid, AutoSignPhotoAlbumComponent component, ref ComponentShutdown args)
    {
        _unsignedAutoSignAlbums.Remove(uid);
    }

    private void OnPhotoAlbumInserted(EntityUid uid, PhotoAlbumComponent component, EntInsertedIntoContainerMessage args)
    {
        RefreshUnsignedAutoSignTracking(uid, component);
    }

    private void OnPhotoAlbumRemoved(EntityUid uid, PhotoAlbumComponent component, EntRemovedFromContainerMessage args)
    {
        RefreshUnsignedAutoSignTracking(uid, component);
    }

    private void RefreshUnsignedAutoSignTracking(EntityUid uid, PhotoAlbumComponent? photoAlbum = null)
    {
        if (!Resolve(uid, ref photoAlbum, false) ||
            photoAlbum.IsSigned ||
            !HasComp<AutoSignPhotoAlbumComponent>(uid))
        {
            _unsignedAutoSignAlbums.Remove(uid);
            return;
        }

        _unsignedAutoSignAlbums.Add(uid);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (_unsignedAutoSignAlbums.Count == 0)
            return;

        var snapshot = new List<EntityUid>(_unsignedAutoSignAlbums);
        foreach (var uid in snapshot)
        {
            if (!TryComp<PhotoAlbumComponent>(uid, out var photoAlbum) ||
                !HasComp<AutoSignPhotoAlbumComponent>(uid) ||
                photoAlbum.IsSigned)
            {
                _unsignedAutoSignAlbums.Remove(uid);
                continue;
            }

            if (!IsOwnedBy(uid, ev.Mob))
                continue;

            SignPhotoAlbum((uid, photoAlbum), ev.Mob, ev.Player.Data.UserName, usePossessiveSignerName: true);
        }
    }

    private bool IsOwnedBy(EntityUid uid, EntityUid owner)
    {
        var current = uid;
        var depth = 0;

        while (depth < 64 && _container.TryGetContainingContainer(current, out var container))
        {
            if (container.Owner == owner)
                return true;

            current = container.Owner;
            depth++;
        }

        return false;
    }

    private void OnGetVerbs(Entity<PhotoAlbumComponent> entity, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || entity.Comp.IsSigned)
            return;

        if (args.Using is not { } pen || !_tags.HasTag(pen, "Write"))
            return;

        var target = args.Target;
        var user = args.User;

        var verb = new Verb
        {
            Text = Loc.GetString("photoalbum-sign-verb"),
            Act = () => VerbSignPhotoAlbum(entity, user)
        };
        args.Verbs.Add(verb);
    }

    private void OnExamine(Entity<PhotoAlbumComponent> entity, ref ExaminedEvent args)
    {
        if (!entity.Comp.IsSigned)
            return;

        args.PushMarkup(Loc.GetString("photoalbum-signed-examine"));
    }

    private void VerbSignPhotoAlbum(Entity<PhotoAlbumComponent> entity, EntityUid user)
    {
        string? username = null;
        if (_player.TryGetSessionByEntity(user, out var session))
            username = session.Data.UserName;

        SignPhotoAlbum(entity, user, username, usePossessiveSignerName: true);

        _popup.PopupEntity(Loc.GetString("photoalbum-signed", ("user", user)), entity);
        _audio.PlayPvs(entity.Comp.SignSound, entity);
    }

    private void SignPhotoAlbum(Entity<PhotoAlbumComponent> entity, EntityUid signer, string? signerUsername, bool usePossessiveSignerName)
    {
        entity.Comp.IsSigned = true;
        entity.Comp.SignerUid = signer;
        entity.Comp.SignerUsername = signerUsername;
        entity.Comp.SignerName = MetaData(signer).EntityName;
        entity.Comp.UsePossessiveSignerName = usePossessiveSignerName;

        _unsignedAutoSignAlbums.Remove(entity.Owner);
        UpdateSignedAlbumName(entity);
    }

    private void UpdateSignedAlbumName(Entity<PhotoAlbumComponent> entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Comp.SignerName))
            return;

        var albumName = entity.Comp.UsePossessiveSignerName
            ? Loc.GetString("photoalbum-signed-name-possessive", ("name", entity.Comp.SignerName))
            : Loc.GetString("photoalbum-signed-name-title", ("name", entity.Comp.SignerName));

        _metaData.SetEntityName(entity, albumName);
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent args)
    {
        _roundEndImageData.Clear();

        List<AlbumData>? albums = new();
        var query = EntityQueryEnumerator<PhotoAlbumComponent>();

        while (query.MoveNext(out var uid, out var photoAlbum)) // query all photoalbums and send photos them to players
        {
            if (!_container.TryGetContainer(uid, photoAlbum.ContainerId, out var container))
                continue;

            List<AlbumImageData> photos = new();

            string? authorCKey = default;
            string? authorName = default;

            foreach (var item in container.ContainedEntities)
            {
                if (!TryComp<PhotoCardComponent>(item, out var photoCard))
                    continue;

                if (photoCard.ImageData is null)
                    continue;

                var imageId = Guid.NewGuid();
                _roundEndImageData[imageId] = photoCard.ImageData;
                photos.Add(new AlbumImageData(imageId, photoCard.PreviewData, photoCard.CustomName));
            }

            if (photos.Count == 0)
                continue;

            if (photoAlbum.IsSigned)
            {
                if (photoAlbum.SignerUid is not null && Exists(photoAlbum.SignerUid))
                    authorName = MetaData(photoAlbum.SignerUid.Value).EntityName;
                else
                    authorName = photoAlbum.SignerName;

                authorCKey = photoAlbum.SignerUsername;
            }

            albums.Add(new AlbumData(photos, authorCKey, authorName));
        }

        if (albums.Count > 0)
            RaiseNetworkEvent(new PhotoAlbumEvent(albums));
    }
}


